using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using System.Diagnostics;

namespace ShadowTraceGame;

public sealed class GameView : View
{
    private const float WorldWidth = 1080, WorldHeight = 1920;
    private readonly Puzzle game = new();
    private readonly Discoveries discoveries;
    private readonly Context context;
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Typeface bold = Typeface.Create(Typeface.Default, TypefaceStyle.Bold)!;
    private readonly Color mint = Color.Rgb(95, 226, 190), white = Color.Rgb(230, 237, 251);
    private readonly Color cyan = Color.Rgb(69, 220, 255), gold = Color.Rgb(255, 195, 89);
    private readonly Area restart = new(850, 25, 1020, 110), help = new(60, 25, 230, 110);
    private readonly Area grab = new(60, 1810, 340, 1895), pause = new(400, 1810, 680, 1895), rewrite = new(740, 1810, 1020, 1895);
    private readonly Area feedback = new(250, 1135, 830, 1220), replay = new(250, 1245, 830, 1330);
    private bool touching, tutorial, active = true, modal, startedVictory, newBadge;
    private int pointerId = -1;
    private float touchStartX, touchStartY, touchX, touchY, inputX, inputY, victoryTime;
    private long lastTick;
    private float exitStartX, exitStartY;

    public GameView(Context context) : base(context)
    {
        this.context = context;
        var prefs = context.GetSharedPreferences("shadow_trace", FileCreationMode.Private)!;
        tutorial = !prefs.GetBoolean("tutorial_three_routes_v1", false);
        discoveries = new Discoveries { Badges = prefs.GetInt("level1_badges", 0),
            Pauses = prefs.GetInt("pause_tokens", 0), Rewrites = prefs.GetInt("rewrite_tokens", 0) };
        KeepScreenOn = true;
        SetBackgroundColor(Color.Rgb(10, 13, 24));
        ContentDescription = "رد من؛ برای حرکت انگشت را بکش و برای راهنما دکمه راهنما را لمس کن";
    }

    public void PauseGame() { active = false; StopTouch(); lastTick = 0; }
    public void ResumeGame() { active = true; lastTick = 0; Invalidate(); }
    public override void OnWindowFocusChanged(bool hasWindowFocus)
    {
        base.OnWindowFocusChanged(hasWindowFocus);
        if (hasWindowFocus) ResumeGame(); else PauseGame();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        var stamp = clock.ElapsedTicks;
        var dt = lastTick == 0 ? 0 : Math.Min((float)(stamp-lastTick)/Stopwatch.Frequency, 0.1f);
        lastTick = stamp;
        if (active && !modal && !tutorial)
        {
            if (!game.Won)
            {
                var revision = game.RoundRevision;
                game.Advance(dt, inputX, inputY);
                if (revision != game.RoundRevision) StopTouch();
            }
            if (game.Won)
            {
                if (!startedVictory)
                {
                    startedVictory = true; StopTouch(); exitStartX = game.X; exitStartY = game.Y;
                    newBadge = discoveries.Award(game.Tier, game.Assisted);
                    SaveProgress();
                    PerformHapticFeedback(FeedbackConstants.LongPress);
                }
                victoryTime = Math.Min(1.65f, victoryTime + dt);
            }
        }
        var scale = Math.Min(Width / WorldWidth, Height / WorldHeight);
        canvas.Save();
        canvas.Translate((Width-WorldWidth*scale)/2, (Height-WorldHeight*scale)/2);
        canvas.Scale(scale, scale);
        DrawScene(canvas);
        if (touching) DrawJoystick(canvas);
        if (game.Won && victoryTime >= 1.65f) DrawResults(canvas);
        if (tutorial) DrawTutorial(canvas);
        canvas.Restore();
        if (active) PostInvalidateOnAnimation();
    }

