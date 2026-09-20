using System;
using System.Collections.Generic;

public static class TimeSpanExtensiont
{
    public static string FormatTimeRemaining(this TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        List<string> parts = new();

        if (time.Days > 0)
            parts.Add($"{time.Days}d");
        if (time.Hours > 0)
            parts.Add($"{time.Hours}h");
        if (time.Minutes > 0)
            parts.Add($"{time.Minutes}m");
        if (time.Seconds > 0 || parts.Count == 0)
            parts.Add($"{time.Seconds}s");

        return string.Join(" ", parts);
    }
}
