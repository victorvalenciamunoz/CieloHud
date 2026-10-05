using System.Text.Json;
using CieloHud.App.Services;
using CieloHud.Core.Alerts;
using CieloHud.Core.Passes;
using CieloHud.Core.Satellites;

namespace CieloHud.App.Alerts;

/// <summary>
/// ISS pass alerts. Off until the user turns them on (AVISOS button), remembered in <c>Preferences</c>.
/// <see cref="RescheduleAsync"/> plans the next days with Core (<see cref="PassAlertPlanner"/>) from the last known location and
/// the cached TLE, stores the alerts and arms one alarm at the first; <see cref="OnAlarmAsync"/> posts what is due and plans again.
/// The alarm and the notification are the platform's (<see cref="IAlertPlatform"/>).
/// </summary>
public sealed class PassAlertService
{
    private const int IssNoradNumber = 25544;
    private const string OnKey = "alerts_on";
    private const string PendingKey = "alerts_pending";
    private const string NotifiedKey = "alerts_notified";
    private const string PlannedAtKey = "alerts_planned_at";
    private const string ProblemKey = "alerts_problem";
    private const string LastWakeKey = "alerts_last_wake";

    /// <summary>The alarm may wake the app with no network; do not wait long for a fresh TLE, the cache will do.</summary>
    private static readonly TimeSpan TleTimeout = TimeSpan.FromSeconds(15);

    private readonly IAlertPlatform _platform;
    private readonly IPreferences _preferences;
    private readonly ObserverStore _observers;
    private readonly ITleProvider _tleProvider;
    private readonly IVisiblePassFinder _finder;
    private readonly PassAlertPlanner _planner = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PassAlertService(IAlertPlatform platform, IPreferences preferences, ObserverStore observers, ITleProvider tleProvider, IVisiblePassFinder finder)
    {
        _platform = platform;
        _preferences = preferences;
        _observers = observers;
        _tleProvider = tleProvider;
        _finder = finder;
    }

    public IAlertPlatform Platform => _platform;

    public bool IsOn => _preferences.Get(OnKey, false);

    /// <summary>How many days ahead each planning looks.</summary>
    public int PlanningDays => (int)_planner.Settings.Horizon.TotalDays;

    /// <summary>Planned alerts, in order. Empty when off or nothing visible in the next days.</summary>
    public IReadOnlyList<ScheduledAlert> Pending => Read(PendingKey, AlertJsonContext.Default.ListScheduledAlert);

    /// <summary>When the alerts were last planned (UTC), if ever.</summary>
    public DateTimeOffset? PlannedAt =>
        _preferences.Get(PlannedAtKey, (string?)null) is { } s ? Parse(s) : null;

    /// <summary>Why the last planning could not run, in words; null when it went fine.</summary>
    public string? Problem => _preferences.Get(ProblemKey, (string?)null);

    /// <summary>
    /// Turns the alerts on when the user taps AVISOS. Needs the notification permission; without it they stay off.
    /// Call on the main thread.
    /// </summary>
    public async Task<bool> TurnOnAsync()
    {
        if (!await _platform.RequestNotificationsAsync())
            return false;
        _preferences.Set(OnKey, true);
        await RescheduleAsync();
        return true;
    }

    public void TurnOff()
    {
        _preferences.Set(OnKey, false);
        _platform.ArmAlert(null);
        _preferences.Remove(PendingKey);
    }

