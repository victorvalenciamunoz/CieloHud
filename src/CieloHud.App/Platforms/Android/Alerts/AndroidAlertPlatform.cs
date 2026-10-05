using Android.App;
using Android.Content;
using AndroidX.Core.App;
using CieloHud.App.Alerts;
using AndroidUri = Android.Net.Uri;

namespace CieloHud.App.Platforms.Android.Alerts;

/// <summary>
/// Alerts on Android (decision 022): one <see cref="AlarmManager"/> alarm that wakes <see cref="AlertReceiver"/> even in Doze,
/// exact when the user allows it ("Alarmas y recordatorios", <c>SCHEDULE_EXACT_ALARM</c>), otherwise a few minutes loose;
/// notifications on their own channel, posted with <see cref="NotificationCompat"/>.
/// </summary>
public sealed class AndroidAlertPlatform : IAlertPlatform
{
    public const string ChannelId = "iss_passes";
    private const int AlertRequestCode = 1;
    private const int TestRequestCode = 2;
    private const int RefreshRequestCode = 3;

    private static Context Context => global::Android.App.Application.Context;

    private static AlarmManager Alarms => (AlarmManager)Context.GetSystemService(Context.AlarmService)!;

    private static NotificationManagerCompat Notifications => NotificationManagerCompat.From(Context)!;

    public bool NotificationsAllowed => Notifications.AreNotificationsEnabled();

    public async Task<bool> RequestNotificationsAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.PostNotifications>();
        return status == PermissionStatus.Granted && NotificationsAllowed;
    }

    // Before Android 12 every app could set exact alarms.
    public bool ExactAlarmsAllowed => !OperatingSystem.IsAndroidVersionAtLeast(31) || Alarms.CanScheduleExactAlarms();

    public void OpenExactAlarmSettings()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            return;
        var intent = new Intent(global::Android.Provider.Settings.ActionRequestScheduleExactAlarm, AndroidUri.Parse("package:" + Context.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        Context.StartActivity(intent);
    }

    public void ArmAlert(DateTimeOffset? at)
    {
        var operation = AlarmIntent(AlertReceiver.AlertAction, AlertRequestCode, null);
        if (at is { } time)
            Arm(time, operation);
        else
            Alarms.Cancel(operation);
    }

    // Planning does not need the exact minute (and ColorOS may add up to an hour anyway): always inexact.
    public void ArmRefresh(DateTimeOffset? at)
    {
        var operation = AlarmIntent(AlertReceiver.RefreshAction, RefreshRequestCode, null);
        if (at is { } time)
            Alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, time.ToUnixTimeMilliseconds(), operation);
        else
            Alarms.Cancel(operation);
    }

    public void ArmTest(DateTimeOffset wakeAt, ScheduledAlert alert) => Arm(wakeAt, AlarmIntent(AlertReceiver.TestAction, TestRequestCode, alert));

    public void Show(ScheduledAlert alert)
    {
        EnsureChannel();
        var now = DateTimeOffset.UtcNow;
        // The bindings return the builder as nullable; set one property per statement on the same instance.
        var builder = new NotificationCompat.Builder(Context, ChannelId);
        builder.SetSmallIcon(Resource.Drawable.ic_stat_iss);
        builder.SetColor(unchecked((int)0xFF7CFFB2));
        builder.SetContentTitle(alert.Title);
        builder.SetContentText(alert.Body);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(alert.Body));
        builder.SetCategory(NotificationCompat.CategoryEvent);
        builder.SetPriority(NotificationCompat.PriorityHigh);
        builder.SetContentIntent(OpenHudIntent(alert));
        builder.SetAutoCancel(true);
        builder.SetShowWhen(false);
        // Gone by itself once the pass is over.
        if (alert.VisibleEnd > now)
            builder.SetTimeoutAfter((long)(alert.VisibleEnd - now).TotalMilliseconds);

        Notifications.Notify(NotificationId(alert), builder.Build()!);
    }

    /// <summary>Alarms wake the receiver in Doze; exact ones only if the user allowed it, otherwise the system may shift them a little.</summary>
    private static void Arm(DateTimeOffset at, PendingIntent operation)
    {
        var millis = at.ToUnixTimeMilliseconds();
        if (!OperatingSystem.IsAndroidVersionAtLeast(31) || Alarms.CanScheduleExactAlarms())
            Alarms.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, millis, operation);
        else
            Alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, millis, operation);
    }

    private static PendingIntent AlarmIntent(string action, int requestCode, ScheduledAlert? alert)
    {
        var intent = new Intent(Context, typeof(AlertReceiver)).SetAction(action);
        if (alert is not null)
            AlertReceiver.PutAlert(intent, alert);
        return PendingIntent.GetBroadcast(Context, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    private static PendingIntent OpenHudIntent(ScheduledAlert alert)
    {
        var intent = new Intent(Context, typeof(MainActivity))
            .PutExtra(LaunchRequests.TargetExtra, "ISS")
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        return PendingIntent.GetActivity(Context, NotificationId(alert), intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    // One notification per pass: the same pass replaces its own (a test replaces the real one, which is fine).
    private static int NotificationId(ScheduledAlert alert) => (int)(alert.VisibleStart.ToUnixTimeSeconds() / 60 % int.MaxValue);

    private static void EnsureChannel()
    {
        var channel = new NotificationChannel(ChannelId, "Pasos de la ISS", NotificationImportance.High)
        {
            Description = "Aviso 10 minutos antes de cada paso visible de la ISS; de madrugada, la víspera a las 22:00.",
        };
        var manager = (NotificationManager)Context.GetSystemService(Context.NotificationService)!;
        manager.CreateNotificationChannel(channel);
    }
}
