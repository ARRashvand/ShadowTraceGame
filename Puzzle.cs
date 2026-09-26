namespace ShadowTraceGame;

// Platform-independent rules: the Android view and route tests use the same simulation.
public readonly record struct Area(float Left, float Top, float Right, float Bottom)
{
    public bool Hits(float x, float y, float half) =>
        x + half > Left && x - half < Right && y + half > Top && y - half < Bottom;
}

public readonly record struct Frame(float Time, float X, float Y, float BoxX, float BoxY, bool MovedBox);
public sealed class Replay(List<Frame> frames)
{
    public List<Frame> Frames { get; } = frames;
    public int Index { get; set; }
    public float X { get; set; } = Puzzle.StartX;
    public float Y { get; set; } = Puzzle.StartY;
}

public sealed class Puzzle
{
    public const float StartX = 540, StartY = 1630, Radius = 38, BoxHalf = 30;
    public const float RedX = 250, RedY = 1100, BlueY = 780;
    public int Level { get; }
    public float BlueX => Level == 1 ? 250 : 830;
    public string Title => Level == 1 ? "دو دست، یک در" : "حافظهٔ کوتاه";
    public float BlueMemory { get; private set; }
    public const float MemoryDuration = 1.6f;
    public const float Duration = 12, Speed = 510, Step = 1f / 120;
    public static readonly Area Room = new(60, 215, 1020, 1770);
    public static readonly Area RedDoor = new(420, 985, 660, 1035);
    public static readonly Area BlueDoor = new(420, 615, 660, 665);
    // The 70-unit chute admits a 60-unit crate, but never the 76-unit player.
    public Area[] Walls { get; }
    private static readonly Area[] FirstWalls = [new(60, 995, 215, 1025), new(285, 995, 420, 1025),
        new(660, 995, 1020, 1025), new(60, 625, 420, 655), new(660, 625, 1020, 655)];
    public List<Replay> Ghosts { get; } = [];
    private readonly List<Frame> recording = [];
    public float X { get; private set; } = StartX;
    public float Y { get; private set; } = StartY;
    public float BoxX { get; private set; } = 250;
    public float BoxY { get; private set; } = 1280;
    public float Time { get; private set; }
    public float FrozenFor { get; private set; }
    public bool Running { get; private set; }
    public bool Won { get; private set; }
    public bool Assisted { get; private set; }
    public bool Holding { get; private set; }
    public bool RedActive { get; private set; }
    public bool BlueActive { get; private set; }
    public bool RedOpen { get; private set; }
    public bool BlueOpen { get; private set; }
    public int RoundRevision { get; private set; }
    public int Tier => Ghosts.Count == 0 ? 2 : Ghosts.Count == 1 ? 1 : 0;
    public string Notice { get; private set; } = "";

    public Puzzle(int level = 1)
    {
        if (level is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(level));
        Level = level;
        Walls = level == 1 ? FirstWalls : [new(60,995,420,1025), new(660,995,1020,1025),
            new(60,625,420,655), new(660,625,1020,655)];
        ResetRound();
    }

    public void Reset()
    {
        Ghosts.Clear();
        Assisted = false;
        ResetRound();
        Notice = "";
    }

    private void ResetRound()
    {
        X = StartX; Y = StartY; BoxX = 250; BoxY = 1280;
        Time = FrozenFor = BlueMemory = 0;
        Running = Won = Holding = false;
        recording.Clear();
        recording.Add(new(0, X, Y, BoxX, BoxY, false));
        foreach (var ghost in Ghosts) { ghost.Index = 0; ghost.X = StartX; ghost.Y = StartY; }
        RedOpen = BlueOpen = false;
        Mechanisms();
        RoundRevision++;
    }

