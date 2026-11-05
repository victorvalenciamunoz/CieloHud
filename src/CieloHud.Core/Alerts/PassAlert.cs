using CieloHud.Core.Passes;

namespace CieloHud.Core.Alerts;

/// <summary>A visible pass and when to announce it.</summary>
/// <param name="NotifyAt">When the alert goes off (UTC). Equal to the planning instant when it is already due.</param>
/// <param name="IsEveningBefore">True when the pass would be announced in the quiet hours and is moved to the evening before.</param>
public sealed record PassAlert(VisiblePass Pass, DateTimeOffset NotifyAt, bool IsEveningBefore);
