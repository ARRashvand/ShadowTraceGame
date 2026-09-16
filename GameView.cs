using Android.Content;
using Android.Graphics;
using Android.Views;
using System.Diagnostics;

namespace ShadowTraceGame;

public sealed class GameView : View
{
    private const float WorldWidth = 1080f;
    private const float WorldHeight = 1920f;
    private const float PlayerRadius = 38f;
    private const float PlayerSpeed = 510f;
    private const float JoystickRadius = 118f;
    private const float LoopDuration = 12f;
    private const float StartX = WorldWidth / 2f;
    private const float StartY = 1630f;

    private readonly Paint _paint = new(PaintFlags.AntiAlias);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly RectF _room = new(60f, 215f, 1020f, 1770f);
    private readonly List<RectF> _walls = [];
    private readonly List<PathSample> _recordedPath = [];

    private float _playerX = StartX;
    private float _playerY = StartY;
    private float _ghostX = StartX;
    private float _ghostY = StartY;
    private float _inputX;
    private float _inputY;
    private float _touchStartX;
    private float _touchStartY;
    private float _touchX;
    private float _touchY;
    private bool _touching;
    private bool _loopRunning;
    private bool _firstRecordingComplete;
    private float _loopElapsed;
    private int _ghostPlaybackIndex;
    private long _lastFrameMs;
    private string? _fatalError;

    public GameView(Context context) : base(context)
    {
        SetBackgroundColor(Color.Rgb(10, 13, 24));
        KeepScreenOn = true;

        _walls.Add(new RectF(60f, 995f, 420f, 1025f));
        _walls.Add(new RectF(660f, 995f, 1020f, 1025f));
        _walls.Add(new RectF(270f, 625f, 810f, 655f));
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        if (_fatalError is not null)
        {
            DrawFatalError(canvas);
            return;
        }

        try
        {
            DrawGameFrame(canvas);
        }
        catch (Exception exception)
        {
            _fatalError = $"DRAW ERROR: {exception.GetType().Name} - {exception.Message}";
            DrawFatalError(canvas);
        }
    }

    private void DrawGameFrame(Canvas canvas)
    {

        var now = _clock.ElapsedMilliseconds;
        var deltaSeconds = _lastFrameMs == 0
            ? 0f
            : Math.Min((now - _lastFrameMs) / 1000f, 0.033f);
        _lastFrameMs = now;

        UpdateGame(deltaSeconds);

        var scale = Math.Min(Width / WorldWidth, Height / WorldHeight);
        var offsetX = (Width - WorldWidth * scale) / 2f;
        var offsetY = (Height - WorldHeight * scale) / 2f;

        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale, scale);

        DrawBackground(canvas);
        DrawRoom(canvas);
        DrawGhost(canvas);
        DrawPlayer(canvas);
        DrawJoystick(canvas, scale, offsetX, offsetY);

