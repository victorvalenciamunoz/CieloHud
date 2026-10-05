namespace CieloHud.App.Hud;

/// <summary>
/// Red night mode, remembered between sessions. Owns the active <see cref="HudPalette"/>: the drawable and chips read
/// <see cref="Palette"/>, and XAML reads the same colors as <c>DynamicResource</c> keys that <see cref="Apply"/> refreshes.
/// On Android it also hides the system bars and dims the window (see <c>NightWindow</c>).
/// </summary>
public sealed class NightMode
{
    private const string PreferenceKey = "night_mode";
    private const string BrightnessKey = "night_brightness";

    /// <summary>
    /// Window brightness in night mode by default (decision 019). 0.01, the bottom of the range, could not be read in a dark
    /// room on the OPPO: about the same as auto-brightness there (20 against 12 of its 2047), and red gives the eye little
    /// light. The user read it well at 0.10 (204 of 2047).
    /// </summary>
    public const float DefaultBrightness = 0.10f;

    /// <summary>The brightness choices offered in night mode: eyes, presbyopia and phones differ.</summary>
    public static IReadOnlyList<float> BrightnessChoices { get; } = [0.03f, 0.06f, 0.10f, 0.20f];

    private readonly IPreferences _preferences;

    public NightMode(IPreferences preferences)
    {
        _preferences = preferences;
        IsOn = preferences.Get(PreferenceKey, false);
    }

    public bool IsOn { get; private set; }

    public HudPalette Palette => IsOn ? HudPalette.Night : HudPalette.Normal;

    /// <summary>Brightness of this window in night mode, 0-1: the user's choice, remembered, or the default.</summary>
    public float Brightness => _preferences.Get(BrightnessKey, DefaultBrightness);

    /// <summary>Sets the night brightness (one of <see cref="BrightnessChoices"/>) and applies it at once.</summary>
    public void SetBrightness(float brightness)
    {
        _preferences.Set(BrightnessKey, brightness);
        ApplyToWindow();
    }

    public void Toggle()
    {
        IsOn = !IsOn;
        _preferences.Set(PreferenceKey, IsOn);
        Apply();
    }

    /// <summary>Publishes the palette as app resources, so pages using <c>DynamicResource</c> restyle on the spot.</summary>
    public void Apply()
    {
        if (Application.Current is not { } app)
            return;

        var p = Palette;
        var resources = app.Resources;
        resources["HudBackground"] = p.Background;
        resources["HudText"] = p.Text;
        resources["HudTextSoft"] = p.TextSoft;
        resources["HudTextMuted"] = p.TextMuted;
        resources["HudTextDim"] = p.TextDim;
        resources["HudLocked"] = p.Locked;
        resources["HudMarker"] = p.Marker;
        resources["HudAlert"] = p.Alert;

        ApplyToWindow();
    }

    /// <summary>System bars and screen brightness. Needs the window, so it is also called each time the app comes to the front.</summary>
    public void ApplyToWindow()
    {
#if ANDROID
        Platforms.Android.NightWindow.Apply(IsOn, Brightness);
#endif
    }
}
