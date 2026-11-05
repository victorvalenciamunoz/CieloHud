using Android.Graphics.Drawables;
using Microsoft.Maui.Platform;

namespace CieloHud.App.Platforms.Android;

/// <summary>
/// The card's scroll bar, always visible while its content does not fit (decision 052): with HISTORIA at the end, even the grown
/// card often has more below, and nobody noticed a card could scroll (decision 041). Painted in the palette, red at night.
/// </summary>
internal static class CardScrollBar
{
    private const int WidthDp = 3;

    public static void Apply(ScrollView scroll, Color color)
    {
        if (scroll.Handler?.PlatformView is not global::Android.Views.View view)
            return;

        view.VerticalScrollBarEnabled = true;
        view.ScrollbarFadingEnabled = false;
        var width = (int)Math.Round(WidthDp * (view.Resources?.DisplayMetrics?.Density ?? 1));
        view.ScrollBarSize = width;
        // Before Android 10 the thumb keeps the system's grey.
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            var thumb = new GradientDrawable();
            thumb.SetColor(color.ToPlatform());
            thumb.SetCornerRadius(width / 2f);
            view.VerticalScrollbarThumbDrawable = thumb;
        }
    }
}