    private void DrawScene(Canvas c)
    {
        Fill(c, new(0,0,1080,1920), Color.Rgb(10,13,24));
        Label(c,"دو دست، یک در",540,65,40,white,true);
        Button(c, help,"راهنما",Color.Rgb(35,45,69));
        Button(c, restart,"از نو",Color.Rgb(35,45,69));
        var status = game.Won ? "راه خروج را پیدا کردی" : game.FrozenFor > 0 ? "مکث زمان — حرکت آزاد است"
            : $"دور {game.Ghosts.Count + 1} • سایه‌ها: {game.Ghosts.Count} • سه روش پایان";
        Label(c,status,540,128,28,mint);
        Fill(c,new(100,152,980,175),Color.Rgb(35,45,69),11);
        if (game.Time > 0) Fill(c,new(100,152,100+880*game.Time/Puzzle.Duration,175),cyan,11);
        Label(c,$"{Math.Ceiling(Puzzle.Duration-game.Time):0} ثانیه",540,202,24,white);
        Fill(c,Puzzle.Room,Color.Rgb(18,25,43),24);
        for(var x=156f;x<1020;x+=96) Line(c,x,220,x,1770,Color.Argb(25,108,132,178),2);
        for(var y=310f;y<1770;y+=96) Line(c,65,y,1020,y,Color.Argb(25,108,132,178),2);
        foreach(var wall in Puzzle.Walls) Fill(c,wall,Color.Rgb(76,91,126),7);
        // Narrow crate passage is visible from the start, with distinct gold tracks.
        Line(c,220,960,220,1070,gold,3); Line(c,280,960,280,1070,gold,3);
        Label(c,"گذرگاه جعبه",270,955,22,gold);
        DrawMechanism(c,Puzzle.RedX,Puzzle.RedY,Puzzle.RedDoor,game.RedActive,game.RedOpen,Color.Rgb(255,82,105));
        DrawMechanism(c,Puzzle.BlueX,Puzzle.BlueY,Puzzle.BlueDoor,game.BlueActive,game.BlueOpen,Color.Rgb(75,142,255));
        foreach(var g in game.Ghosts)
        {
            var index=game.Ghosts.IndexOf(g);
            Character(c,g.X,g.Y,index==0 ? Color.Argb(140,190,111,255) : Color.Argb(155,255,195,89));
            Label(c,$"{index+1}",g.X,g.Y+65,23,white);
        }
        Fill(c,new(game.BoxX-30,game.BoxY-30,game.BoxX+30,game.BoxY+30),Color.Rgb(169,124,60),8);
        Line(c,game.BoxX-22,game.BoxY-22,game.BoxX+22,game.BoxY+22,gold,4);
        Line(c,game.BoxX+22,game.BoxY-22,game.BoxX-22,game.BoxY+22,gold,4);
        if(game.Holding) Line(c,game.X,game.Y,game.BoxX,game.BoxY,Color.Argb(180,255,195,89),3);
        DrawExit(c);
        var motion = Ease((victoryTime-0.3f)/0.85f);
        var px = game.Won ? exitStartX+(540-exitStartX)*motion : game.X;
        var py = game.Won ? exitStartY+(245-exitStartY)*motion : game.Y;
        var alpha = game.Won ? (int)(255*(1-Ease((victoryTime-1.05f)/0.4f))) : 255;
        Character(c,px,py,Color.Argb(alpha,69,220,255));
        if(game.Won) DrawVictory(c);
        var tip = game.Notice.Length>0 ? game.Notice : "جعبه را هل بده؛ با «گرفتن» می‌توانی آن را بکشی";
        Label(c,tip,540,1724,24,white);
        Button(c,grab,game.Holding ? "رها کردن جعبه" : "گرفتن جعبه",Color.Rgb(101,76,43));
        Button(c,pause,$"مکث زمان · {discoveries.Pauses}",discoveries.Pauses>0?Color.Rgb(41,89,98):Color.Rgb(35,45,59));
        Button(c,rewrite,$"بازنویسی · {discoveries.Rewrites}",discoveries.Rewrites>0?Color.Rgb(78,56,109):Color.Rgb(35,45,59));
        if(game.Assisted) Label(c,"این تلاش با کمک است؛ نشان‌ها در تلاش بدون کمک ثبت می‌شوند",540,1795,21,gold);
        else Label(c,"نسخه ۰.۷ • دو دست، یک در",540,1795,21,Color.Rgb(135,153,182));
    }

    private void DrawMechanism(Canvas c,float x,float y,Area gate,bool pressed,bool open,Color color)
    {
        Line(c,x,y,x,gate.Bottom,color,3); Line(c,x,gate.Bottom,gate.Left,gate.Bottom,color,3);
        Circle(c,x,y,59,Color.Argb(65,color.R,color.G,color.B));
        Circle(c,x,y,pressed?45:35,Color.Argb(pressed?255:120,color.R,color.G,color.B));
        if(open)
        {
            Fill(c,new(gate.Left,gate.Top,gate.Left+15,gate.Bottom),color,4);
            Fill(c,new(gate.Right-15,gate.Top,gate.Right,gate.Bottom),color,4);
        }
        else Fill(c,gate,color,8);
    }