    public bool ToggleHold()
    {
        if (Won) return false;
        if (Holding) { Holding = false; Notice = "جعبه رها شد"; return true; }
        if (Distance(X, Y, BoxX, BoxY) > 180) { Notice = "برای گرفتن، به جعبه نزدیک‌تر شو"; return false; }
        // A tether may cross the small crate chute, but cannot reach through solid walls.
        for (var t = 0f; t <= 1; t += 0.03f)
            if (Walls.Any(w => w.Hits(X + (BoxX - X) * t, Y + (BoxY - Y) * t, 1)))
            { Notice = "از سمت گذرگاه به جعبه نزدیک شو"; return false; }
        Holding = true; Notice = "جعبه همراه توست؛ دوباره لمس کن تا رها شود";
        return true;
    }

    public bool Freeze()
    {
        if (!Running || Won || FrozenFor > 0) return false;
        FrozenFor = 3; Assisted = true; Notice = "زمان و سایه‌ها سه ثانیه متوقف شدند";
        return true;
    }

    public bool Rewrite(int index)
    {
        if (Won || index < 0 || index >= Ghosts.Count) return false;
        Ghosts.RemoveRange(index, Ghosts.Count - index);
        Assisted = true;
        ResetRound();
        Notice = "این سایه را دوباره بساز؛ سایه‌های قدیمی‌تر حفظ شدند";
        return true;
    }

    public void Advance(float seconds, float inputX, float inputY)
    {
        if (Won || seconds <= 0) return;
        var magnitude = MathF.Sqrt(inputX * inputX + inputY * inputY);
        if (magnitude > 1) { inputX /= magnitude; inputY /= magnitude; }
        if (!Running && magnitude <= 0.01f) return;
        Running = true;
        // Small physics steps prevent tunnelling through narrow walls at low frame rates.
        while (seconds > 0.000001f && Running && !Won)
        {
            var dt = Math.Min(seconds, Step);
            var frozen = FrozenFor > 0;
            if (frozen) dt = Math.Min(dt, FrozenFor);
            else dt = Math.Min(dt, Duration - Time);
            seconds -= dt;
            if (!frozen)
            {
                Time += dt;
                BlueMemory = Math.Max(0, BlueMemory - dt);
                ReplayGhosts();
            }
            var beforeX = BoxX; var beforeY = BoxY;
            Mechanisms();
            MoveAxis(inputX * Speed * dt, true);
            MoveAxis(inputY * Speed * dt, false);
            if (Holding) PullBox(Speed * dt);
            Mechanisms();
            var movedBox = Math.Abs(BoxX - beforeX) + Math.Abs(BoxY - beforeY) > 0.0001f;
            if (Ghosts.Count < 2 && !frozen)
                recording.Add(new(Time, X, Y, BoxX, BoxY, movedBox));
            if (frozen) FrozenFor = Math.Max(0, FrozenFor - dt);
            // Success depends on reaching the exit, never on a compulsory ghost count.
            if (X >= 468 && X <= 612 && Y <= 340)
            { Won = true; Running = Holding = false; return; }
            if (Time >= Duration - 0.00001f)
            {
                if (Ghosts.Count < 2)
                {
                    recording.Add(new(Duration, X, Y, BoxX, BoxY, false));
                    Ghosts.Add(new Replay([.. recording]));
                }
                ResetRound();
                Notice = "دور بعد آماده است؛ برای شروع دوباره حرکت کن";
            }
        }
    }

    private void ReplayGhosts()
    {
        // Only actual crate movements are replayed. A released crate is shared, not pinned
        // to an old recording. Later ghosts have deterministic priority on simultaneous moves.
        foreach (var ghost in Ghosts)
        {
            while (ghost.Index + 1 < ghost.Frames.Count && ghost.Frames[ghost.Index + 1].Time <= Time)
            {
                var frame = ghost.Frames[++ghost.Index];
                if (frame.MovedBox) { BoxX = frame.BoxX; BoxY = frame.BoxY; }
            }
            var a = ghost.Frames[ghost.Index];
            var b = ghost.Frames[Math.Min(ghost.Index + 1, ghost.Frames.Count - 1)];
            var blend = Math.Clamp((Time - a.Time) / Math.Max(b.Time - a.Time, 0.00001f), 0, 1);
            ghost.X = a.X + (b.X - a.X) * blend;
            ghost.Y = a.Y + (b.Y - a.Y) * blend;
        }
    }

