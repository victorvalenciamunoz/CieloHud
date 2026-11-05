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
        // A new activity brings a new HUD, which takes the target when it appears.
        if (savedInstanceState is null && Target(Intent) is { } target)
            LaunchRequests.Keep(target);
    }

    // Tapping an alert while the app is open: the HUD is there, maybe under diagnostics.
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (Target(intent) is { } target)
            LaunchRequests.Request(target);
    }

    private static string? Target(Intent? intent) => intent?.GetStringExtra(LaunchRequests.TargetExtra);
}