    private void DrawExit(Canvas c)
    {
        var opening = game.Won ? Ease(victoryTime/0.3f) : 0;
        var pulse = (float)(0.5+0.5*Math.Sin(clock.Elapsed.TotalSeconds*2));
        Fill(c,new(415,260,665,315),Color.Rgb(24,63,62),9);
        Fill(c,new(420,270,540-115*opening,305),mint,5);
        Fill(c,new(540+115*opening,270,660,305),mint,5);
        Label(c,"در خروج",540,247,24,mint,true);
        for(int i=0;i<2;i++)
        {
            var y=405+i*70;
            var color=Color.Argb((int)(60+50*pulse),95,226,190);
            Line(c,510,y,540,y-23,color,5); Line(c,540,y-23,570,y,color,5);
        }
    }

    private void DrawVictory(Canvas c)
    {
        if(victoryTime<0.3f || victoryTime>=1.65f) return;
        for(var i=0;i<18;i++)
        {
            var g=game.Ghosts.Count>0 ? game.Ghosts[i%game.Ghosts.Count] : null;
            var sx=g?.X ?? game.BoxX; var sy=g?.Y ?? game.BoxY;
            var t=Ease((victoryTime-0.3f-i*0.025f)/0.8f);
            Circle(c,sx+(540-sx)*t+MathF.Sin(i*2)*25,sy+(275-sy)*t,5,Color.Argb((int)(180*(1-t)),255,195,89));
        }
        var flash=Math.Max(0,1-Math.Abs(victoryTime-1.28f)/0.22f);
        Fill(c,new(0,0,1080,1920),Color.Argb((int)(55*flash),128,255,215));
    }

    private void DrawResults(Canvas c)
    {
        Fill(c,new(0,0,1080,1920),Color.Argb(145,5,9,17));
        Fill(c,new(90,430,990,1440),Color.Rgb(16,25,43),32);
        Label(c,"مرحله کامل شد",540,515,48,mint,true);
        var names=new[]{"معمولی","هوشمندانه","استادانه"};
        Label(c,$"روش {names[game.Tier]} — {game.Ghosts.Count} سایه",540,577,32,gold);
        var reward=game.Assisted ? "پایان با کمک؛ برای نشان، بدون کمک تلاش کن"
            : !newBadge ? "این نشان قبلاً گرفته شده؛ جایزه تکرار نمی‌شود"
            : game.Tier==2 ? "نشان تازه + یک بازنویسی گذشته"
            : game.Tier==1 ? "نشان تازه + یک مکث زمان" : "اولین نشان پایان مرحله را گرفتی";
        Label(c,reward,540,635,26,white);
        Label(c,$"امتیاز کشف‌ها: {discoveries.Score} / ۸۵۰",540,690,30,mint);
        string[] hints=["راه آشنا: با دو سایه به خروج برس", "آیا هر کلید به یک سایه نیاز دارد؟", "آیا هنوز به دری که رد شده‌ای نیاز داری؟"];
        for(var i=0;i<3;i++)
        {
            var got=(discoveries.Badges & (1<<i))!=0;
            var y=767+i*104;
            Label(c,$"{(got?"◆":"◇")} {names[i]} · {new[]{100,250,500}[i]} امتیاز",540,y,30,got?gold:white);
            Label(c,hints[i],540,y+38,24,Color.Rgb(154,174,201));
        }
        Button(c,feedback,"ارسال نظر",Color.Rgb(77,88,128));
        Button(c,replay,"تلاش برای راه دیگر",Color.Rgb(55,118,108));
        Label(c,"مرحله بعد — به‌زودی",540,1388,26,Color.Rgb(126,140,163));
    }

    private void DrawTutorial(Canvas c)
    {
        Fill(c,new(0,0,1080,1920),Color.Argb(180,5,9,17));
        Fill(c,new(85,400,995,1460),Color.Rgb(16,25,43),32);
        Label(c,"یک مرحله، سه روش پایان",540,505,43,mint,true);
        string[] lines=["انگشتت را بکش تا حرکت کنی؛ رها کن تا بایستی.",
            "هر دور ۱۲ ثانیه است؛ مسیرت به سایه تبدیل می‌شود.",
            "روش معمول: قرمز، سپس آبی، سپس در خروج.",
            "جعبه را می‌توان هل داد و روی کلید گذاشت.",
            "کنارش «گرفتن جعبه» را بزن تا آن را بکشی.",
            "دوباره همان دکمه را بزن تا جعبه رها شود.",
            "با سایه کمتر تمام کن و نشان‌های دیگر را کشف کن.",
            "هر نشان فقط یک‌بار جایزه دارد؛ از نو همیشه رایگان است."];
        for(var i=0;i<lines.Length;i++) Label(c,lines[i],540,620+i*75,28,white);
        Button(c,new(245,1270,835,1380),"شروع کشف",Color.Rgb(55,118,108));
    }

