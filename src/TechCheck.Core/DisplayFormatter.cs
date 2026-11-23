using System.Globalization;

namespace TechCheck.Core;

public static class DisplayFormatter
{
    public const string Unavailable = "Unavailable";

    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

    public static string FormatBytes(long? bytes)
    {
        if (bytes is null or < 0)
        {
            return Unavailable;
        }

        double value = bytes.Value;
        var unitIndex = 0;

        while (value >= 1024d && unitIndex < ByteUnits.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        var number = unitIndex == 0
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.#", CultureInfo.InvariantCulture);

        return $"{number} {ByteUnits[unitIndex]}";
    }

    public static string FormatStorage(long? bytes, string? label)
    {
        var formatted = FormatBytes(bytes);
        var normalizedLabel = label?.Trim();
        return formatted == Unavailable || string.IsNullOrEmpty(normalizedLabel)
            ? formatted
            : $"{formatted} {normalizedLabel}";
    }

    public static string FormatPercent(double? percent)
    {
        return IsValidPercent(percent)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{percent!.Value:0.#}%")
            : Unavailable;
    }

    public static string FormatUptime(TimeSpan? uptime)
    {
        if (!IsValidUptime(uptime))
        {
            return Unavailable;
        }

        var value = uptime!.Value;
        if (value == TimeSpan.Zero)
        {
            return "0 minutes";
        }

        if (value < TimeSpan.FromMinutes(1))
        {
            return "< 1 minute";
        }

        List<string> parts = [];
        AddPart(parts, value.Days, "day");
        AddPart(parts, value.Hours, "hour");
        AddPart(parts, value.Minutes, "minute");

        return string.Join(' ', parts);
    }

    public static string FormatCompactUptime(TimeSpan? uptime)
    {
        if (!IsValidUptime(uptime))
        {
            return Unavailable;
        }

        var value = uptime!.Value;
        if (value == TimeSpan.Zero)
        {
            return "0m";
        }

        if (value < TimeSpan.FromMinutes(1))
        {
            return "<1m";
        }

        List<string> parts = [];
        AddCompactPart(parts, value.Days, "d");
        AddCompactPart(parts, value.Hours, "h");
        AddCompactPart(parts, value.Minutes, "m");

        return string.Join(' ', parts);
    }

    public static string ValueOrUnavailable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Unavailable : value.Trim();
    }

    public static string FormatCount(int? value)
    {
        return value is > 0
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : Unavailable;
    }

    public static string FormatDnsServers(IEnumerable<string>? servers)
    {
        if (servers is null)
        {
            return Unavailable;
        }

        var values = servers
            .Where(static server => !string.IsNullOrWhiteSpace(server))
            .Select(static server => server.Trim())
            .ToArray();

        return values.Length == 0 ? Unavailable : string.Join(", ", values);
    }

    internal static bool IsValidPercent(double? percent)
    {
        return percent is >= 0d and <= 100d
            && !double.IsNaN(percent.Value)
            && !double.IsInfinity(percent.Value);
    }

    private static bool IsValidUptime(TimeSpan? uptime)
    {
        return uptime is not null && uptime.Value >= TimeSpan.Zero;
    }

    private static void AddPart(ICollection<string> parts, int value, string singularUnit)
    {
        if (value > 0)
        {
            parts.Add($"{value.ToString(CultureInfo.InvariantCulture)} {singularUnit}{(value == 1 ? string.Empty : "s")}");
        }
    }

    private static void AddCompactPart(ICollection<string> parts, int value, string unit)
    {
        if (value > 0)
        {
            parts.Add($"{value.ToString(CultureInfo.InvariantCulture)}{unit}");
        }
    }
}
