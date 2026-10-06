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

    /// <summary>
    /// Pure black (an OLED pixel switched off) and levels of red with almost no green or blue. Red gives the eye about a fifth
    /// of the light of white, so text and buttons use nearly full red: the first, dimmer palette (down to 31 %) could not be
    /// read in a dark room (decision 019). The darkness comes from the black background and the low window brightness.
    /// </summary>
    public static HudPalette Night { get; } = new(
        Background: Color.FromArgb("#000000"),
        Text: Color.FromArgb("#FF3020"),
        TextSoft: Color.FromArgb("#E62A1A"),
        TextMuted: Color.FromArgb("#C02418"),
        TextDim: Color.FromArgb("#8C1A10"),
        Locked: Color.FromArgb("#FF4A30"),
        LockedFill: Color.FromArgb("#4A0E08"),
        Guide: Color.FromArgb("#D02818"),
        Marker: Color.FromArgb("#F03020"),
        MarkerCore: Color.FromArgb("#FF5038"),
        Alert: Color.FromArgb("#FF4A30"),
        Star: Color.FromArgb("#D82A1C"));
}
