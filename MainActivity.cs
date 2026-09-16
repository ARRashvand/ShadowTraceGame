using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Graphics;
using Android.Widget;

namespace ShadowTraceGame;

[Activity(
    Label = "رد من",
    MainLauncher = true,
    Icon = "@drawable/ic_shadow_trace",
    ScreenOrientation = ScreenOrientation.Portrait,
    Theme = "@android:style/Theme.Material.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode)]
public sealed class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        try
        {
            SetContentView(new GameView(this));
        }
        catch (Exception exception)
        {
            var errorView = new TextView(this)
            {
                Text = $"STARTUP ERROR\n\n{exception.GetType().Name}\n{exception.Message}",
                TextSize = 18f,
                Gravity = Android.Views.GravityFlags.Center
            };
            errorView.SetTextColor(Color.Rgb(255, 110, 110));
            errorView.SetBackgroundColor(Color.Rgb(10, 13, 24));
            SetContentView(errorView);
        }
    }
}
