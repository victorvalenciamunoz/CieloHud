using Android.Views;
using AndroidX.Core.View;

namespace CieloHud.App.Platforms.Android;

/// <summary>
/// Night mode on the activity window: hides the system bars (white clock, battery and navigation icons) and dims
/// this window only. The system brightness setting is never touched; Android drops the override when the app leaves.
/// </summary>
internal static class NightWindow
{
    /// <param name="brightness">Window brightness in night mode, 0-1 (a fraction of the panel range, applied to this window only).</param>
    public static void Apply(bool night, float brightness)
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
        attributes.ScreenBrightness = night ? brightness : WindowManagerLayoutParams.BrightnessOverrideNone;
        window.Attributes = attributes;
    }
}