    private void SaveProgress()
    {
        context.GetSharedPreferences("shadow_trace",FileCreationMode.Private)!.Edit()!
            .PutInt("level1_badges",discoveries.Badges)!.PutInt("pause_tokens",discoveries.Pauses)!
            .PutInt("rewrite_tokens",discoveries.Rewrites)!.Apply();
    }
    private void FreshRun() { game.Reset(); startedVictory=false; victoryTime=0; StopTouch(); }
    private void StopTouch() { touching=false; pointerId=-1; inputX=inputY=0; }
    private void Message(string text) => Toast.MakeText(context,text,ToastLength.Long)?.Show();

    private void ChooseRewrite()
    {
        if(discoveries.Rewrites<=0) { Message("نشان استادانه یک بازنویسی می‌دهد؛ «از نو» همیشه رایگان است."); return; }
        if(game.Ghosts.Count==0) { Message("هنوز سایه‌ای برای بازنویسی نداری."); return; }
        StopTouch(); modal=true;
        var dialog=new AlertDialog.Builder(context)!.SetTitle("کدام سایه بازنویسی شود؟")!
            .SetItems(Enumerable.Range(1,game.Ghosts.Count).Select(i=>$"سایه {i}").ToArray(),(_,e)=>ConfirmRewrite(e.Which))!
            .SetNegativeButton("انصراف",(_,_)=>{})!.Create()!;
        dialog.DismissEvent+=(_,_)=>{ modal=false; lastTick=0; };
        dialog.Show();
    }
    private void ConfirmRewrite(int index)
    {
        // Post until the selection dialog has finished dismissing.
        Post(()=>
        {
            modal=true;
            var d=new AlertDialog.Builder(context)!.SetTitle("یک هدیه مصرف شود؟")!
                .SetMessage("به ابتدای ضبط این سایه برمی‌گردی. سایه‌های قدیمی‌تر می‌مانند؛ این سایه و سایه‌های بعدی پاک می‌شوند. این تلاش با کمک ثبت می‌شود.")!
                .SetPositiveButton("بازنویسی",(_,_)=>
                {
                    if(discoveries.Rewrites>0 && game.Rewrite(index)) { discoveries.Rewrites--; SaveProgress(); }
                })!.SetNegativeButton("انصراف",(_,_)=>{})!.Create()!;
            d.DismissEvent+=(_,_)=>{modal=false;lastTick=0;}; d.Show();
        });
    }
    private void Share()
    {
        var text=$"رد من — ۰.۷\nنشان‌ها: {discoveries.Badges} | امتیاز: {discoveries.Score}\n\n"+
            "کدام راه‌ها را کشف کردی؟\nکار با جعبه راحت بود؟\nدوست داشتی برای کشف راه دیگر برگردی؟\nکجا گیر کردی؟";
        using var intent=new Intent(Intent.ActionSend);
        intent.SetType("text/plain"); intent.PutExtra(Intent.ExtraText,text);
        try { context.StartActivity(Intent.CreateChooser(intent,"ارسال نظر")); }
        catch(ActivityNotFoundException) { Message("برنامه‌ای برای اشتراک‌گذاری نصب نیست."); }
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if(e is null) return false;
        if(e.ActionMasked==MotionEventActions.Down)
        {
            var p=ToWorld(e.GetX(),e.GetY());
            if(tutorial)
            {
                if(new Area(245,1270,835,1380).Hits(p.X,p.Y,0))
                { tutorial=false; context.GetSharedPreferences("shadow_trace",FileCreationMode.Private)!.Edit()!.PutBoolean("tutorial_three_routes_v1",true)!.Apply(); }
                return true;
            }
            if(restart.Hits(p.X,p.Y,0)) { FreshRun(); return true; }
            if(help.Hits(p.X,p.Y,0)) { StopTouch(); tutorial=true; return true; }
            if(game.Won)
            {
                if(victoryTime>=1.65f)
                { if(feedback.Hits(p.X,p.Y,0)) Share(); else if(replay.Hits(p.X,p.Y,0)) FreshRun(); }
                return true;
            }
            if(grab.Hits(p.X,p.Y,0)) { game.ToggleHold(); return true; }
            if(pause.Hits(p.X,p.Y,0))
            {
                if(discoveries.Pauses<=0) Message("نشان هوشمندانه یک مکث زمان می‌دهد.");
                else if(game.Freeze()) { discoveries.Pauses--; SaveProgress(); }
                else Message("مکث زمان هنگام حرکت در یک دور قابل استفاده است.");
                return true;
            }
            if(rewrite.Hits(p.X,p.Y,0)) { ChooseRewrite(); return true; }
            touching=true;pointerId=e.GetPointerId(0);touchStartX=touchX=p.X;touchStartY=touchY=p.Y;
            return true;
        }
        if(e.ActionMasked==MotionEventActions.PointerDown)
        {
            // A second finger can use the crate or freeze button without losing movement.
            var p=ToWorld(e.GetX(e.ActionIndex),e.GetY(e.ActionIndex));
            if(!game.Won && !tutorial)
            {
                if(grab.Hits(p.X,p.Y,0)) game.ToggleHold();
                if(pause.Hits(p.X,p.Y,0) && discoveries.Pauses>0 && game.Freeze()) { discoveries.Pauses--;SaveProgress(); }
            }
            return true;
        }
        if(e.ActionMasked==MotionEventActions.Move && touching)
        {
            var index=e.FindPointerIndex(pointerId);
            if(index<0) {StopTouch();return true;}
            var p=ToWorld(e.GetX(index),e.GetY(index));touchX=p.X;touchY=p.Y;
            var dx=touchX-touchStartX;var dy=touchY-touchStartY;var distance=MathF.Sqrt(dx*dx+dy*dy);
            inputX=distance<14?0:dx/Math.Max(distance,118);inputY=distance<14?0:dy/Math.Max(distance,118);
            return true;
        }
        if(e.ActionMasked==MotionEventActions.Up || e.ActionMasked==MotionEventActions.Cancel ||
            e.ActionMasked==MotionEventActions.PointerUp && e.GetPointerId(e.ActionIndex)==pointerId) StopTouch();
        return true;
    }

