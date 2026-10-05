using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using CieloHud.App.Alerts;

namespace CieloHud.App;

// Portrait only: the orientation math assumes the top of the screen is the top of the device.
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Restored after being killed: the intent is the old one, already handled.
        if (savedInstanceState is null)
            HandleLaunch(Intent);
    }

    // Tapping an alert while the app is open.
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleLaunch(intent);
    }

    private static void HandleLaunch(Intent? intent)
    {
        if (intent?.GetStringExtra(LaunchRequests.TargetExtra) is { } target)
            LaunchRequests.Request(target);
    }
}
