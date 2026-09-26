using Android.Graphics;

namespace ShadowTraceGame;

// Presentation only: collision geometry, recordings and timers remain in Puzzle.
public sealed partial class GameView
{
    private float visualTime, redSlide, blueSlide, echoTime, noticeAge, pathTick;
    private string previousNotice="";
    private readonly List<(float X,float Y)> livePath=[];
    private List<(float X,float Y)> echoPath=[];

    private void ResetVisuals()
    {
        redSlide=blueSlide=echoTime=pathTick=0;
        livePath.Clear(); echoPath.Clear(); previousNotice=""; noticeAge=0;
    }
    private void UpdateVisuals(float dt)
    {
        visualTime+=dt; echoTime=Math.Max(0,echoTime-dt);
        // Closing is immediate, so a visibly clear gate never conceals a closed collider.
        redSlide=game.RedOpen?Math.Min(1,redSlide+dt*10):0;
        blueSlide=game.BlueOpen?Math.Min(1,blueSlide+dt*10):0;
        if(previousNotice!=game.Notice) { previousNotice=game.Notice;noticeAge=0; }
        else noticeAge+=dt;
        pathTick+=dt;
        if(game.Running && !game.Won && pathTick>=0.12f)
        {
            pathTick=0;livePath.Add((game.X,game.Y));
            if(livePath.Count>110) livePath.RemoveAt(0);
        }
    }
    private void Ring(Canvas c,float x,float y,float radius,Color color,float width,float fraction=1)
    {
        paint.Color=color;paint.StrokeWidth=width;paint.SetStyle(Paint.Style.Stroke);
        c.DrawArc(x-radius,y-radius,x+radius,y+radius,-90,360*Math.Clamp(fraction,0,1),false,paint);
        paint.SetStyle(Paint.Style.Fill);
    }
    private void DrawLab(Canvas c)
    {
        Fill(c,new(0,0,1080,1920),Color.Rgb(10,16,25));
        Button(c,levels,$"مراحل · {game.Level}: {game.Title}",Color.Rgb(24,38,51));
        Button(c,help,"راهنما",Color.Rgb(18,28,40));
        Button(c,restart,"از نو",Color.Rgb(18,28,40));
        Label(c,game.Won?"راه خروج را پیدا کردی":$"دور {game.Ghosts.Count+1}  /  سایه‌ها {game.Ghosts.Count}",540,143,27,mint);
        Fill(c,new(80,174,780,182),Color.Rgb(32,45,59),4);
        var fraction=Math.Clamp(1-game.Time/Puzzle.Duration,0,1);
        if(fraction>0) Fill(c,new(80,174,80+700*fraction,182),game.FrozenFor>0?gold:cyan,4);
        Label(c,$"{Math.Ceiling(Puzzle.Duration-game.Time):0} ثانیه",897,186,27,white);

        Fill(c,new(53,218,1027,1780),Color.Rgb(5,10,17),28);
        Fill(c,Puzzle.Room,Color.Rgb(29,41,53),22);
        // Low-contrast floor slabs rather than a graph-paper grid.
        Fill(c,new(70,225,1010,615),Color.Rgb(23,39,47),14);
        Fill(c,new(70,665,1010,985),Color.Rgb(24,35,48),12);
        Fill(c,new(70,1035,1010,1760),Color.Rgb(22,32,44),14);
        for(var y=350f;y<1740;y+=170)
        {
            Line(c,85,y,995,y,Color.Argb(12,180,208,226),2);
            for(var x=180f;x<990;x+=240) Line(c,x,y-100,x,y-86,Color.Argb(20,180,208,226),2);
        }
        Line(c,66,245,66,1740,Color.Rgb(49,67,78),3);
        Line(c,1014,245,1014,1740,Color.Rgb(49,67,78),3);
        foreach(var wall in game.Walls)
        {
            Fill(c,new(wall.Left,wall.Top+10,wall.Right,wall.Bottom+13),Color.Argb(100,0,0,0),5);
            Fill(c,wall,Color.Rgb(66,82,96),5);
            Line(c,wall.Left+5,wall.Top+3,wall.Right-5,wall.Top+3,Color.Rgb(117,139,151),3);
            Line(c,wall.Left+5,wall.Bottom-2,wall.Right-5,wall.Bottom-2,Color.Rgb(38,52,67),4);
        }
        if(game.Level==1)
        {
            Line(c,219,980,219,1050,gold,3); Line(c,281,980,281,1050,gold,3);
            Label(c,"گذرگاه جعبه",250,955,21,gold);
        }
        LabMechanism(c,Puzzle.RedX,Puzzle.RedY,Puzzle.RedDoor,game.RedActive,game.RedOpen,redSlide,Color.Rgb(245,109,119),false);
        LabMechanism(c,game.BlueX,Puzzle.BlueY,Puzzle.BlueDoor,game.BlueActive,game.BlueOpen,blueSlide,Color.Rgb(99,169,247),true);
        if(game.Level==2)
        {
            Ring(c,game.BlueX,Puzzle.BlueY,70,Color.Rgb(42,65,88),5);
            if(game.BlueMemory>0) Ring(c,game.BlueX,Puzzle.BlueY,70,cyan,6,game.BlueMemory/Puzzle.MemoryDuration);
            Label(c,"حافظهٔ کوتاه",game.BlueX,885,22,Color.Rgb(156,183,205));
        }
        for(var gi=0;gi<game.Ghosts.Count;gi++)
        {
            var g=game.Ghosts[gi];var tint=gi==0?Color.Rgb(185,151,245):gold;
            for(var n=5;n>=1;n--)
            {
                var at=g.Index-n*10;
                if(at<0) continue;
                var f=g.Frames[at];
                Circle(c,f.X,f.Y,Math.Max(5,25-n*3),Color.Argb(55-n*8,tint.R,tint.G,tint.B));
            }
            Character(c,g.X,g.Y,Color.Argb(155,tint.R,tint.G,tint.B));
            Fill(c,new(g.X-19,g.Y+43,g.X+19,g.Y+74),Color.Rgb(31,38,52),10);
            Label(c,$"{gi+1}",g.X,g.Y+67,22,tint,true);
        }
        DrawCrate(c);
        // Exit beacon stays spatially and visually separate from puzzle gates.
        Fill(c,new(429,307,651,370),Color.Argb(17,95,226,190),18);
        Line(c,408,269,408,309,mint,5); Line(c,672,269,672,309,mint,5);
        DrawExit(c);
        var motion=Ease((victoryTime-0.3f)/0.85f);
        var px=game.Won?exitStartX+(540-exitStartX)*motion:game.X;
        var py=game.Won?exitStartY+(245-exitStartY)*motion:game.Y;
        var alpha=game.Won?(int)(255*(1-Ease((victoryTime-1.05f)/0.4f))):255;
        Character(c,px,py,Color.Argb(alpha,69,220,255));
        if(echoTime>0)
        {
            var t=Ease(1-echoTime/0.85f);
            for(var i=0;i<echoPath.Count;i+=3)
            {
                var p=echoPath[i];
                Circle(c,p.X+(Puzzle.StartX-p.X)*t,p.Y+(Puzzle.StartY-p.Y)*t,4,Color.Argb((int)(110*(1-t)),185,151,245));
            }
            Ring(c,Puzzle.StartX,Puzzle.StartY,42+(1-t)*55,Color.Argb((int)(180*(1-t)),185,151,245),3);
        }
        if(game.Won) DrawVictory(c);

        // Contextual messages live outside the playable room.
        var nearby=Puzzle.Distance(game.X,game.Y,game.BoxX,game.BoxY)<=180;
        var message=game.Notice.Length>0 && noticeAge<3.5f?game.Notice:
            game.FrozenFor>0?"زمان ایستاده؛ تو هنوز می‌توانی حرکت کنی":
            game.Assisted?"تلاش با کمک · نشان‌ها برای تلاش بدون کمک‌اند":
            !game.Running?"انگشتت را بکش؛ با گذشته‌ات راه خروج را بساز":"";
        if(message.Length>0) Label(c,message,540,1793,22,game.Assisted?gold:Color.Rgb(169,190,206));
        Button(c,grab,game.Holding?"رها کردن جعبه":nearby?"گرفتن جعبه":"نزدیک جعبه شو",nearby||game.Holding?Color.Rgb(125,88,46):Color.Rgb(64,54,43));
        Button(c,pause,$"Ⅱ  مکث · {discoveries.Pauses}",discoveries.Pauses>0?Color.Rgb(32,72,81):Color.Rgb(23,34,45));
        Button(c,rewrite,$"↶ بازنویسی · {discoveries.Rewrites}",discoveries.Rewrites>0?Color.Rgb(63,48,89):Color.Rgb(23,34,45));
    }
    private void DrawCrate(Canvas c)
    {
        var x=game.BoxX;var y=game.BoxY;
        Fill(c,new(x-32,y-19,x+34,y+40),Color.Argb(100,0,0,0),9);
        Fill(c,new(x-30,y-30,x+30,y+30),Color.Rgb(128,87,46),7);
        Fill(c,new(x-27,y-28,x+27,y+18),Color.Rgb(193,145,80),5);
        Line(c,x-22,y-21,x+22,y-21,Color.Rgb(241,203,137),3);
        Line(c,x-19,y-14,x+19,y+12,Color.Rgb(134,91,48),5);
        Line(c,x+19,y-14,x-19,y+12,Color.Rgb(134,91,48),5);
        if(Puzzle.Distance(game.X,game.Y,x,y)<=180)
        {
            paint.Color=game.Holding?mint:gold;paint.StrokeWidth=2;paint.SetStyle(Paint.Style.Stroke);
            c.DrawRoundRect(x-36,y-36,x+36,y+36,10,10,paint);paint.SetStyle(Paint.Style.Fill);
        }
        if(game.Holding)
        {
            Line(c,game.X,game.Y,x,y,Color.Argb(140,95,226,190),3);
            Circle(c,x,y,5,mint);
        }
    }
    private void LabMechanism(Canvas c,float x,float y,Area gate,bool pressed,bool open,float slide,Color tint,bool blue)
    {
        var edge=x>gate.Right?gate.Right:gate.Left;
        var track=Color.Argb(open?120:40,tint.R,tint.G,tint.B);
        Line(c,x,y,x,gate.Bottom+12,track,3);Line(c,x,gate.Bottom+12,edge,gate.Bottom+12,track,3);
        if(open)
        {
            var vertical=Math.Abs(y-gate.Bottom-12);var horizontal=Math.Abs(x-edge);
            var progress=(visualTime*210)%(vertical+horizontal);
            var tx=progress<vertical?x:x+Math.Sign(edge-x)*(progress-vertical);
            var ty=progress<vertical?y-progress:gate.Bottom+12;
            Circle(c,tx,ty,4,tint);
        }
        Circle(c,x,y+7,53,Color.Rgb(9,17,26));
        Circle(c,x,y,51,Color.Rgb(48,63,77));
        Ring(c,x,y,49,Color.Argb(110,tint.R,tint.G,tint.B),2);
        Circle(c,x,y+(pressed?4:-3),38,Color.Argb(pressed?220:95,tint.R,tint.G,tint.B));
        if(blue) { Line(c,x-12,y-10,x+12,y-10,white,3);Line(c,x-12,y+6,x+12,y+6,white,3); }
        else Ring(c,x,y-2,12,white,3);
        // Symbols supplement color on switches and gates.
        Fill(c,new(gate.Left,gate.Top+8,gate.Right,gate.Bottom+8),Color.Argb(80,0,0,0),6);
        var middle=(gate.Left+gate.Right)/2;
        var amount=Ease(slide)*(gate.Right-gate.Left-30)/2;
        Fill(c,new(gate.Left,gate.Top,middle-amount,gate.Bottom),tint,6);
        Fill(c,new(middle+amount,gate.Top,gate.Right,gate.Bottom),tint,6);
        if(!open)
        {
            if(blue) {Line(c,middle-12,gate.Top+18,middle+12,gate.Top+18,white,3);Line(c,middle-12,gate.Top+30,middle+12,gate.Top+30,white,3);}
            else Ring(c,middle,(gate.Top+gate.Bottom)/2,10,white,3);
        }
    }
}
