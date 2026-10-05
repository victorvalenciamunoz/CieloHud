using System.Text.Json.Serialization;
using CieloHud.Core.Alerts;

namespace CieloHud.App.Alerts;

/// <summary>
/// A <see cref="PassAlert"/> ready to show: the text is written when it is planned, so the alarm only has to post it.
/// Stored in <c>Preferences</c> as JSON: the app is usually closed, and its process gone, by the time the alarm goes off.
/// </summary>
public sealed record ScheduledAlert(
    DateTimeOffset NotifyAt,
    DateTimeOffset VisibleStart,
    DateTimeOffset VisibleEnd,
    bool IsEveningBefore,
    string Title,
    string Body)
{
    public static ScheduledAlert From(PassAlert alert, TimeZoneInfo timeZone) => new(
        alert.NotifyAt,
        alert.Pass.VisibleStart.Instant,
        alert.Pass.VisibleEnd.Instant,
        alert.IsEveningBefore,
        PassAlertText.Title(alert),
        PassAlertText.Body(alert, timeZone));
}

// Source-generated: no reflection, safe with trimming in Release.
[JsonSerializable(typeof(List<ScheduledAlert>))]
[JsonSerializable(typeof(List<DateTimeOffset>))]
internal sealed partial class AlertJsonContext : JsonSerializerContext;
