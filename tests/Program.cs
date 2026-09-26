using ShadowTraceGame;

void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
void Go(Puzzle p, float x, float y, float hz = 60)
{
    var revision = p.RoundRevision;
    for (var i = 0; i < hz * 10 && !p.Won; i++)
    {
        var dx = x - p.X; var dy = y - p.Y;
        if (Math.Abs(dx) < 0.4 && Math.Abs(dy) < 0.4) return;
        var max = Puzzle.Speed / hz;
        p.Advance(1 / hz, Math.Clamp(dx / max, -1, 1), Math.Clamp(dy / max, -1, 1));
        if (p.RoundRevision != revision) throw new Exception($"Timeout going to {x},{y}");
    }
    if (!p.Won) throw new Exception($"Blocked going to {x},{y}: player {p.X},{p.Y} box {p.BoxX},{p.BoxY}");
}
void WaitRound(Puzzle p)
{
    var revision = p.RoundRevision;
    for (var i = 0; i < 800 && p.RoundRevision == revision; i++) p.Advance(1f / 60, 0, 0);
    Check(p.RoundRevision != revision, "loop ends");
}
void RedRecording(Puzzle p)
{
    Go(p, 540, 1110); Go(p, 250, 1110); WaitRound(p);
}
void PassRed(Puzzle p, float hz = 60)
{
    Go(p, 540, 1100, hz);
    for (var i = 0; i < 240 && !p.RedOpen; i++) p.Advance(1f / 60, 0, 0);
    Go(p, 540, 900, hz);
}
void Finish(Puzzle p, float hz = 60)
{
    Go(p, 540, 780, hz);
    for (var i = 0; i < 300 && !p.BlueOpen; i++) p.Advance(1f / 60, 0, 0);
    Go(p, 540, 320, hz);
}
void PlaceRedCrate(Puzzle p, float hz = 60)
{
    Go(p, 250, 1500, hz); Go(p, 250, 1168, hz);
    Check(p.RedActive && Math.Abs(p.BoxY - 1100) < 2, "crate holds red switch");
}

foreach (var hz in new[] { 30f, 60f, 120f })
{
    var normal = new Puzzle();
    RedRecording(normal);
    PassRed(normal, hz); Go(normal, 250, 780, hz); WaitRound(normal);
    PassRed(normal, hz); Finish(normal, hz);
    Check(normal.Won && normal.Tier == 0, $"normal route at {hz} fps");

    var smart = new Puzzle();
    PlaceRedCrate(smart, hz); Go(smart, 540, 1168, hz); Go(smart, 540, 900, hz);
    Go(smart, 250, 780, hz); WaitRound(smart);
    PassRed(smart, hz); Finish(smart, hz);
    Check(smart.Won && smart.Tier == 1, $"one-ghost crate replay route at {hz} fps");

    var master = new Puzzle();
    PlaceRedCrate(master, hz); Go(master, 540, 1168, hz); Go(master, 540, 940, hz);
    Go(master, 250, 940, hz);
    Check(master.ToggleHold(), "grab crate through chute");
    Go(master, 250, 710, hz);
    master.Advance(0.4f, 0, 0);
    Check(master.ToggleHold(), "release crate");
    Check(master.BlueActive, "pulled crate holds blue switch");
    Go(master, 540, 710, hz); Finish(master, hz);
    Check(master.Won && master.Tier == 2 && master.Time < 12, $"zero-ghost master route at {hz} fps ({master.Time:F2}s)");
}

var blocked = new Puzzle();
Go(blocked, 540, 1100); blocked.Advance(0.4f, 0, -1);
Check(blocked.Y >= Puzzle.RedDoor.Bottom + Puzzle.Radius - 0.01f, "closed red door blocks player");
Go(blocked, 250, 1100); blocked.Advance(0.5f, 0, -1);
Check(blocked.Y >= 1025 + Puzzle.Radius - 0.01f, "crate chute blocks player");

