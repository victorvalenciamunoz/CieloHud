namespace CieloHud.App.Hud;

/// <summary>
/// Every color the app shows, by role rather than by hue, so a whole mode can be swapped at once.
/// <see cref="Normal"/> is the GincanaHud look; <see cref="Night"/> keeps dark adaptation: only dim reds on black,
/// with states told apart by intensity (and, in the drawing, by stroke width).
/// </summary>
public sealed record HudPalette(
    Color Background,
    Color Text,
    Color TextSoft,
    Color TextMuted,
    Color TextDim,
    Color Locked,
    Color LockedFill,
    Color Guide,
    Color Marker,
    Color MarkerCore,
    Color Alert,
    Color Star)
{
    public static HudPalette Normal { get; } = new(
        Background: Color.FromArgb("#0B1218"),
        Text: Color.FromArgb("#E8EEF4"),
        TextSoft: Color.FromArgb("#C5D0DB"),
        TextMuted: Color.FromArgb("#8FA0B0"),
        TextDim: Color.FromArgb("#5A6A7A"),
        Locked: Color.FromArgb("#7CFFB2"),
        LockedFill: Color.FromArgb("#1F6F4A"),
        Guide: Color.FromArgb("#4DD2FF"),
        Marker: Color.FromArgb("#FFC42E"),
        MarkerCore: Colors.White,
        Alert: Color.FromArgb("#FF5C5C"),
        Star: Color.FromArgb("#E8EEF4"));

    /// <summary>Pure black (an OLED pixel switched off) and four or five levels of red with almost no green or blue.</summary>
    public static HudPalette Night { get; } = new(
        Background: Color.FromArgb("#000000"),
        Text: Color.FromArgb("#C02818"),
        TextSoft: Color.FromArgb("#A02014"),
        TextMuted: Color.FromArgb("#801A10"),
        TextDim: Color.FromArgb("#50100A"),
        Locked: Color.FromArgb("#E8301C"),
        LockedFill: Color.FromArgb("#300806"),
        Guide: Color.FromArgb("#901C12"),
        Marker: Color.FromArgb("#B02416"),
        MarkerCore: Color.FromArgb("#E8301C"),
        Alert: Color.FromArgb("#E8301C"),
        Star: Color.FromArgb("#B02416"));
}
