using Android.Content;
using CieloHud.App.Alerts;

namespace CieloHud.App.Platforms.Android.Alerts;

/// <summary>
/// Woken by the alert alarm, usually with the app closed: Android starts the process (and with it the MAUI services) just for this.
/// The work runs under <see cref="BroadcastReceiver.GoAsync"/> so the process is kept alive until it finishes.
/// </summary>
[BroadcastReceiver(Name = "com.cielohud.app.AlertReceiver", Exported = false)]
public sealed class AlertReceiver : BroadcastReceiver
{
    public const string AlertAction = "com.cielohud.app.ALERT";
    public const string TestAction = "com.cielohud.app.TEST_ALERT";

    private const string NotifyAtExtra = "notify_at";
    private const string VisibleStartExtra = "visible_start";
    private const string VisibleEndExtra = "visible_end";
    private const string TitleExtra = "title";
    private const string BodyExtra = "body";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (IPlatformApplication.Current?.Services.GetService<PassAlertService>() is not { } alerts || intent is null)
            return;

        var pending = GoAsync();
        Task.Run(async () =>
        {
            try
            {
                if (intent.Action == TestAction && ReadAlert(intent) is { } test)
                    alerts.OnTestAlarm(test);
                else if (intent.Action == AlertAction)
                    await alerts.OnAlarmAsync();
            }
            catch (Exception ex)
            {
                // Never crash a process the user did not open; the next app start plans again.
                global::Android.Util.Log.Error("CieloHud", $"Alert alarm failed: {ex}");
            }
            finally
            {
                pending?.Finish();
            }
        });
    }

    internal static void PutAlert(Intent intent, ScheduledAlert alert) => intent
        .PutExtra(NotifyAtExtra, alert.NotifyAt.ToUnixTimeMilliseconds())
        .PutExtra(VisibleStartExtra, alert.VisibleStart.ToUnixTimeMilliseconds())
        .PutExtra(VisibleEndExtra, alert.VisibleEnd.ToUnixTimeMilliseconds())
        .PutExtra(TitleExtra, alert.Title)
        .PutExtra(BodyExtra, alert.Body);

    private static ScheduledAlert? ReadAlert(Intent intent) =>
        intent.GetStringExtra(TitleExtra) is { } title && intent.GetStringExtra(BodyExtra) is { } body
            ? new ScheduledAlert(
                DateTimeOffset.FromUnixTimeMilliseconds(intent.GetLongExtra(NotifyAtExtra, 0)),
                DateTimeOffset.FromUnixTimeMilliseconds(intent.GetLongExtra(VisibleStartExtra, 0)),
                DateTimeOffset.FromUnixTimeMilliseconds(intent.GetLongExtra(VisibleEndExtra, 0)),
                false, title, body)
            : null;
}
