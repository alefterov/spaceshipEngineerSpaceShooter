using System;
using System.Globalization;

public static class NumberFormat
{
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
