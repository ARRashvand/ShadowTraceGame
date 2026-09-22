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
    private const int RequiredGhostCount = 2;
    private const float RedSwitchX = 250f;
    private const float RedSwitchY = 1390f;
    private const float BlueSwitchX = 830f;
    private const float BlueSwitchY = 800f;
    private const float SwitchRadius = 62f;
    private const float ExitSequenceDuration = 1.65f;
    private const float IntroDuration = 0.72f;

    private readonly Paint _paint = new(PaintFlags.AntiAlias);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly RectF _room = new(60f, 215f, 1020f, 1770f);
    private readonly RectF _redDoor = new(420f, 985f, 660f, 1035f);
    private readonly RectF _blueDoor = new(420f, 615f, 660f, 665f);
    private readonly RectF _goal = new(430f, 275f, 650f, 400f);
    private readonly RectF _exitDoor = new(420f, 217f, 660f, 365f);
    private readonly RectF _restartButton = new(890f, 35f, 1040f, 105f);
    private readonly RectF _feedbackButton = new(250f, 970f, 830f, 1050f);
    private readonly RectF _replayButton = new(250f, 1070f, 830f, 1150f);
    private readonly RectF _nextLevelButton = new(250f, 1170f, 830f, 1250f);
    private readonly List<RectF> _walls = [];
    private readonly List<List<PathSample>> _recordedPaths = [];
    private readonly List<PathSample> _currentRecording = [];
    private readonly List<GhostPlayback> _ghosts = [];
    private readonly Context _context;

    private float _playerX = StartX;
    private float _playerY = StartY;
    private float _inputX;
    private float _inputY;
    private float _touchStartX;
    private float _touchStartY;
    private float _touchX;
    private float _touchY;
    private bool _touching;
    private bool _loopRunning;
    private bool _redSwitchActive;
    private bool _redDoorOpen;
    private bool _blueSwitchActive;
    private bool _blueDoorOpen;
    private bool _levelComplete;
    private bool _exitSequenceActive;
    private bool _exitHapticTriggered;
    private bool _introComplete;
    private bool _tutorialVisible;
    private float _loopElapsed;
    private float _exitSequenceElapsed;
    private float _introElapsed;
    private float _exitStartX;
    private float _exitStartY;
    private long _lastFrameMs;
    private string? _fatalError;

    public GameView(Context context) : base(context)
    {
        _context = context;
        _tutorialVisible = context
            .GetSharedPreferences("shadow_trace", FileCreationMode.Private)?
            .GetBoolean("tutorial_seen_two_ghosts", false) != true;
        SetBackgroundColor(Color.Rgb(10, 13, 24));
        KeepScreenOn = true;

        _walls.Add(new RectF(60f, 995f, 420f, 1025f));
        _walls.Add(new RectF(660f, 995f, 1020f, 1025f));
        _walls.Add(new RectF(60f, 625f, 420f, 655f));
        _walls.Add(new RectF(660f, 625f, 1020f, 655f));
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

        if (!_introComplete)
        {
            DrawIntro(canvas);
            canvas.Restore();
            PostInvalidateOnAnimation();
            return;
        }

        DrawBackground(canvas);
        DrawRoom(canvas);
        DrawGhosts(canvas);
        DrawVictoryParticles(canvas);
        DrawPlayer(canvas);
        DrawJoystick(canvas, scale, offsetX, offsetY);
        DrawExitFlash(canvas);
        DrawCompletionOverlay(canvas);
        DrawTutorialOverlay(canvas);

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
        if (deltaSeconds <= 0f)
            return;

        if (!_introComplete)
        {
            _introElapsed += deltaSeconds;
            _introComplete = _introElapsed >= IntroDuration;
            return;
        }

        if (_levelComplete)
            return;

        if (_exitSequenceActive)
        {
            UpdateExitSequence(deltaSeconds);
            return;
        }

        if (!_loopRunning)
            return;

        _loopElapsed = Math.Min(_loopElapsed + deltaSeconds, LoopDuration);

        foreach (var ghost in _ghosts)
            UpdateGhostPosition(ghost, _loopElapsed);

        UpdateMechanisms();

        var nextX = _playerX + _inputX * PlayerSpeed * deltaSeconds;
        var nextY = _playerY + _inputY * PlayerSpeed * deltaSeconds;

        if (CanStandAt(nextX, _playerY))
            _playerX = nextX;

        if (CanStandAt(_playerX, nextY))
            _playerY = nextY;

        UpdateMechanisms();

        if (_recordedPaths.Count < RequiredGhostCount)
            _currentRecording.Add(new PathSample(_loopElapsed, _playerX, _playerY));

        if (_recordedPaths.Count >= RequiredGhostCount && _goal.Contains(_playerX, _playerY))
        {
            StartExitSequence();
            return;
        }

        if (_loopElapsed >= LoopDuration)
            FinishLoop();
    }

    private void StartLoop()
    {
        if (_levelComplete)
            return;

        _loopRunning = true;
        _loopElapsed = 0f;
        ResetGhostPlaybacks();

        _currentRecording.Clear();
        if (_recordedPaths.Count < RequiredGhostCount)
            _currentRecording.Add(new PathSample(0f, _playerX, _playerY));
    }

    private void FinishLoop()
    {
        if (_recordedPaths.Count < RequiredGhostCount)
        {
            _currentRecording.Add(new PathSample(LoopDuration, _playerX, _playerY));
            _recordedPaths.Add([.. _currentRecording]);
            _currentRecording.Clear();
        }

        _loopRunning = false;
        _loopElapsed = 0f;
        _playerX = StartX;
        _playerY = StartY;
        ResetGhostPlaybacks();
        _inputX = 0f;
        _inputY = 0f;
        _touching = false;
        UpdateMechanisms();
    }

    private void ResetLevel()
    {
        _recordedPaths.Clear();
        _currentRecording.Clear();
        _ghosts.Clear();
        _playerX = StartX;
        _playerY = StartY;
        _inputX = 0f;
        _inputY = 0f;
        _loopElapsed = 0f;
        _redSwitchActive = false;
        _redDoorOpen = false;
        _blueSwitchActive = false;
        _blueDoorOpen = false;
        _levelComplete = false;
        _exitSequenceActive = false;
        _exitHapticTriggered = false;
        _exitSequenceElapsed = 0f;
        _exitStartX = StartX;
        _exitStartY = StartY;
        _loopRunning = false;
        _touching = false;
    }

    private void DismissTutorial()
    {
        _tutorialVisible = false;
        _context.GetSharedPreferences("shadow_trace", FileCreationMode.Private)?
            .Edit()?
            .PutBoolean("tutorial_seen_two_ghosts", true)?
            .Apply();
    }

    private void ShareFeedback()
    {
        var feedback =
            "رد من — نسخه ۰.۶\n\n" +
            "۱. آیا هدف بازی را سریع فهمیدی؟ چرا؟\n" +
            "۲. کنترل حرکت را از ۱ تا ۵ چند می‌دهی؟\n" +
            "۳. همکاری با دو سایه گذشته‌ات جذاب بود؟\n" +
            "۴. کجا گیج شدی یا گیر کردی؟\n" +
            "۵. اگر یک چیز را تغییر دهی، چه خواهد بود؟";

        var intent = new Intent(Intent.ActionSend);
        intent.SetType("text/plain");
        intent.PutExtra(Intent.ExtraText, feedback);
        _context.StartActivity(Intent.CreateChooser(intent, "ارسال نظر درباره رد من"));
    }

    private void StartExitSequence()
    {
        _exitSequenceActive = true;
        _exitSequenceElapsed = 0f;
        _exitStartX = _playerX;
        _exitStartY = _playerY;
        _loopRunning = false;
        _touching = false;
        _inputX = 0f;
        _inputY = 0f;

        if (!_exitHapticTriggered)
        {
            PerformHapticFeedback(FeedbackConstants.LongPress);
            _exitHapticTriggered = true;
        }
    }

    private void UpdateExitSequence(float deltaSeconds)
    {
        _exitSequenceElapsed = Math.Min(_exitSequenceElapsed + deltaSeconds, ExitSequenceDuration);

        var leaveProgress = EaseInOut((_exitSequenceElapsed - 0.36f) / 0.84f);
        _playerX = _exitStartX + (WorldWidth / 2f - _exitStartX) * leaveProgress;
        _playerY = _exitStartY + (140f - _exitStartY) * leaveProgress;

        if (_exitSequenceElapsed >= ExitSequenceDuration)
            CompleteLevel();
    }

    private void CompleteLevel()
    {
        _levelComplete = true;
        _exitSequenceActive = false;
        _loopRunning = false;
        _touching = false;
        _inputX = 0f;
        _inputY = 0f;
    }

    private void UpdateMechanisms()
    {
        _redSwitchActive = IsOnSwitch(_playerX, _playerY, RedSwitchX, RedSwitchY)
            || _ghosts.Any(ghost => IsOnSwitch(ghost.X, ghost.Y, RedSwitchX, RedSwitchY));
        _blueSwitchActive = IsOnSwitch(_playerX, _playerY, BlueSwitchX, BlueSwitchY)
            || _ghosts.Any(ghost => IsOnSwitch(ghost.X, ghost.Y, BlueSwitchX, BlueSwitchY));

        // Keep each door open until the player has completely passed through it.
        _redDoorOpen = _redSwitchActive
            || CircleIntersectsRect(_playerX, _playerY, PlayerRadius, _redDoor);
        _blueDoorOpen = _blueSwitchActive
            || CircleIntersectsRect(_playerX, _playerY, PlayerRadius, _blueDoor);
    }

    private static bool IsOnSwitch(float x, float y, float switchX, float switchY)
    {
        var dx = x - switchX;
        var dy = y - switchY;
        var activationRadius = SwitchRadius - 8f;
        return dx * dx + dy * dy <= activationRadius * activationRadius;
    }

    private static bool CircleIntersectsRect(float x, float y, float radius, RectF rect)
    {
        var closestX = Math.Clamp(x, rect.Left, rect.Right);
        var closestY = Math.Clamp(y, rect.Top, rect.Bottom);
        var dx = x - closestX;
        var dy = y - closestY;
        return dx * dx + dy * dy < radius * radius;
    }

    private void ResetGhostPlaybacks()
    {
        _ghosts.Clear();
        for (var index = 0; index < _recordedPaths.Count; index++)
            _ghosts.Add(new GhostPlayback(_recordedPaths[index], index));
    }

    private static void UpdateGhostPosition(GhostPlayback ghost, float time)
    {
        if (ghost.Path.Count == 0)
            return;

        while (ghost.SampleIndex + 1 < ghost.Path.Count
            && ghost.Path[ghost.SampleIndex + 1].Time <= time)
        {
            ghost.SampleIndex++;
        }

        var current = ghost.Path[ghost.SampleIndex];
        if (ghost.SampleIndex + 1 >= ghost.Path.Count)
        {
            ghost.X = current.X;
            ghost.Y = current.Y;
            return;
        }

        var next = ghost.Path[ghost.SampleIndex + 1];
        var span = Math.Max(next.Time - current.Time, 0.0001f);
        var amount = Math.Clamp((time - current.Time) / span, 0f, 1f);
        ghost.X = current.X + (next.X - current.X) * amount;
        ghost.Y = current.Y + (next.Y - current.Y) * amount;
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

        if (_walls.Any(wall => RectF.Intersects(playerBounds, wall)))
            return false;

        if (!_redDoorOpen && RectF.Intersects(playerBounds, _redDoor))
            return false;

        return _blueDoorOpen || !RectF.Intersects(playerBounds, _blueDoor);
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
        var status = _levelComplete
            ? "مرحله کامل شد"
            : _exitSequenceActive
                ? "در خروج باز شد"
            : _recordedPaths.Count switch
            {
                0 => _loopRunning ? "دور اول — کلید قرمز را نگه دار" : "هدف: با دو سایه به در خروج برس",
                1 => _loopRunning ? "دور دوم — کلید آبی را نگه دار" : "سایه اول آماده است — حرکت کن",
                _ => _loopRunning ? "دور سوم — از هر دو دروازه عبور کن" : "دو سایه آماده‌اند — به خروج برس"
            };
        canvas.DrawText(status, WorldWidth / 2f, 108f, _paint);

        _paint.Color = Color.Rgb(35, 45, 69);
        canvas.DrawRoundRect(_restartButton, 22f, 22f, _paint);
        _paint.TextSize = 24f;
        _paint.Color = Color.Rgb(210, 220, 242);
        canvas.DrawText("از نو", _restartButton.CenterX(), 80f, _paint);

        var progress = _loopRunning ? _loopElapsed / LoopDuration : 0f;
        _paint.Color = Color.Rgb(35, 45, 69);
        canvas.DrawRoundRect(new RectF(100f, 137f, 980f, 171f), 17f, 17f, _paint);
        _paint.Color = _recordedPaths.Count switch
        {
            0 => Color.Rgb(69, 220, 255),
            1 => Color.Rgb(190, 111, 255),
            _ => Color.Rgb(255, 195, 89)
        };
        canvas.DrawRoundRect(new RectF(100f, 137f, 100f + 880f * progress, 171f), 17f, 17f, _paint);

        _paint.TextSize = 25f;
        _paint.Color = Color.Rgb(235, 241, 255);
        var seconds = _loopRunning ? Math.Ceiling(LoopDuration - _loopElapsed) : LoopDuration;
        canvas.DrawText($"{seconds:0}", WorldWidth / 2f, 164f, _paint);
    }

    private void DrawIntro(Canvas canvas)
    {
        _paint.Color = Color.Rgb(10, 13, 24);
        canvas.DrawRect(0f, 0f, WorldWidth, WorldHeight, _paint);
        _paint.TextAlign = Paint.Align.Center;
        _paint.SetTypeface(Typeface.Create(Typeface.Default, TypefaceStyle.Bold));
        _paint.TextSize = 64f;
        _paint.Color = Color.Rgb(95, 226, 190);
        canvas.DrawText("رد من", WorldWidth / 2f, 880f, _paint);
        _paint.SetTypeface(Typeface.Default);
        _paint.TextSize = 31f;
        _paint.Color = Color.Rgb(145, 167, 206);
        canvas.DrawText("گذشته‌ات راه آینده را می‌سازد", WorldWidth / 2f, 950f, _paint);
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

        DrawSwitchesAndDoors(canvas);

        DrawExitDoor(canvas);
        DrawExitGuidance(canvas);
    }

    private void DrawExitDoor(Canvas canvas)
    {
        var opening = _exitSequenceActive
            ? EaseInOut(_exitSequenceElapsed / 0.42f)
            : _levelComplete ? 1f : 0f;
        var pulse = 0.5f + 0.5f * MathF.Sin((float)_clock.Elapsed.TotalSeconds * 3.5f);

        _paint.Color = Color.Rgb(38, 82, 86);
        canvas.DrawRoundRect(_exitDoor, 34f, 34f, _paint);

        var inner = new RectF(_exitDoor.Left + 20f, _exitDoor.Top + 18f, _exitDoor.Right - 20f, _exitDoor.Bottom - 16f);
        _paint.Color = Color.Argb((int)(45 + opening * 150), 95, 226, 190);
        canvas.DrawRoundRect(inner, 24f, 24f, _paint);

        _paint.SetStyle(Paint.Style.Stroke);
        _paint.StrokeWidth = 8f;
        _paint.Color = Color.Rgb(95, 226, 190);
        canvas.DrawRoundRect(_exitDoor, 34f, 34f, _paint);
        _paint.SetStyle(Paint.Style.Fill);

        var halfPanel = inner.Width() / 2f - 8f;
        var panelWidth = halfPanel * (1f - opening);
        _paint.Color = Color.Rgb(22, 73, 76);
        canvas.DrawRoundRect(new RectF(inner.Left, inner.Top, inner.Left + panelWidth, inner.Bottom), 20f, 20f, _paint);
        canvas.DrawRoundRect(new RectF(inner.Right - panelWidth, inner.Top, inner.Right, inner.Bottom), 20f, 20f, _paint);

        _paint.Color = Color.Argb((int)(95 + 110 * pulse), 143, 255, 220);
        canvas.DrawCircle(WorldWidth / 2f, 258f, 11f + pulse * 4f, _paint);
        _paint.TextAlign = Paint.Align.Center;
        _paint.TextSize = 27f;
        _paint.Color = Color.Rgb(194, 255, 229);
        canvas.DrawText("در خروج", WorldWidth / 2f, 345f, _paint);
    }

    private void DrawExitGuidance(Canvas canvas)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin((float)_clock.Elapsed.TotalSeconds * 2.6f);
        _paint.Color = Color.Argb((int)(28 + 42 * pulse), 95, 226, 190);
        _paint.StrokeWidth = 7f;
        for (var y = 505f; y <= 745f; y += 100f)
        {
            canvas.DrawLine(495f, y, 540f, y - 35f, _paint);
            canvas.DrawLine(540f, y - 35f, 585f, y, _paint);
        }
    }

    private void DrawSwitchesAndDoors(Canvas canvas)
    {
        DrawSwitchAndDoor(
            canvas,
            RedSwitchX,
            RedSwitchY,
            _redDoor,
            _redSwitchActive,
            _redDoorOpen,
            Color.Rgb(255, 82, 105),
            Color.Rgb(91, 56, 70),
            Color.Rgb(255, 180, 190));

        DrawSwitchAndDoor(
            canvas,
            BlueSwitchX,
            BlueSwitchY,
            _blueDoor,
            _blueSwitchActive,
            _blueDoorOpen,
            Color.Rgb(75, 142, 255),
            Color.Rgb(48, 65, 105),
            Color.Rgb(163, 197, 255));
    }

    private void DrawSwitchAndDoor(
        Canvas canvas,
        float switchX,
        float switchY,
        RectF door,
        bool switchActive,
        bool doorOpen,
        Color activeColor,
        Color inactiveColor,
        Color highlightColor)
    {
        _paint.StrokeWidth = 7f;
        _paint.Color = switchActive
            ? Color.Argb(150, activeColor.R, activeColor.G, activeColor.B)
            : Color.Argb(55, activeColor.R, activeColor.G, activeColor.B);
        canvas.DrawLine(switchX, switchY, switchX, door.Bottom, _paint);
        canvas.DrawLine(switchX, door.Bottom, door.Left, door.Bottom, _paint);

        _paint.Color = switchActive ? activeColor : inactiveColor;
        canvas.DrawCircle(switchX, switchY, SwitchRadius, _paint);
        _paint.Color = switchActive ? highlightColor : Color.Rgb(105, 112, 137);
        canvas.DrawCircle(switchX, switchY, SwitchRadius - 18f, _paint);

        if (doorOpen)
        {
            _paint.Color = Color.Argb(65, activeColor.R, activeColor.G, activeColor.B);
            canvas.DrawRoundRect(door, 14f, 14f, _paint);
        }
        else
        {
            _paint.Color = activeColor;
            canvas.DrawRoundRect(door, 14f, 14f, _paint);
            _paint.Color = highlightColor;
            for (var x = door.Left + 28f; x < door.Right; x += 48f)
                canvas.DrawRect(x, door.Top + 5f, x + 12f, door.Bottom - 5f, _paint);
        }
    }

    private void DrawGhosts(Canvas canvas)
    {
        foreach (var ghost in _ghosts)
        {
            _paint.Color = ghost.ColorIndex == 0
                ? Color.Argb(135, 190, 111, 255)
                : Color.Argb(145, 255, 195, 89);
            canvas.DrawCircle(ghost.X, ghost.Y, PlayerRadius, _paint);

            _paint.Color = Color.Argb(185, 255, 255, 255);
            canvas.DrawCircle(ghost.X - 12f, ghost.Y - 7f, 5f, _paint);
            canvas.DrawCircle(ghost.X + 12f, ghost.Y - 7f, 5f, _paint);
        }
    }

    private void DrawVictoryParticles(Canvas canvas)
    {
        if (!_exitSequenceActive || _exitSequenceElapsed < 0.32f || _ghosts.Count == 0)
            return;

        var flow = (_exitSequenceElapsed - 0.32f) / 0.95f;
        for (var ghostIndex = 0; ghostIndex < _ghosts.Count; ghostIndex++)
        {
            var ghost = _ghosts[ghostIndex];
            for (var particleIndex = 0; particleIndex < 9; particleIndex++)
            {
                var sequenceIndex = ghostIndex * 9 + particleIndex;
                var delayed = Math.Clamp((flow - sequenceIndex * 0.035f) / 0.55f, 0f, 1f);
                var eased = EaseInOut(delayed);
                var startX = ghost.X + MathF.Sin(sequenceIndex * 2.1f) * 24f;
                var startY = ghost.Y + MathF.Cos(sequenceIndex * 1.7f) * 24f;
                var endX = WorldWidth / 2f + MathF.Sin(sequenceIndex * 1.1f) * 42f;
                var endY = 230f + MathF.Cos(sequenceIndex * 1.3f) * 36f;
                var alpha = (int)(190 * (1f - Math.Max(0f, delayed - 0.72f) / 0.28f));
                _paint.Color = ghostIndex == 0
                    ? Color.Argb(alpha, 190, 111, 255)
                    : Color.Argb(alpha, 255, 195, 89);
                canvas.DrawCircle(
                    startX + (endX - startX) * eased,
                    startY + (endY - startY) * eased,
                    5f + (1f - eased) * 5f,
                    _paint);
            }
        }
    }

    private void DrawPlayer(Canvas canvas)
    {
        var visibility = _exitSequenceActive
            ? 1f - EaseInOut((_exitSequenceElapsed - 1.05f) / 0.4f)
            : 1f;
        var alpha = (int)(255 * Math.Clamp(visibility, 0f, 1f));
        _paint.Color = Color.Argb(alpha, 69, 220, 255);
        canvas.DrawCircle(_playerX, _playerY, PlayerRadius, _paint);

        _paint.Color = Color.Argb(alpha, 255, 255, 255);
        canvas.DrawCircle(_playerX - 12f, _playerY - 7f, 5f, _paint);
        canvas.DrawCircle(_playerX + 12f, _playerY - 7f, 5f, _paint);
    }

    private void DrawJoystick(Canvas canvas, float scale, float offsetX, float offsetY)
    {
        if (!_touching || _levelComplete)
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

    private void DrawCompletionOverlay(Canvas canvas)
    {
        if (!_levelComplete)
            return;

        _paint.Color = Color.Argb(190, 7, 10, 20);
        canvas.DrawRoundRect(new RectF(120f, 610f, 960f, 1300f), 42f, 42f, _paint);
        _paint.TextAlign = Paint.Align.Center;
        _paint.SetTypeface(Typeface.Create(Typeface.Default, TypefaceStyle.Bold));
        _paint.TextSize = 54f;
        _paint.Color = Color.Rgb(95, 226, 190);
        canvas.DrawText("مرحله کامل شد", WorldWidth / 2f, 780f, _paint);

        _paint.SetTypeface(Typeface.Default);
        _paint.TextSize = 30f;
        _paint.Color = Color.Rgb(215, 225, 245);
        canvas.DrawText("دو سایه‌ات راه در خروج را ساختند", WorldWidth / 2f, 860f, _paint);

        _paint.Color = Color.Rgb(77, 88, 128);
        canvas.DrawRoundRect(_feedbackButton, 24f, 24f, _paint);
        _paint.TextSize = 31f;
        _paint.Color = Color.White;
        canvas.DrawText("ارسال نظر", WorldWidth / 2f, 1022f, _paint);

        _paint.Color = Color.Rgb(55, 118, 108);
        canvas.DrawRoundRect(_replayButton, 24f, 24f, _paint);
        _paint.TextSize = 31f;
        _paint.Color = Color.White;
        canvas.DrawText("تکرار مرحله", WorldWidth / 2f, 1122f, _paint);

        _paint.Color = Color.Rgb(42, 50, 68);
        canvas.DrawRoundRect(_nextLevelButton, 24f, 24f, _paint);
        _paint.TextSize = 29f;
        _paint.Color = Color.Rgb(150, 160, 180);
        canvas.DrawText("مرحله بعد — به‌زودی", WorldWidth / 2f, 1222f, _paint);
    }

    private void DrawTutorialOverlay(Canvas canvas)
    {
        if (!_tutorialVisible)
            return;

        _paint.Color = Color.Argb(224, 7, 10, 20);
        canvas.DrawRoundRect(new RectF(105f, 520f, 975f, 1260f), 44f, 44f, _paint);
        _paint.TextAlign = Paint.Align.Center;
        _paint.SetTypeface(Typeface.Create(Typeface.Default, TypefaceStyle.Bold));
        _paint.TextSize = 50f;
        _paint.Color = Color.Rgb(95, 226, 190);
        canvas.DrawText("چگونه بازی کنیم؟", WorldWidth / 2f, 650f, _paint);

        _paint.SetTypeface(Typeface.Default);
        _paint.TextSize = 31f;
        _paint.Color = Color.Rgb(226, 235, 250);
        canvas.DrawText("۱. دور اول روی کلید قرمز بمان.", WorldWidth / 2f, 740f, _paint);
        canvas.DrawText("۲. دور دوم از سایه اول کمک بگیر", WorldWidth / 2f, 810f, _paint);
        canvas.DrawText("و روی کلید آبی بمان.", WorldWidth / 2f, 860f, _paint);
        canvas.DrawText("۳. دور سوم با کمک هر دو سایه", WorldWidth / 2f, 930f, _paint);
        canvas.DrawText("از درها عبور کن و به خروج برس.", WorldWidth / 2f, 980f, _paint);

        _paint.Color = Color.Rgb(55, 118, 108);
        canvas.DrawRoundRect(new RectF(265f, 1035f, 815f, 1130f), 26f, 26f, _paint);
        _paint.TextSize = 34f;
        _paint.Color = Color.White;
        canvas.DrawText("برای شروع لمس کن", WorldWidth / 2f, 1098f, _paint);
    }

    private void DrawExitFlash(Canvas canvas)
    {
        if (!_exitSequenceActive)
            return;

        var flash = Math.Clamp((_exitSequenceElapsed - 1.05f) / 0.25f, 0f, 1f)
            * (1f - Math.Clamp((_exitSequenceElapsed - 1.30f) / 0.35f, 0f, 1f));
        if (flash <= 0f)
            return;

        _paint.Color = Color.Argb((int)(120 * flash), 128, 255, 215);
        canvas.DrawRect(0f, 0f, WorldWidth, WorldHeight, _paint);
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null)
            return false;

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                if (!_introComplete)
                    return true;

                var worldPoint = ScreenToWorld(e.GetX(), e.GetY());
                if (_tutorialVisible)
                {
                    DismissTutorial();
                    return true;
                }

                if (_restartButton.Contains(worldPoint.X, worldPoint.Y))
                {
                    ResetLevel();
                    return true;
                }

                if (_levelComplete)
                {
                    if (_feedbackButton.Contains(worldPoint.X, worldPoint.Y))
                    {
                        ShareFeedback();
                        return true;
                    }

                    if (_replayButton.Contains(worldPoint.X, worldPoint.Y))
                        ResetLevel();
                    return true;
                }

                if (_exitSequenceActive)
                    return true;

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

    private PointF ScreenToWorld(float screenX, float screenY)
    {
        var scale = Math.Min(Width / WorldWidth, Height / WorldHeight);
        if (scale <= 0f)
            return new PointF(-1f, -1f);

        var offsetX = (Width - WorldWidth * scale) / 2f;
        var offsetY = (Height - WorldHeight * scale) / 2f;
        return new PointF((screenX - offsetX) / scale, (screenY - offsetY) / scale);
    }

    private static float EaseInOut(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value * value * (3f - 2f * value);
    }

    private sealed class GhostPlayback(List<PathSample> path, int colorIndex)
    {
        public List<PathSample> Path { get; } = path;
        public int ColorIndex { get; } = colorIndex;
        public float X { get; set; } = StartX;
        public float Y { get; set; } = StartY;
        public int SampleIndex { get; set; }
    }

    private readonly record struct PathSample(float Time, float X, float Y);
}
