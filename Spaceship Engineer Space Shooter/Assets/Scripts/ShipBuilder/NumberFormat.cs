using System;
using System.Globalization;

/// <summary>
/// Display-only shortening of big numbers for prices and amounts: 1 000 -> "1k", 1 250 000 -> "1.25m",
/// 3 400 000 000 -> "3.4b". Rounded to hundredths for DISPLAY only (trailing zeros dropped) — the real
/// amounts that are earned or spent are never rounded; always format at the moment the text is set and keep
/// the exact integer everywhere else.
///
/// Numbers below Threshold are shown in full. Threshold is set by GameDataManager (Compact Numbers From).
/// </summary>
public static class NumberFormat
{
    /// <summary>Numbers of this size or bigger are shortened. Set from GameDataManager.</summary>
    public static long Threshold = 1000;

    private static readonly string[] Suffixes = { "", "k", "m", "b", "t", "q" };

    public static string Compact(long value)
    {
        long magnitude = Math.Abs(value);
        if (magnitude < Threshold || magnitude < 1000) return value.ToString(CultureInfo.InvariantCulture);

        double scaled = magnitude;
        int suffix = 0;
        while (scaled >= 1000d && suffix < Suffixes.Length - 1)
        {
            scaled /= 1000d;
            suffix++;
        }

        // Rounding up can reach 1000 (999 999 -> 1000k): step up one more suffix instead.
        double rounded = Math.Round(scaled, 2, MidpointRounding.AwayFromZero);
        if (rounded >= 1000d && suffix < Suffixes.Length - 1)
        {
            rounded = Math.Round(rounded / 1000d, 2, MidpointRounding.AwayFromZero);
            suffix++;
        }

        string text = rounded.ToString("0.##", CultureInfo.InvariantCulture) + Suffixes[suffix];
        return value < 0 ? "-" + text : text;
    }

    public static string Compact(int value) => Compact((long)value);
    public static string Compact(float value) => Compact((long)Math.Round(value));
}
