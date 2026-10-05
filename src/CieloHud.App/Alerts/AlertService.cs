using System.Text.Json;
using CieloHud.App.Services;
using CieloHud.Core.Alerts;
using CieloHud.Core.Conjunctions;
using CieloHud.Core.Passes;
using CieloHud.Core.Satellites;

namespace CieloHud.App.Alerts;

/// <summary>
/// Alerts of ISS passes and Moon-planet conjunctions. Off until the user turns them on (AVISOS button), remembered in <c>Preferences</c>.
/// <see cref="RescheduleAsync"/> plans the next days with Core (<see cref="PassAlertPlanner"/>, <see cref="ConjunctionAlertPlanner"/>)
/// from the last known location and, for the ISS, the cached TLE; it stores both kinds in one list ordered by time and arms one alarm
/// at the first; <see cref="OnAlarmAsync"/> posts what is due and plans again.
/// The alarm and the notification are the platform's (<see cref="IAlertPlatform"/>).
/// </summary>
public sealed class AlertService
{
    private const int IssNoradNumber = 25544;
    private const string OnKey = "alerts_on";
    private const string PendingKey = "alerts_pending";
    private const string NotifiedKey = "alerts_notified";
    private const string NotifiedConjunctionsKey = "alerts_notified_conjunctions";
    private const string HistoryKey = "alerts_history";
    private const int HistoryLength = 6;
    private const string ProblemKey = "alerts_problem";
    private const string LastWakeKey = "alerts_last_wake";
    private const string NextRefreshKey = "alerts_next_refresh";

    /// <summary>The alarm may wake the app with no network; do not wait long for a fresh TLE, the cache will do.</summary>
    private static readonly TimeSpan TleTimeout = TimeSpan.FromSeconds(15);

    private readonly IAlertPlatform _platform;
    private readonly IPreferences _preferences;
    private readonly ObserverStore _observers;
    private readonly ITleProvider _tleProvider;
    private readonly IVisiblePassFinder _finder;
    private readonly IConjunctionFinder _conjunctionFinder;
    private readonly PassAlertPlanner _planner = new();
    private readonly ConjunctionAlertPlanner _conjunctionPlanner = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AlertService(IAlertPlatform platform, IPreferences preferences, ObserverStore observers, ITleProvider tleProvider,
        IVisiblePassFinder finder, IConjunctionFinder conjunctionFinder)
    {
        _platform = platform;
        _preferences = preferences;
        _observers = observers;
        _tleProvider = tleProvider;
        _finder = finder;
        _conjunctionFinder = conjunctionFinder;
    }

    public IAlertPlatform Platform => _platform;

    public bool IsOn => _preferences.Get(OnKey, false);

    /// <summary>How many days ahead each planning looks.</summary>
    public int PlanningDays => (int)_planner.Settings.Horizon.TotalDays;

    /// <summary>Planned alerts of both kinds, in order. Empty when off or nothing to see in the next days.</summary>
    public IReadOnlyList<ScheduledAlert> Pending => Read(PendingKey, AlertJsonContext.Default.ListScheduledAlert);

    /// <summary>The last plannings, oldest first, with why they ran: shows that the background ones happen (reboot, daily…).</summary>
    public IReadOnlyList<PlanningRecord> History => Read(HistoryKey, AlertJsonContext.Default.ListPlanningRecord);

