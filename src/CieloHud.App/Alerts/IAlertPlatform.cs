namespace CieloHud.App.Alerts;

/// <summary>What the operating system does for the alerts: permissions, the alarm that wakes the app and the notification.</summary>
public interface IAlertPlatform
{
    /// <summary>True when the app may post notifications.</summary>
    bool NotificationsAllowed { get; }

    /// <summary>Asks for the notification permission if needed. Must be called on the main thread.</summary>
    Task<bool> RequestNotificationsAsync();

    /// <summary>True when alarms go off at the exact minute; otherwise the system may delay them a few minutes.</summary>
    bool ExactAlarmsAllowed { get; }

    /// <summary>Opens the system screen where the user allows exact alarms ("Alarmas y recordatorios").</summary>
    void OpenExactAlarmSettings();

    /// <summary>
    /// Arms the single alert alarm to wake the app at <paramref name="at"/>, replacing the previous one; null cancels it.
    /// The system may deliver it later (see <c>AlarmSteps</c>).
    /// </summary>
    void ArmAlert(DateTimeOffset? at);

    /// <summary>Arms the test alarm, which hands <paramref name="alert"/> back on waking. For trying the alerts without waiting for a pass.</summary>
    void ArmTest(DateTimeOffset wakeAt, ScheduledAlert alert);

    /// <summary>Posts the notification. Tapping it opens the HUD on the ISS.</summary>
    void Show(ScheduledAlert alert);
}
