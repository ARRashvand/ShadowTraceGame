using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using System.Diagnostics;

namespace ShadowTraceGame;

public sealed partial class GameView : View
{
    private const float WorldWidth = 1080, WorldHeight = 1920;
    private Puzzle game;
    private bool level2Unlocked;
    private readonly Discoveries discoveries;
    private readonly Context context;
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Typeface bold = Typeface.Create(Typeface.Default, TypefaceStyle.Bold)!;
    private readonly Color mint = Color.Rgb(95, 226, 190), white = Color.Rgb(230, 237, 251);
    private readonly Color cyan = Color.Rgb(69, 220, 255), gold = Color.Rgb(255, 195, 89);
    private readonly Area restart = new(850, 25, 1020, 110), help = new(60, 25, 230, 110);
    private readonly Area grab = new(350, 1810, 730, 1900), pause = new(60, 1810, 320, 1900), rewrite = new(760, 1810, 1020, 1900);
    private readonly Area feedback = new(350, 1350, 730, 1420), replay = new(190, 1135, 890, 1220);
    private readonly Area levels = new(250,25,830,110), next = new(190,1245,890,1330);
    private bool touching, tutorial, active = true, modal, startedVictory, newBadge;
    private int pointerId = -1;
    private float touchStartX, touchStartY, touchX, touchY, inputX, inputY, victoryTime;
    private long lastTick;
    private float exitStartX, exitStartY;

    public GameView(Context context) : base(context)
    {
        this.context = context;
        var prefs = context.GetSharedPreferences("shadow_trace", FileCreationMode.Private)!;
        level2Unlocked = prefs.GetBoolean("level2_unlocked", false) || prefs.GetInt("level1_badges",0) != 0;
        var selected = Math.Clamp(prefs.GetInt("selected_level",1),1,2);
        game = new Puzzle(selected == 2 && level2Unlocked ? 2 : 1);
        tutorial = !prefs.GetBoolean("tutorial_three_routes_v1", false);
        discoveries = new Discoveries { Badges = prefs.GetInt($"level{game.Level}_badges", 0),
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
            UpdateVisuals(dt);
            if (!game.Won)
            {
                var revision = game.RoundRevision;
                game.Advance(dt, inputX, inputY);
                if (revision != game.RoundRevision) { StopTouch(); echoTime=0.85f; echoPath=[.. livePath]; livePath.Clear(); }
            }
            if (game.Won)
            {
                if (!startedVictory)
                {
                    startedVictory = true; StopTouch(); exitStartX = game.X; exitStartY = game.Y;
                    newBadge = discoveries.Award(game.Tier, game.Assisted);
                    if(game.Level == 1) level2Unlocked = true;
                    SaveProgress();
                    PerformHapticFeedback(FeedbackConstants.LongPress);
                }
                victoryTime = Math.Min(3.2f, victoryTime + dt);
            }
        }
        var scale = Math.Min(Width / WorldWidth, Height / WorldHeight);
        canvas.Save();
        canvas.Translate((Width-WorldWidth*scale)/2, (Height-WorldHeight*scale)/2);
        canvas.Scale(scale, scale);
        DrawLab(canvas);
        if (touching) DrawJoystick(canvas);
        if (game.Won && victoryTime >= 1.65f) DrawResults(canvas);
        if (tutorial) DrawTutorial(canvas);
        canvas.Restore();
        if (active) PostInvalidateOnAnimation();
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
        if(victoryTime>=1.95f) Label(c,reward,540,635,26,white);
        Label(c,$"کشف‌های این مرحله: {discoveries.Score} / ۸۵۰",540,690,30,mint);
        string[] hints=game.Level == 1
            ? ["راه آشنا: با دو سایه به خروج برس", "آیا هر کلید به یک سایه نیاز دارد؟", "آیا هنوز به دری که رد شده‌ای نیاز داری؟"]
            : ["راه آشنا: با دو سایه به خروج برس", "بعد از ترک آبی، فرصت کوتاهی داری", "چه چیزی می‌تواند جای آخرین سایه را بگیرد؟"];
        for(var i=0;i<3;i++)
        {
            if(victoryTime < 2.1f+i*0.15f) continue;
            var got=(discoveries.Badges & (1<<i))!=0;
            var y=767+i*104;
            Fill(c,new(155,y-37,925,y+54),got?Color.Rgb(43,43,45):Color.Rgb(23,33,49),15);
            Label(c,$"{(got?"◆":"◇")} {names[i]} · {new[]{100,250,500}[i]} امتیاز",540,y,30,got?gold:white);
            Label(c,hints[i],540,y+38,24,Color.Rgb(154,174,201));
        }
        Button(c,feedback,"ارسال نظر",Color.Rgb(29,39,55));
        Button(c,replay,"کشف راه دیگر",Color.Rgb(42,105,96));
        Button(c,next,game.Level == 1 ? "مرحله دوم: حافظهٔ کوتاه" : "انتخاب مرحله",Color.Rgb(35,65,92));
    }

