using Android.App;
using Android.Content;
using AndroidX.Core.App;
using CieloHud.App.Alerts;
using AndroidUri = Android.Net.Uri;

namespace CieloHud.App.Platforms.Android.Alerts;

/// <summary>
/// Alerts on Android (decision 022): one <see cref="AlarmManager"/> alarm that wakes <see cref="AlertReceiver"/> even in Doze,
/// exact when the user allows it ("Alarmas y recordatorios", <c>SCHEDULE_EXACT_ALARM</c>), otherwise a few minutes loose;
/// notifications posted with <see cref="NotificationCompat"/>, on one channel for ISS passes and another for the Moon and the planets
/// (decision 027), so each can be tuned on its own in the system settings.
/// </summary>
public sealed class AndroidAlertPlatform : IAlertPlatform
{
    public const string PassChannelId = "iss_passes";
    public const string ConjunctionChannelId = "moon_planets";
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
        EnsureChannels();
        var now = DateTimeOffset.UtcNow;
        var isPass = alert.Kind == AlertKind.Pass;
        // The bindings return the builder as nullable; set one property per statement on the same instance.
        var builder = new NotificationCompat.Builder(Context, isPass ? PassChannelId : ConjunctionChannelId);
        builder.SetSmallIcon(isPass ? Resource.Drawable.ic_stat_iss : Resource.Drawable.ic_stat_moon);
        builder.SetColor(unchecked((int)0xFF7CFFB2));
        builder.SetContentTitle(alert.Title);
        builder.SetContentText(alert.Body);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(alert.Body));
        builder.SetCategory(NotificationCompat.CategoryEvent);
        // Below Android 8 the priority stands in for the channel importance.
        builder.SetPriority(isPass ? NotificationCompat.PriorityHigh : NotificationCompat.PriorityDefault);
        builder.SetContentIntent(OpenHudIntent(alert));
        builder.SetAutoCancel(true);
        builder.SetShowWhen(false);
        // Gone by itself once the pass, or the conjunction, is over.
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
            .PutExtra(LaunchRequests.TargetExtra, alert.Target)
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        return PendingIntent.GetActivity(Context, NotificationId(alert), intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    // One notification per pass or conjunction: the same one replaces its own (a test replaces the real one, which is fine).
    // Conjunctions count down from -1 so they never take the id of a pass that starts the same minute.
    private static int NotificationId(ScheduledAlert alert)
    {
        var minutes = (int)(alert.VisibleStart.ToUnixTimeSeconds() / 60 % int.MaxValue);
        return alert.Kind == AlertKind.Pass ? minutes : -1 - minutes;
    }

    private static void EnsureChannels()
    {
        var manager = (NotificationManager)Context.GetSystemService(Context.NotificationService)!;
        manager.CreateNotificationChannel(new NotificationChannel(PassChannelId, "Pasos de la ISS", NotificationImportance.High)
        {
            Description = "Aviso 10 minutos antes de cada paso visible de la ISS; de madrugada, la víspera a las 22:00.",
        });
        // Lasts hours and is announced ahead: it makes a sound, but no banner over what you are doing.
        manager.CreateNotificationChannel(new NotificationChannel(ConjunctionChannelId, "Luna y planetas", NotificationImportance.Default)
        {
            Description = "Aviso cuando la Luna pasa junto a Venus, Marte, Júpiter o Saturno: al anochecer, o la víspera a las 22:00 si es de madrugada.",
        });
    }
}