    private (float X,float Y) ToWorld(float x,float y)
    {
        var s=Math.Min(Width/1080f,Height/1920f);
        return s<=0?(-1,-1):((x-(Width-1080*s)/2)/s,(y-(Height-1920*s)/2)/s);
    }
    private void DrawJoystick(Canvas c)
    {
        Circle(c,touchStartX,touchStartY,118,Color.Argb(35,255,255,255));
        Circle(c,touchStartX+inputX*118,touchStartY+inputY*118,40,Color.Argb(110,69,220,255));
    }
    private void Character(Canvas c,float x,float y,Color color)
    {
        Circle(c,x,y,38,color);Circle(c,x-12,y-7,5,Color.Argb(color.A,255,255,255));Circle(c,x+12,y-7,5,Color.Argb(color.A,255,255,255));
    }
    private void Fill(Canvas c,Area a,Color color,float radius=0)
    { paint.Color=color;paint.SetStyle(Paint.Style.Fill);c.DrawRoundRect(a.Left,a.Top,a.Right,a.Bottom,radius,radius,paint); }
    private void Circle(Canvas c,float x,float y,float r,Color color) {paint.Color=color;c.DrawCircle(x,y,r,paint);}
    private void Line(Canvas c,float x,float y,float xx,float yy,Color color,float width)
    {paint.Color=color;paint.StrokeWidth=width;c.DrawLine(x,y,xx,yy,paint);}
    private void Label(Canvas c,string text,float x,float y,float size,Color color,bool heavy=false)
    {
        paint.SetTypeface(heavy?bold:Typeface.Default);paint.TextAlign=Paint.Align.Center;paint.Color=color;paint.TextSize=size;
        // Keep Persian text inside its panel on narrow devices without clipping.
        var width=paint.MeasureText(text);if(width>890) paint.TextSize=size*890/width;
        c.DrawText(text,x,y,paint);
    }
    private void Button(Canvas c,Area a,string text,Color color)
    {Fill(c,a,color,18);Label(c,text,(a.Left+a.Right)/2,(a.Top+a.Bottom)/2+10,28,white);}
    private static float Ease(float t) {t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
}
