namespace CieloHud.App.Hud;

/// <summary>
/// Red night mode, remembered between sessions. Owns the active <see cref="HudPalette"/>: the drawable and chips read
/// <see cref="Palette"/>, and XAML reads the same colors as <c>DynamicResource</c> keys that <see cref="Apply"/> refreshes.
/// </summary>
public sealed class NightMode
{
    private const string PreferenceKey = "night_mode";

    private readonly IPreferences _preferences;

    public NightMode(IPreferences preferences)
    {
        _preferences = preferences;
        IsOn = preferences.Get(PreferenceKey, false);
    }

    public bool IsOn { get; private set; }

    public HudPalette Palette => IsOn ? HudPalette.Night : HudPalette.Normal;

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
    }
}
