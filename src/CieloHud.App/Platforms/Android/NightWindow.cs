using Android.Views;
using AndroidX.Core.View;

namespace CieloHud.App.Platforms.Android;

/// <summary>
/// Night mode on the activity window: hides the system bars (white clock, battery and navigation icons) and dims
/// this window only. The system brightness setting is never touched; Android drops the override when the app leaves.
/// </summary>
internal static class NightWindow
{
    /// <summary>
    /// The bottom of the brightness range, not a fixed percentage: whatever the user has set (or auto-brightness picks
    /// in the dark), this is never brighter. 0 is avoided because some devices read it as "screen off".
    /// </summary>
    private const float NightBrightness = 0.01f;

    public static void Apply(bool night)
    {
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window is not { } window)
            return;

        if (WindowCompat.GetInsetsController(window, window.DecorView) is { } bars)
        {
            if (night)
            {
                // A swipe from the edge shows them for a moment, then they hide again.
                bars.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
                bars.Hide(WindowInsetsCompat.Type.SystemBars());
            }
            else
            {
                bars.Show(WindowInsetsCompat.Type.SystemBars());
            }
        }

        var attributes = window.Attributes!;
        attributes.ScreenBrightness = night ? NightBrightness : WindowManagerLayoutParams.BrightnessOverrideNone;
        window.Attributes = attributes;
    }
}