var rewrite = new Puzzle(); RedRecording(rewrite); PassRed(rewrite); Go(rewrite, 250, 780); WaitRound(rewrite);
var first = rewrite.Ghosts[0].Frames;
Check(rewrite.Rewrite(1), "rewrite second ghost accepted");
Check(rewrite.Ghosts.Count == 1 && ReferenceEquals(first, rewrite.Ghosts[0].Frames)
    && rewrite.Time == 0 && rewrite.BoxY == 1280 && rewrite.Assisted, "rewrite keeps earlier recording and resets environment");
PassRed(rewrite); Go(rewrite, 250, 780); WaitRound(rewrite);
Check(rewrite.Rewrite(0) && rewrite.Ghosts.Count == 0, "rewriting first removes dependent ghosts");
rewrite.Reset();
Check(!rewrite.Assisted && rewrite.Ghosts.Count == 0 && !rewrite.RedOpen && !rewrite.BlueOpen, "free restart fully resets run");

var frozen = new Puzzle(); RedRecording(frozen); PassRed(frozen);
var time = frozen.Time; var gx = frozen.Ghosts[0].X; var gy = frozen.Ghosts[0].Y; var xBefore = frozen.X;
Check(frozen.Freeze(), "freeze starts during run");
frozen.Advance(1, 1, 0);
Check(frozen.Time == time && frozen.Ghosts[0].X == gx && frozen.Ghosts[0].Y == gy && frozen.RedOpen
    && frozen.X > xBefore && frozen.Assisted, "freeze holds time, ghosts and switch while player moves");
frozen.Advance(2.1f, 0, 0);
Check(frozen.Time > time && frozen.FrozenFor == 0, "freeze expires");

var rewards = new Discoveries();
Check(rewards.Award(0, false) && rewards.Score == 100, "normal badge");
Check(rewards.Award(1, false) && rewards.Pauses == 1, "smart reward");
Check(!rewards.Award(1, false) && rewards.Pauses == 1, "no repeat reward farming");
Check(!rewards.Award(2, true) && rewards.Rewrites == 0, "assisted win earns no unassisted badge");
Check(rewards.Award(2, false) && rewards.Rewrites == 1 && rewards.Score == 850, "master reward and collection score");
foreach(var hz in new[]{30f,60f,120f})
{
    var normal = new Puzzle(2);
    RedRecording(normal); PassRed(normal,hz); Go(normal,830,780,hz); WaitRound(normal);
    PassRed(normal,hz); Finish(normal,hz);
    Check(normal.Won && normal.Tier==0,$"level 2 normal at {hz} fps");

    var clever = new Puzzle(2);
    RedRecording(clever); PassRed(clever,hz); Go(clever,830,780,hz);
    Go(clever,540,780,hz); Finish(clever,hz);
    Check(clever.Won && clever.Tier==1,$"level 2 memory shortcut at {hz} fps");

    var master = new Puzzle(2);
    PlaceRedCrate(master,hz); Go(master,540,1168,hz); Go(master,540,900,hz);
    Go(master,830,780,hz); Go(master,540,780,hz); Finish(master,hz);
    Check(master.Won && master.Tier==2,$"level 2 crate and memory at {hz} fps ({master.Time:F2}s)");
}
var memory = new Puzzle(2);
PlaceRedCrate(memory); Go(memory,540,1168); Go(memory,540,900); Go(memory,830,780);
Go(memory,540,780);
Check(!memory.BlueActive && memory.BlueOpen && memory.BlueMemory>0,"memory holds gate after leaving switch");
var remaining=memory.BlueMemory;
Check(memory.Freeze(),"memory freeze starts"); memory.Advance(1,0,0);
Check(memory.BlueMemory==remaining,"assistance freezes memory countdown too");
memory.Advance(2+remaining+0.1f,0,0);
Check(!memory.BlueOpen && memory.BlueMemory==0,"memory expires and gate closes");
memory.Reset();
Check(memory.BlueMemory==0 && !memory.BlueOpen,"reset clears memory");
Go(memory,540,1100); Go(memory,250,1100); memory.Advance(0.5f,0,-1);
Check(memory.Y>=1063-0.01f,"level 2 has no crate chute bypass");
var levelTwoRewards=new Discoveries();
Check(levelTwoRewards.Award(2,false) && levelTwoRewards.Score==500 && rewards.Score==850,
    "independent level discoveries");
Console.WriteLine("All route and rule checks passed.");