    /// <summary>When the alerts will be planned again with the app closed (UTC), if the alerts are on.</summary>
    public DateTimeOffset? NextRefresh =>
        _preferences.Get(NextRefreshKey, (string?)null) is { } s ? Parse(s) : null;

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
        await RescheduleAsync("al activar");
        return true;
    }

    public void TurnOff()
    {
        _preferences.Set(OnKey, false);
        _platform.ArmAlert(null);
        ArmRefresh(null);
        _preferences.Remove(PendingKey);
    }

    /// <summary>Plans the alerts again and arms the alarm at the first one. Safe to call often and from any thread.</summary>
    /// <param name="reason">Why, in words, for the history in diagnostics ("al abrir la app", "tras reiniciar"…).</param>
    public async Task RescheduleAsync(string reason)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await RescheduleCoreAsync(reason).ConfigureAwait(false);
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
                var notifiedConjunctions = Read(NotifiedConjunctionsKey, AlertJsonContext.Default.ListNotifiedConjunction);
                foreach (var alert in pending.Where(a => AlarmSteps.IsDue(now, a.NotifyAt) && a.WorthUntil > now))
                {
                    _platform.Show(alert);
                    if (alert.Kind == AlertKind.Pass)
                        notified.Add(alert.VisibleStart);
                    else
                        notifiedConjunctions.AddRange(alert.Conjunctions ?? []);
                }
                // Only recent ones matter: the planner never looks at passes that have already started, and a conjunction
                // is only the same one within a day of its best moment.
                notified.RemoveAll(n => n < now - TimeSpan.FromDays(1));
                notifiedConjunctions.RemoveAll(n => n.Best < now - TimeSpan.FromDays(2));
                Write(NotifiedKey, notified, AlertJsonContext.Default.ListDateTimeOffset);
                Write(NotifiedConjunctionsKey, notifiedConjunctions, AlertJsonContext.Default.ListNotifiedConjunction);
            }
            await RescheduleCoreAsync("tras un aviso").ConfigureAwait(false);
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

    /// <summary>
    /// Posts the next planned alert of that kind (or an example) in <paramref name="delay"/>, through real alarms, marked as a test.
    /// For a conjunction it looks up to two months ahead, so there is always a real one to try. Slow: call off the main thread.
    /// </summary>
    public void ArmTest(TimeSpan delay, AlertKind kind = AlertKind.Pass)
    {
        var now = DateTimeOffset.UtcNow;
        var sample = Pending.FirstOrDefault(a => a.Kind == kind)
            ?? (kind == AlertKind.Pass ? ExamplePass(now) : NextConjunction(now) ?? ExampleConjunction(now));
        var at = now + delay;
        // Real visible times keep the alert alive until they end; an example lives a quarter of an hour after posting.
        var visibleEnd = sample.VisibleEnd > at ? sample.VisibleEnd : at + TimeSpan.FromMinutes(15);
        var test = sample with { NotifyAt = at, VisibleEnd = visibleEnd, Title = "[PRUEBA] " + sample.Title };
        _platform.ArmTest(AlarmSteps.NextWakeUp(now, at), test);
    }

    private static ScheduledAlert ExamplePass(DateTimeOffset now) =>
        new(now, now + TimeSpan.FromMinutes(10), now + TimeSpan.FromMinutes(15), false,
            "La ISS pasa en 10 min", "A las 21:43 pasa la ISS · 5 min · aparece por el NO, máximo 67° al SE");

    private static ScheduledAlert ExampleConjunction(DateTimeOffset now) =>
        new(now, now + TimeSpan.FromMinutes(30), now + TimeSpan.FromHours(3), false,
            "La Luna junto a Júpiter", "Esta noche, la Luna junto a Júpiter (3°) · mejor hacia las 22:00 al SE", AlertKind.Conjunction);

    // The first conjunction from here, with the text its real alert will have.
    private ScheduledAlert? NextConjunction(DateTimeOffset now)
    {
        if (_observers.Last is not { } observer)
            return null;
        var conjunctions = _conjunctionFinder.Find(observer, now, now + TimeSpan.FromDays(60), TimeZoneInfo.Local);
        return _conjunctionPlanner.Plan(conjunctions, now, TimeZoneInfo.Local).FirstOrDefault() is { } alert
            ? ScheduledAlert.From(alert, TimeZoneInfo.Local)
            : null;
    }

    /// <summary>Brings the background planning forward to <paramref name="delay"/> from now, to try it with the app closed.</summary>
    public void ArmRefreshTest(TimeSpan delay) => ArmRefresh(DateTimeOffset.UtcNow + delay);

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

    private async Task RescheduleCoreAsync(string reason)
    {
        if (!IsOn)
        {
            _platform.ArmAlert(null);
            ArmRefresh(null);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var history = Read(HistoryKey, AlertJsonContext.Default.ListPlanningRecord);
        history.Add(new PlanningRecord(now, reason));
        Write(HistoryKey, history.TakeLast(HistoryLength).ToList(), AlertJsonContext.Default.ListPlanningRecord);

        if (_observers.Last is not { } observer)
        {
            Fail(now, "sin ubicación: abre el HUD con el GPS activo");
            return;
        }

        var timeZone = TimeZoneInfo.Local;
        var horizon = _planner.Settings.Horizon;

        // Conjunctions need nothing but the ephemeris: they are planned even with no network.
        var conjunctions = _conjunctionFinder.Find(observer, now, now + horizon, timeZone);
        var notifiedConjunctions = Read(NotifiedConjunctionsKey, AlertJsonContext.Default.ListNotifiedConjunction);
        var alerts = _conjunctionPlanner.Plan(conjunctions, now, timeZone, notifiedConjunctions)
            .Select(a => ScheduledAlert.From(a, timeZone))
            .ToList();

        string? problem = null;
        try
        {
            using var timeout = new CancellationTokenSource(TleTimeout);
            var tle = await _tleProvider.GetTleAsync(IssNoradNumber, timeout.Token).ConfigureAwait(false);
            var passes = _finder.Find(tle, observer, now, now + horizon);
            var notified = Read(NotifiedKey, AlertJsonContext.Default.ListDateTimeOffset);
            alerts.AddRange(_planner.Plan(passes, tle.Epoch, now, timeZone, notified).Select(a => ScheduledAlert.From(a, timeZone)));
        }
        catch (Exception ex) when (ex is TleUnavailableException or OperationCanceledException or IOException)
        {
            problem = "sin órbita de la ISS (sin red): solo avisos de la Luna y los planetas";
        }

        alerts = alerts.OrderBy(a => a.NotifyAt).ThenBy(a => a.WorthUntil).ToList();
        Write(PendingKey, alerts, AlertJsonContext.Default.ListScheduledAlert);
        if (problem is null)
            _preferences.Remove(ProblemKey);
        else
            _preferences.Set(ProblemKey, problem);
        // Early enough that the system delivering it late still lands on the alert; an alert already due goes off now.
        _platform.ArmAlert(alerts.Count > 0 ? AlarmSteps.NextWakeUp(now, alerts[0].NotifyAt) : null);
        // Without the TLE, try again sooner.
        ArmRefresh(now + (problem is null ? _planner.Settings.RefreshInterval : _planner.Settings.RetryInterval));
    }

    private void Fail(DateTimeOffset now, string problem)
    {
        _preferences.Set(ProblemKey, problem);
        _preferences.Remove(PendingKey);
        _platform.ArmAlert(null);
        ArmRefresh(now + _planner.Settings.RetryInterval);
    }

    /// <summary>The daily planning with the app closed. Inexact: a few minutes, or the hour ColorOS may add, do not matter here.</summary>
    private void ArmRefresh(DateTimeOffset? at)
    {
        _platform.ArmRefresh(at);
        if (at is { } time)
            _preferences.Set(NextRefreshKey, Format(time));
        else
            _preferences.Remove(NextRefreshKey);
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