        canvas.Restore();
        PostInvalidateOnAnimation();
    }

    private void DrawFatalError(Canvas canvas)
    {
        _paint.Color = Color.Rgb(10, 13, 24);
        canvas.DrawRect(0f, 0f, Width, Height, _paint);
        _paint.Color = Color.Rgb(255, 110, 110);
        _paint.TextAlign = Paint.Align.Center;
        _paint.TextSize = 32f;
        canvas.DrawText(_fatalError ?? "UNKNOWN ERROR", Width / 2f, Height / 2f, _paint);
    }

    private void UpdateGame(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || !_loopRunning)
            return;

        _loopElapsed = Math.Min(_loopElapsed + deltaSeconds, LoopDuration);

        var nextX = _playerX + _inputX * PlayerSpeed * deltaSeconds;
        var nextY = _playerY + _inputY * PlayerSpeed * deltaSeconds;

        if (CanStandAt(nextX, _playerY))
            _playerX = nextX;

        if (CanStandAt(_playerX, nextY))
            _playerY = nextY;

        if (_firstRecordingComplete)
            UpdateGhostPosition(_loopElapsed);
        else
            _recordedPath.Add(new PathSample(_loopElapsed, _playerX, _playerY));

        if (_loopElapsed >= LoopDuration)
            FinishLoop();
    }

    private void StartLoop()
    {
        _loopRunning = true;
        _loopElapsed = 0f;
        _ghostPlaybackIndex = 0;

        if (!_firstRecordingComplete)
        {
            _recordedPath.Clear();
            _recordedPath.Add(new PathSample(0f, _playerX, _playerY));
        }
        else
        {
            UpdateGhostPosition(0f);
        }
    }

    private void FinishLoop()
    {
        if (!_firstRecordingComplete)
        {
            _recordedPath.Add(new PathSample(LoopDuration, _playerX, _playerY));
            _firstRecordingComplete = true;
        }

        _loopRunning = false;
        _loopElapsed = 0f;
        _playerX = StartX;
        _playerY = StartY;
        _ghostX = StartX;
        _ghostY = StartY;
        _ghostPlaybackIndex = 0;
        _inputX = 0f;
        _inputY = 0f;
        _touching = false;
    }

    private void UpdateGhostPosition(float time)
    {
        if (_recordedPath.Count == 0)
            return;

        while (_ghostPlaybackIndex + 1 < _recordedPath.Count
            && _recordedPath[_ghostPlaybackIndex + 1].Time <= time)
        {
            _ghostPlaybackIndex++;
        }

        var current = _recordedPath[_ghostPlaybackIndex];
        if (_ghostPlaybackIndex + 1 >= _recordedPath.Count)
        {
            _ghostX = current.X;
            _ghostY = current.Y;
            return;
        }

        var next = _recordedPath[_ghostPlaybackIndex + 1];
        var span = Math.Max(next.Time - current.Time, 0.0001f);
        var amount = Math.Clamp((time - current.Time) / span, 0f, 1f);
        _ghostX = current.X + (next.X - current.X) * amount;
        _ghostY = current.Y + (next.Y - current.Y) * amount;
    }

    private bool CanStandAt(float x, float y)
    {
        if (x - PlayerRadius < _room.Left || x + PlayerRadius > _room.Right
            || y - PlayerRadius < _room.Top || y + PlayerRadius > _room.Bottom)
        {
            return false;
        }

        var playerBounds = new RectF(
            x - PlayerRadius,
            y - PlayerRadius,
            x + PlayerRadius,
            y + PlayerRadius);

        return !_walls.Any(wall => RectF.Intersects(playerBounds, wall));
    }

    private void DrawBackground(Canvas canvas)
    {
        _paint.Color = Color.Rgb(10, 13, 24);
        canvas.DrawRect(0f, 0f, WorldWidth, WorldHeight, _paint);

        _paint.TextAlign = Paint.Align.Center;
        _paint.SetTypeface(Typeface.Create(Typeface.Default, TypefaceStyle.Bold));
        _paint.TextSize = 46f;
        _paint.Color = Color.Rgb(235, 241, 255);
        canvas.DrawText("رد من", WorldWidth / 2f, 60f, _paint);

        _paint.SetTypeface(Typeface.Default);
        _paint.TextSize = 29f;
        _paint.Color = Color.Rgb(139, 154, 190);
        var status = !_firstRecordingComplete
            ? (_loopRunning ? "دور اول — در حال ضبط مسیر" : "حرکت کن تا دور اول شروع شود")
            : (_loopRunning ? "دور دوم — سایه‌ات برگشته" : "حرکت کن تا سایه شروع شود");
        canvas.DrawText(status, WorldWidth / 2f, 108f, _paint);

        var progress = _loopRunning ? _loopElapsed / LoopDuration : 0f;
        _paint.Color = Color.Rgb(35, 45, 69);
        canvas.DrawRoundRect(new RectF(100f, 137f, 980f, 171f), 17f, 17f, _paint);
        _paint.Color = _firstRecordingComplete
            ? Color.Rgb(190, 111, 255)
            : Color.Rgb(69, 220, 255);
        canvas.DrawRoundRect(new RectF(100f, 137f, 100f + 880f * progress, 171f), 17f, 17f, _paint);

        _paint.TextSize = 25f;
        _paint.Color = Color.Rgb(235, 241, 255);
        var seconds = _loopRunning ? Math.Ceiling(LoopDuration - _loopElapsed) : LoopDuration;
        canvas.DrawText($"{seconds:0}", WorldWidth / 2f, 164f, _paint);
    }

    private void DrawRoom(Canvas canvas)
    {
        _paint.SetStyle(Paint.Style.Fill);
        _paint.Color = Color.Rgb(18, 25, 43);
        canvas.DrawRoundRect(_room, 34f, 34f, _paint);

        _paint.SetStyle(Paint.Style.Stroke);
        _paint.StrokeWidth = 8f;
        _paint.Color = Color.Rgb(49, 63, 96);
        canvas.DrawRoundRect(_room, 34f, 34f, _paint);
        _paint.SetStyle(Paint.Style.Fill);

        const float gridSize = 96f;
        _paint.StrokeWidth = 2f;
        _paint.Color = Color.Argb(45, 108, 132, 178);
        for (var x = _room.Left + gridSize; x < _room.Right; x += gridSize)
            canvas.DrawLine(x, _room.Top, x, _room.Bottom, _paint);
        for (var y = _room.Top + gridSize; y < _room.Bottom; y += gridSize)
            canvas.DrawLine(_room.Left, y, _room.Right, y, _paint);

        foreach (var wall in _walls)
        {
            _paint.Color = Color.Rgb(76, 91, 126);
            canvas.DrawRoundRect(wall, 12f, 12f, _paint);
        }

        _paint.Color = Color.Rgb(95, 226, 190);
        canvas.DrawRoundRect(new RectF(430f, 217f, 650f, 243f), 13f, 13f, _paint);
        _paint.TextAlign = Paint.Align.Center;
        _paint.TextSize = 27f;
        _paint.Color = Color.Rgb(95, 226, 190);
        canvas.DrawText("خروجی آینده", WorldWidth / 2f, 290f, _paint);
    }

    private void DrawGhost(Canvas canvas)
    {
        if (!_firstRecordingComplete)
            return;

        _paint.Color = Color.Argb(125, 190, 111, 255);
        canvas.DrawCircle(_ghostX, _ghostY, PlayerRadius, _paint);

        _paint.Color = Color.Argb(170, 255, 255, 255);
        canvas.DrawCircle(_ghostX - 12f, _ghostY - 7f, 5f, _paint);
        canvas.DrawCircle(_ghostX + 12f, _ghostY - 7f, 5f, _paint);
    }

    private void DrawPlayer(Canvas canvas)
    {
        _paint.Color = Color.Rgb(69, 220, 255);
        canvas.DrawCircle(_playerX, _playerY, PlayerRadius, _paint);

        _paint.Color = Color.White;
        canvas.DrawCircle(_playerX - 12f, _playerY - 7f, 5f, _paint);
        canvas.DrawCircle(_playerX + 12f, _playerY - 7f, 5f, _paint);
    }

    private void DrawJoystick(Canvas canvas, float scale, float offsetX, float offsetY)
    {
        if (!_touching)
            return;

        var startX = (_touchStartX - offsetX) / scale;
        var startY = (_touchStartY - offsetY) / scale;
        var knobX = (_touchX - offsetX) / scale;
        var knobY = (_touchY - offsetY) / scale;

        var dx = knobX - startX;
        var dy = knobY - startY;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        if (length > JoystickRadius)
        {
            knobX = startX + dx / length * JoystickRadius;
            knobY = startY + dy / length * JoystickRadius;
        }

        _paint.Color = Color.Argb(55, 255, 255, 255);
        canvas.DrawCircle(startX, startY, JoystickRadius, _paint);
        _paint.Color = Color.Argb(150, 69, 220, 255);
        canvas.DrawCircle(knobX, knobY, 48f, _paint);
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null)
            return false;

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _touching = true;
                _touchStartX = _touchX = e.GetX();
                _touchStartY = _touchY = e.GetY();
                UpdateInput();
                return true;

            case MotionEventActions.Move:
                if (!_touching)
                    return true;
                _touchX = e.GetX();
                _touchY = e.GetY();
                UpdateInput();
                return true;

            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                _touching = false;
                _inputX = 0f;
                _inputY = 0f;
                return true;
        }

        return base.OnTouchEvent(e);
    }

    private void UpdateInput()
    {
        var dx = _touchX - _touchStartX;
        var dy = _touchY - _touchStartY;
        var length = MathF.Sqrt(dx * dx + dy * dy);

        if (length < 14f)
        {
            _inputX = 0f;
            _inputY = 0f;
            return;
        }

        var strength = Math.Min(length / 118f, 1f);
        _inputX = dx / length * strength;
        _inputY = dy / length * strength;

        if (!_loopRunning)
            StartLoop();
    }

    private readonly record struct PathSample(float Time, float X, float Y);
}
