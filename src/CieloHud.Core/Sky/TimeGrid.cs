namespace CieloHud.Core.Sky;

/// <summary>Sampling on whole steps (:00, :05… UTC), so a search gives the same result whenever it is computed (decision 030).</summary>
internal static class TimeGrid
{
    /// <summary>The first whole step at or after <paramref name="instant"/>.</summary>
    public static DateTimeOffset AlignUp(DateTimeOffset instant, TimeSpan step)
    {
        var remainder = instant.UtcTicks % step.Ticks;
        return remainder == 0 ? instant : instant.AddTicks(step.Ticks - remainder);
    }
}
