using System.Text.Json.Serialization;
using CieloHud.Core.Alerts;

namespace CieloHud.App.Alerts;

/// <summary>What an alert announces. A pass is 0: alerts stored before conjunctions existed read as passes.</summary>
public enum AlertKind
{
    Pass = 0,
    Conjunction = 1,
}

/// <summary>
/// A <see cref="PassAlert"/> or <see cref="ConjunctionAlert"/> ready to show: the text is written when it is planned, so the
/// alarm only has to post it. Stored in <c>Preferences</c> as JSON: the app is usually closed, and its process gone, by the time
/// the alarm goes off.
/// </summary>
/// <param name="VisibleStart">Start of the visible pass, or of the conjunction window.</param>
/// <param name="VisibleEnd">End of the visible pass, or of the conjunction window: the notification goes away then.</param>
/// <param name="Conjunctions">For a conjunction, what to remember once announced so it is not announced again.</param>
/// <param name="Guide">For a conjunction, the HUD target to guide to: "Luna", or the brighter of two planets ("Júpiter").
/// Missing in conjunctions stored before two planets existed, which were all of the Moon.</param>
public sealed record ScheduledAlert(
    DateTimeOffset NotifyAt,
    DateTimeOffset VisibleStart,
    DateTimeOffset VisibleEnd,
    bool IsEveningBefore,
    string Title,
    string Body,
    AlertKind Kind = AlertKind.Pass,
    IReadOnlyList<NotifiedConjunction>? Conjunctions = null,
    string? Guide = null)
{
    /// <summary>Past this, posting it is pointless: a pass that has started, a conjunction that is over.</summary>
    [JsonIgnore]
    public DateTimeOffset WorthUntil => Kind == AlertKind.Pass ? VisibleStart : VisibleEnd;

    /// <summary>What the HUD guides to when the alert is tapped: the ISS, the Moon, or the brighter of two planets.</summary>
    [JsonIgnore]
    public string Target => Kind == AlertKind.Pass ? "ISS" : Guide ?? "Luna";

    public static ScheduledAlert From(PassAlert alert, TimeZoneInfo timeZone) => new(
        alert.NotifyAt,
        alert.Pass.VisibleStart.Instant,
        alert.Pass.VisibleEnd.Instant,
        alert.IsEveningBefore,
        PassAlertText.Title(alert),
        PassAlertText.Body(alert, timeZone));

    public static ScheduledAlert From(ConjunctionAlert alert, TimeZoneInfo timeZone) => new(
        alert.NotifyAt,
        alert.WindowStart,
        alert.WindowEnd,
        alert.IsEveningBefore,
        ConjunctionAlertText.Title(alert),
        ConjunctionAlertText.Body(alert, timeZone),
        AlertKind.Conjunction,
        alert.Conjunctions.Select(NotifiedConjunction.From).ToList(),
        ConjunctionAlertText.GuideName(alert.Guide));
}

/// <summary>One planning of the alerts and why it ran.</summary>
public sealed record PlanningRecord(DateTimeOffset At, string Reason);

// Source-generated: no reflection, safe with trimming in Release.
[JsonSerializable(typeof(List<ScheduledAlert>))]
[JsonSerializable(typeof(List<DateTimeOffset>))]
[JsonSerializable(typeof(List<NotifiedConjunction>))]
[JsonSerializable(typeof(List<PlanningRecord>))]
internal sealed partial class AlertJsonContext : JsonSerializerContext;
