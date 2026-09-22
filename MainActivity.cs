using Android.App;
using Android.Content.PM;
using Android.OS;

namespace ShadowTraceGame;

[Activity(Label = "رد من", MainLauncher = true, Icon = "@drawable/ic_shadow_trace",
    ScreenOrientation = ScreenOrientation.Portrait, Theme = "@android:style/Theme.Material.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : Activity
{
    private GameView? gameView;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        gameView = new GameView(this);
        SetContentView(gameView);
    }
    protected override void OnPause() { gameView?.PauseGame(); base.OnPause(); }
    protected override void OnResume() { base.OnResume(); gameView?.ResumeGame(); }
}