    private void Mechanisms()
    {
        RedActive = OnSwitch(RedX, RedY);
        BlueActive = OnSwitch(BlueX, BlueY);
        if (Level == 2 && BlueActive) BlueMemory = MemoryDuration;
        RedOpen = RedActive || RedOpen && (RedDoor.Hits(X, Y, Radius) || RedDoor.Hits(BoxX, BoxY, BoxHalf));
        BlueOpen = BlueActive || BlueMemory > 0 || BlueOpen && (BlueDoor.Hits(X, Y, Radius) || BlueDoor.Hits(BoxX, BoxY, BoxHalf));
    }

    private bool OnSwitch(float x, float y) => Distance(X, Y, x, y) <= 54
        || Distance(BoxX, BoxY, x, y) <= 38
        || Ghosts.Any(g => Distance(g.X, g.Y, x, y) <= 54);

    private bool Clear(float x, float y, float half) =>
        x - half >= Room.Left && x + half <= Room.Right && y - half >= Room.Top && y + half <= Room.Bottom
        && !Walls.Any(w => w.Hits(x, y, half))
        && (RedOpen || !RedDoor.Hits(x, y, half)) && (BlueOpen || !BlueDoor.Hits(x, y, half));

    private void MoveAxis(float delta, bool horizontal)
    {
        if (Math.Abs(delta) < 0.00001f) return;
        var x = X + (horizontal ? delta : 0); var y = Y + (horizontal ? 0 : delta);
        if (!Clear(x, y, Radius)) return;
        if (Math.Abs(x - BoxX) < Radius + BoxHalf && Math.Abs(y - BoxY) < Radius + BoxHalf)
        {
            var bx = horizontal ? x + Math.Sign(delta) * (Radius + BoxHalf) : BoxX;
            var by = horizontal ? BoxY : y + Math.Sign(delta) * (Radius + BoxHalf);
            if (!Clear(bx, by, BoxHalf)) return;
            BoxX = bx; BoxY = by;
        }
        X = x; Y = y;
    }

    private void PullBox(float maximum)
    {
        var dx = X - BoxX; var dy = Y - BoxY;
        var span = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (span <= Radius + BoxHalf + 2) return;
        var fraction = Math.Min(maximum / Math.Max(Distance(X, Y, BoxX, BoxY), 1),
            1 - (Radius + BoxHalf + 2) / span);
        var x = BoxX + dx * fraction; var y = BoxY + dy * fraction;
        if (Clear(x, BoxY, BoxHalf)) BoxX = x;
        if (Clear(BoxX, y, BoxHalf)) BoxY = y;
        if (Distance(X, Y, BoxX, BoxY) > 240) { Holding = false; Notice = "جعبه پشت مانع ماند؛ دوباره نزدیک شو"; }
    }

    public static float Distance(float ax, float ay, float bx, float by) => MathF.Sqrt((ax-bx)*(ax-bx)+(ay-by)*(ay-by));
}

public sealed class Discoveries
{
    public int Badges { get; set; }
    public int Pauses { get; set; }
    public int Rewrites { get; set; }
    public int Score => ((Badges & 1) != 0 ? 100 : 0) + ((Badges & 2) != 0 ? 250 : 0) + ((Badges & 4) != 0 ? 500 : 0);
    public bool Award(int tier, bool assisted)
    {
        if (assisted || tier is < 0 or > 2 || (Badges & (1 << tier)) != 0) return false;
        Badges |= 1 << tier;
        if (tier == 1) Pauses++;
        if (tier == 2) Rewrites++;
        return true;
    }
}