    /// <summary>Plans the alerts again and arms the alarm at the first one. Safe to call often and from any thread.</summary>
    public async Task RescheduleAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await RescheduleCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The alarm went off. Before the first alert is due (ColorOS delivers alarms late, so they are armed early; see
    /// <see cref="AlarmSteps"/>), it only re-arms toward it. When due, it posts what is due, remembers it and plans again.
    /// </summary>
    public async Task OnAlarmAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var pending = Pending;
            if (IsOn && pending.Count > 0)
            {
                var target = pending[0].NotifyAt;
                RecordWake(now, target);
                if (!AlarmSteps.IsDue(now, target))
                {
                    _platform.ArmAlert(AlarmSteps.NextWakeUp(now, target));
                    return;
                }

                var notified = Read(NotifiedKey, AlertJsonContext.Default.ListDateTimeOffset);
                foreach (var alert in pending.Where(a => AlarmSteps.IsDue(now, a.NotifyAt) && a.VisibleStart > now))
                {
                    _platform.Show(alert);
                    notified.Add(alert.VisibleStart);
                }
                // Only recent ones matter: the planner never looks at passes that have already started.
                notified.RemoveAll(n => n < now - TimeSpan.FromDays(1));
                Write(NotifiedKey, notified, AlertJsonContext.Default.ListDateTimeOffset);
            }
            await RescheduleCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The last time the alarm went off and what it was aiming at (UTC), to see how late the system delivers it.</summary>
    public (DateTimeOffset Fired, DateTimeOffset Target)? LastWake =>
        _preferences.Get(LastWakeKey, (string?)null)?.Split('|') is [var fired, var target]
            ? (Parse(fired), Parse(target))
            : null;

    /// <summary>Posts the next planned alert (or an example) in <paramref name="delay"/>, through real alarms, marked as a test.</summary>
    public void ArmTest(TimeSpan delay)
    {
        var now = DateTimeOffset.UtcNow;
        var sample = Pending.FirstOrDefault()
            ?? new ScheduledAlert(now, now + TimeSpan.FromMinutes(10), now + TimeSpan.FromMinutes(15), false,
                "La ISS pasa en 10 min", "A las 21:43 pasa la ISS · 5 min · aparece por el NO, máximo 67° al SE");
        var at = now + delay;
        // Real visible times keep the alert alive until the pass ends; an example lives a quarter of an hour after posting.
        var visibleEnd = sample.VisibleEnd > at ? sample.VisibleEnd : at + TimeSpan.FromMinutes(15);
        var test = sample with { NotifyAt = at, VisibleEnd = visibleEnd, Title = "[PRUEBA] " + sample.Title };
        _platform.ArmTest(AlarmSteps.NextWakeUp(now, at), test);
    }

    /// <summary>The test alarm went off: the same steps as a real alert, without touching the planned ones.</summary>
    public void OnTestAlarm(ScheduledAlert test)
    {
        var now = DateTimeOffset.UtcNow;
        RecordWake(now, test.NotifyAt);
        if (AlarmSteps.IsDue(now, test.NotifyAt))
            _platform.Show(test);
        else
            _platform.ArmTest(AlarmSteps.NextWakeUp(now, test.NotifyAt), test);
    }

    private void RecordWake(DateTimeOffset fired, DateTimeOffset target) =>
        _preferences.Set(LastWakeKey, $"{Format(fired)}|{Format(target)}");

    private static string Format(DateTimeOffset instant) => instant.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string instant) => DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture);

    private async Task RescheduleCoreAsync()
    {
        if (!IsOn)
        {
            _platform.ArmAlert(null);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _preferences.Set(PlannedAtKey, Format(now));

        if (_observers.Last is not { } observer)
        {
            Fail("sin ubicación: abre el HUD con el GPS activo");
            return;
        }

        Tle tle;
        try
        {
            using var timeout = new CancellationTokenSource(TleTimeout);
            tle = await _tleProvider.GetTleAsync(IssNoradNumber, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TleUnavailableException or OperationCanceledException or IOException)
        {
            Fail("sin órbita de la ISS (sin red)");
            return;
        }

        var passes = _finder.Find(tle, observer, now, now + _planner.Settings.Horizon);
        var notified = Read(NotifiedKey, AlertJsonContext.Default.ListDateTimeOffset);
        var alerts = _planner.Plan(passes, tle.Epoch, now, TimeZoneInfo.Local, notified)
            .Select(a => ScheduledAlert.From(a, TimeZoneInfo.Local))
            .ToList();

        Write(PendingKey, alerts, AlertJsonContext.Default.ListScheduledAlert);
        _preferences.Remove(ProblemKey);
        // Early enough that the system delivering it late still lands on the alert; an alert already due goes off now.
        _platform.ArmAlert(alerts.Count > 0 ? AlarmSteps.NextWakeUp(now, alerts[0].NotifyAt) : null);
    }

    private void Fail(string problem)
    {
        _preferences.Set(ProblemKey, problem);
        _preferences.Remove(PendingKey);
        _platform.ArmAlert(null);
    }

    private List<T> Read<T>(string key, System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<T>> type)
    {
        if (_preferences.Get(key, (string?)null) is not { } json)
            return [];
        try
        {
            return JsonSerializer.Deserialize(json, type) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Write<T>(string key, List<T> value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<T>> type) =>
        _preferences.Set(key, JsonSerializer.Serialize(value, type));
}
