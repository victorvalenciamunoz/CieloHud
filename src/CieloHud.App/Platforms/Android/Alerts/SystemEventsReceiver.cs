using Android.App;
using Android.Content;
using CieloHud.App.Alerts;

namespace CieloHud.App.Platforms.Android.Alerts;

/// <summary>
/// System events that call for planning the alerts again, with the app closed:
/// <list type="bullet">
/// <item>reboot and app update, which wipe the app's alarms;</item>
/// <item>exact alarms just allowed in "Alarmas y recordatorios";</item>
/// <item>a new time zone (travel), which changes the local times in the text and the quiet hours.</item>
/// </list>
/// Exported because the system sends these; it only plans again, whatever the intent carries.
/// </summary>
[BroadcastReceiver(Name = "com.cielohud.app.SystemEventsReceiver", Exported = true)]
[IntentFilter([
    Intent.ActionBootCompleted,
    Intent.ActionMyPackageReplaced,
    ExactAlarmPermissionChanged,
    Intent.ActionTimezoneChanged,
])]
public sealed class SystemEventsReceiver : BroadcastReceiver
{
    /// <summary><c>AlarmManager.ACTION_SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED</c> (API 31); older versions never send it.</summary>
    private const string ExactAlarmPermissionChanged = "android.app.action.SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED";

    /// <summary>Each event and how the diagnostics history names it.</summary>
    private static readonly Dictionary<string, string> Reasons = new()
    {
        [Intent.ActionBootCompleted] = "tras reiniciar",
        [Intent.ActionMyPackageReplaced] = "tras actualizar la app",
        [ExactAlarmPermissionChanged] = "alarmas exactas permitidas",
        [Intent.ActionTimezoneChanged] = "cambio de zona horaria",
    };

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action is not { } action || !Reasons.TryGetValue(action, out var reason))
            return;
        if (IPlatformApplication.Current?.Services.GetService<PassAlertService>() is not { } alerts || !alerts.IsOn)
            return;

        var pending = GoAsync();
        Task.Run(async () =>
        {
            try
            {
                // A new time zone: .NET caches the local one per process.
                if (action == Intent.ActionTimezoneChanged)
                    TimeZoneInfo.ClearCachedData();
                await alerts.RescheduleAsync(reason);
                global::Android.Util.Log.Info("CieloHud", $"Alerts planned again after {action}");
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("CieloHud", $"Planning after {action} failed: {ex}");
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}