    private void DrawTutorial(Canvas c)
    {
        Fill(c,new(0,0,1080,1920),Color.Argb(180,5,9,17));
        Fill(c,new(85,400,995,1460),Color.Rgb(16,25,43),32);
        Label(c,$"{game.Title} · سه روش پایان",540,505,43,mint,true);
        string[] lines=["انگشتت را بکش تا حرکت کنی؛ رها کن تا بایستی.",
            "هر دور ۱۲ ثانیه است؛ مسیرت به سایه تبدیل می‌شود.",
            "روش معمول: قرمز، سپس آبی، سپس در خروج.",
            "جعبه را می‌توان هل داد و روی کلید گذاشت.",
            "کنارش «گرفتن جعبه» را بزن تا آن را بکشی.",
            "دوباره همان دکمه را بزن تا جعبه رها شود.",
            "با سایه کمتر تمام کن و نشان‌های دیگر را کشف کن.",
            "هر نشان فقط یک‌بار جایزه دارد؛ از نو همیشه رایگان است."];
        if(game.Level == 2)
        {
            lines[2] = "کلید آبی بعد از رها شدن، ۱٫۶ ثانیه یادش می‌ماند.";
            lines[5] = "حلقه دور کلید آبی، فرصت باقی‌مانده را نشان می‌دهد.";
        }
        for(var i=0;i<lines.Length;i++) Label(c,lines[i],540,620+i*75,28,white);
        Button(c,new(245,1270,835,1380),"شروع کشف",Color.Rgb(55,118,108));
    }

    private void SaveProgress()
    {
        context.GetSharedPreferences("shadow_trace",FileCreationMode.Private)!.Edit()!
            .PutInt($"level{game.Level}_badges",discoveries.Badges)!.PutInt("pause_tokens",discoveries.Pauses)!
            .PutInt("selected_level",game.Level)!.PutBoolean("level2_unlocked",level2Unlocked)!
            .PutInt("rewrite_tokens",discoveries.Rewrites)!.Apply();
    }
    private void LoadLevel(int level)
    {
        if(level == 2 && !level2Unlocked) return;
        SaveProgress();
        game = new Puzzle(level);
        discoveries.Badges = context.GetSharedPreferences("shadow_trace",FileCreationMode.Private)!.GetInt($"level{level}_badges",0);
        startedVictory=false; victoryTime=0; newBadge=false; StopTouch(); lastTick=0; ResetVisuals();
        tutorial=true;
        SaveProgress();
    }
    private void ChooseLevel()
    {
        StopTouch(); modal=true;
        var d=new AlertDialog.Builder(context)!.SetTitle("انتخاب مرحله")!
            .SetItems(new[]{"۱ · دو دست، یک در",level2Unlocked ? "۲ · حافظهٔ کوتاه" : "۲ · پس از پایان مرحله اول باز می‌شود"},(_,e)=>
            {
                if(e.Which==1 && !level2Unlocked) { Message("اول به در خروج مرحله یک برس؛ هر روشی کافی است."); return; }
                var target=e.Which+1;
                if(!game.Running && game.Ghosts.Count==0 || game.Won) { LoadLevel(target); return; }
                Post(()=>ConfirmLevel(target));
            })!.SetNegativeButton("بازگشت",(_,_)=>{})!.Create()!;
        d.DismissEvent+=(_,_)=>{modal=false;lastTick=0;}; d.Show();
    }
    private void ConfirmLevel(int target)
    {
        modal=true;
        var d=new AlertDialog.Builder(context)!.SetTitle("تلاش فعلی پایان یابد؟")!
            .SetMessage("مسیرها و سایه‌های این تلاش پاک می‌شوند؛ نشان‌ها و هدیه‌ها باقی می‌مانند.")!
            .SetPositiveButton("انتخاب مرحله",(_,_)=>LoadLevel(target))!
            .SetNegativeButton("ادامه تلاش",(_,_)=>{})!.Create()!;
        d.DismissEvent+=(_,_)=>{modal=false;lastTick=0;}; d.Show();
    }
    private void FreshRun() { game.Reset(); startedVictory=false; victoryTime=0; StopTouch(); ResetVisuals(); }
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
        var text=$"رد من — ۰.۹ · مرحله {game.Level}\nنشان‌ها: {discoveries.Badges} | امتیاز: {discoveries.Score}\n\n"+
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
            if(levels.Hits(p.X,p.Y,0)) { ChooseLevel(); return true; }
            if(game.Won)
            {
                if(victoryTime>=1.65f)
                {
                    if(feedback.Hits(p.X,p.Y,0)) Share();
                    else if(replay.Hits(p.X,p.Y,0)) FreshRun();
                    else if(next.Hits(p.X,p.Y,0)) { if(game.Level==1) LoadLevel(2); else ChooseLevel(); }
                }
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
        var ghost=color.A<200;
        paint.Color=Color.Argb(color.A/4,0,0,0);c.DrawOval(x-34,y+24,x+34,y+45,paint);
        Circle(c,x,y,46,Color.Argb(color.A/12,color.R,color.G,color.B));
        Circle(c,x,y,38,Color.Argb(color.A,color.R/2,color.G/2,color.B/2));
        Circle(c,x,y-4,34,color);
        if(ghost) Ring(c,x,y-4,27,Color.Argb(color.A,225,235,255),2);
        var dx=ghost?0:inputX*7; var dy=ghost?0:inputY*7;
        Circle(c,x-12+dx,y-10+dy,5,Color.Argb(color.A,245,255,255));
        Circle(c,x+12+dx,y-10+dy,5,Color.Argb(color.A,245,255,255));
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
    {
        Fill(c,new(a.Left,a.Top+5,a.Right,a.Bottom+5),Color.Argb(65,0,0,0),20);
        Fill(c,a,color,20);
        Line(c,a.Left+22,a.Top+2,a.Right-22,a.Top+2,Color.Argb(32,225,245,255),2);
        paint.SetTypeface(bold); paint.TextSize=28;
        var size=Math.Min(28,28*(a.Right-a.Left-28)/Math.Max(1,paint.MeasureText(text)));
        Label(c,text,(a.Left+a.Right)/2,(a.Top+a.Bottom)/2+size*0.35f,size,white,true);
    }
    private static float Ease(float t) {t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
}
