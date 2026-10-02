namespace CieloHud.Core.Satellites;

/// <summary>
/// Two-line element set describing a satellite orbit at a given epoch. Lines are kept verbatim for the propagator.
/// </summary>
public readonly record struct Tle
{
    private const int LineLength = 69;

    public string Name { get; }
    public string Line1 { get; }
    public string Line2 { get; }

    /// <summary>NORAD catalog number, parsed from columns 3-7 of line 1 (ISS = 25544).</summary>
    public int NoradNumber { get; }

    /// <summary>Epoch of the elements, in UTC. Accuracy degrades as the instant moves away from it.</summary>
    public DateTimeOffset Epoch { get; }

    public Tle(string name, string line1, string line2)
    {
        ArgumentNullException.ThrowIfNull(name);
        ValidateLine(line1, '1', nameof(line1));
        ValidateLine(line2, '2', nameof(line2));

        Name = name.Trim();
        Line1 = line1;
        Line2 = line2;
        NoradNumber = int.Parse(line1.AsSpan(2, 5), System.Globalization.CultureInfo.InvariantCulture);
        Epoch = ParseEpoch(line1);
    }

    /// <summary>Parses the three-line format ("name", line 1, line 2) as published by CelesTrak.</summary>
    public static Tle Parse(string threeLines)
    {
        ArgumentNullException.ThrowIfNull(threeLines);
        var lines = threeLines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.TrimEnd('\r'))
            .ToArray();
        if (lines.Length != 3)
            throw new FormatException($"Expected 3 lines (name, line 1, line 2) but found {lines.Length}.");
        return new Tle(lines[0], lines[1], lines[2]);
    }

    private static void ValidateLine(string line, char expectedNumber, string paramName)
    {
        ArgumentNullException.ThrowIfNull(line, paramName);
        if (line.Length != LineLength)
            throw new FormatException($"TLE line {expectedNumber} must be {LineLength} characters long but was {line.Length}.");
        if (line[0] != expectedNumber || line[1] != ' ')
            throw new FormatException($"TLE line {expectedNumber} must start with '{expectedNumber} '.");
    }

    // Columns 19-20: two-digit year (57-99 => 1900s, 00-56 => 2000s). Columns 21-32: day of year with fraction.
    private static DateTimeOffset ParseEpoch(string line1)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var twoDigitYear = int.Parse(line1.AsSpan(18, 2), culture);
        var year = twoDigitYear >= 57 ? 1900 + twoDigitYear : 2000 + twoDigitYear;
        var dayOfYear = double.Parse(line1.AsSpan(20, 12), culture);
        return new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayOfYear - 1);
    }
}
