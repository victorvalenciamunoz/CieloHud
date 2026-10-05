using CieloHud.Core.Sky;

namespace CieloHud.App.Services;

/// <summary>
/// The last place the app got a location fix, kept on the phone in <c>Preferences</c>. The alerts are planned in the
/// background, where reading the location would need the intrusive background-location permission; they use this instead.
/// </summary>
public sealed class ObserverStore
{
    private const string LatitudeKey = "observer_lat";
    private const string LongitudeKey = "observer_lon";
    private const string AltitudeKey = "observer_alt";

    private readonly IPreferences _preferences;

    public ObserverStore(IPreferences preferences) => _preferences = preferences;

    public Observer? Last =>
        _preferences.ContainsKey(LatitudeKey)
            ? new Observer(_preferences.Get(LatitudeKey, 0.0), _preferences.Get(LongitudeKey, 0.0), _preferences.Get(AltitudeKey, 0.0))
            : null;

    public void Save(Observer observer)
    {
        _preferences.Set(LatitudeKey, observer.LatitudeDegrees);
        _preferences.Set(LongitudeKey, observer.LongitudeDegrees);
        _preferences.Set(AltitudeKey, observer.AltitudeMeters);
    }
}
