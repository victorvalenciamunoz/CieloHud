using CieloHud.Core.Sky;

namespace CieloHud.App.Services;

/// <summary><see cref="ILocationSource"/> over MAUI's <see cref="Geolocation"/>.</summary>
public sealed class GeolocationSource : ILocationSource
{
    public async Task<LocationFix?> GetAsync(CancellationToken cancellationToken = default)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            return null;

        Location? location;
        try
        {
            location = await Geolocation.Default.GetLastKnownLocationAsync();
            // A fresh fix is worth a few seconds; the sky does not depend on metres but a stale fix may be another city.
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            location = await Geolocation.Default.GetLocationAsync(request, cancellationToken) ?? location;
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            return null;
        }

        if (location is null)
            return null;

        var observer = new Observer(location.Latitude, location.Longitude, location.Altitude ?? 0);
        return new LocationFix(observer, location.Accuracy, location.Timestamp);
    }
}
