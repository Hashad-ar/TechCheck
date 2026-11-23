using System.Globalization;
using TechCheck.Core;

namespace TechCheck.Tests;

internal static class Program
{
    private const long Kibibyte = 1024L;
    private const long Mebibyte = Kibibyte * 1024L;
    private const long Gibibyte = Mebibyte * 1024L;

    private static readonly (string Name, Action Test)[] Tests =
    [
        ("Byte formatting uses binary units and stable rounding", ByteFormattingUsesBinaryUnits),
        ("Storage and percent formatting handle valid and invalid data", StorageAndPercentFormattingHandleInvalidData),
        ("Long uptime formatting handles boundaries and plurals", LongUptimeFormattingHandlesBoundaries),
        ("Compact uptime formatting stays concise", CompactUptimeFormattingStaysConcise),
        ("CPU health includes exact warning and failure boundaries", CpuThresholdBoundariesAreInclusive),
        ("Memory health includes exact warning and failure boundaries", MemoryThresholdBoundariesAreInclusive),
        ("Storage health includes exact warning and failure boundaries", StorageThresholdBoundariesAreInclusive),
        ("Invalid usage measurements are unavailable", InvalidUsageIsUnavailable),
        ("Usage percentage calculation rejects inconsistent byte counts", UsageCalculationRejectsInvalidCounts),
        ("Network outcomes map to meaningful diagnostic states", NetworkOutcomesMapToStates),
        ("Network snapshots defensively copy DNS values", NetworkSnapshotIsImmutable),
        ("Diagnostic report is complete and deterministic", ReportIsCompleteAndDeterministic),
        ("Diagnostic report handles unavailable fields", ReportHandlesUnavailableFields),
        ("Report status labels cover every diagnostic state", ReportStatusLabelsCoverEveryState),
        ("Formatting is independent of the current culture", FormattingIsCultureInvariant),
        ("Public entry points reject null aggregate objects", NullAggregatesAreRejected),
    ];

    private static int Main()
    {
        var failed = 0;

        foreach (var (name, test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS  {name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL  {name}");
                Console.Error.WriteLine($"      {exception.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{Tests.Length - failed}/{Tests.Length} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static void ByteFormattingUsesBinaryUnits()
    {
        Assert.Equal("Unavailable", DisplayFormatter.FormatBytes(null));
        Assert.Equal("Unavailable", DisplayFormatter.FormatBytes(-1));
        Assert.Equal("0 B", DisplayFormatter.FormatBytes(0));
        Assert.Equal("1023 B", DisplayFormatter.FormatBytes(1023));
        Assert.Equal("1 KB", DisplayFormatter.FormatBytes(Kibibyte));
        Assert.Equal("1.5 KB", DisplayFormatter.FormatBytes(Kibibyte + (Kibibyte / 2)));
        Assert.Equal("16 MB", DisplayFormatter.FormatBytes(16 * Mebibyte));
        Assert.Equal("9.2 GB", DisplayFormatter.FormatBytes((long)(9.2d * Gibibyte)));
        Assert.Equal("8 EB", DisplayFormatter.FormatBytes(long.MaxValue));
    }

    private static void StorageAndPercentFormattingHandleInvalidData()
    {
        Assert.Equal("476 GB Total", DisplayFormatter.FormatStorage(476 * Gibibyte, "Total"));
        Assert.Equal("138 GB Free", DisplayFormatter.FormatStorage(138 * Gibibyte, " Free "));
        Assert.Equal("138 GB", DisplayFormatter.FormatStorage(138 * Gibibyte, null));
        Assert.Equal("Unavailable", DisplayFormatter.FormatStorage(null, "Total"));
        Assert.Equal("71%", DisplayFormatter.FormatPercent(71));
        Assert.Equal("71.3%", DisplayFormatter.FormatPercent(71.25));
        Assert.Equal("Unavailable", DisplayFormatter.FormatPercent(null));
        Assert.Equal("Unavailable", DisplayFormatter.FormatPercent(double.PositiveInfinity));
        Assert.Equal("Unavailable", DisplayFormatter.FormatPercent(100.01));
        Assert.Equal("Unavailable", DisplayFormatter.ValueOrUnavailable("  "));
        Assert.Equal("value", DisplayFormatter.ValueOrUnavailable(" value "));
    }

    private static void LongUptimeFormattingHandlesBoundaries()
    {
        Assert.Equal("Unavailable", DisplayFormatter.FormatUptime(null));
        Assert.Equal("Unavailable", DisplayFormatter.FormatUptime(TimeSpan.FromTicks(-1)));
        Assert.Equal("0 minutes", DisplayFormatter.FormatUptime(TimeSpan.Zero));
        Assert.Equal("< 1 minute", DisplayFormatter.FormatUptime(TimeSpan.FromSeconds(59)));
        Assert.Equal("1 minute", DisplayFormatter.FormatUptime(TimeSpan.FromMinutes(1)));
        Assert.Equal("1 hour", DisplayFormatter.FormatUptime(TimeSpan.FromHours(1)));
        Assert.Equal("1 day 1 hour 1 minute", DisplayFormatter.FormatUptime(new TimeSpan(1, 1, 1, 59)));
        Assert.Equal("2 days 4 hours", DisplayFormatter.FormatUptime(new TimeSpan(2, 4, 0, 0)));
    }

    private static void CompactUptimeFormattingStaysConcise()
    {
        Assert.Equal("Unavailable", DisplayFormatter.FormatCompactUptime(null));
        Assert.Equal("0m", DisplayFormatter.FormatCompactUptime(TimeSpan.Zero));
        Assert.Equal("<1m", DisplayFormatter.FormatCompactUptime(TimeSpan.FromSeconds(30)));
        Assert.Equal("1m", DisplayFormatter.FormatCompactUptime(TimeSpan.FromMinutes(1)));
        Assert.Equal("2d 4h 5m", DisplayFormatter.FormatCompactUptime(new TimeSpan(2, 4, 5, 0)));
    }

    private static void CpuThresholdBoundariesAreInclusive()
    {
        Assert.Status(DiagnosticStatus.Good, HealthEvaluator.EvaluateCpu(79.9));
        Assert.Status(DiagnosticStatus.Warning, HealthEvaluator.EvaluateCpu(80));
        Assert.Status(DiagnosticStatus.Warning, HealthEvaluator.EvaluateCpu(94.9));
        Assert.Status(DiagnosticStatus.Failed, HealthEvaluator.EvaluateCpu(95));
        Assert.Status(DiagnosticStatus.Failed, HealthEvaluator.EvaluateCpu(100));
    }

    private static void MemoryThresholdBoundariesAreInclusive()
    {
        Assert.Status(DiagnosticStatus.Good, HealthEvaluator.EvaluateMemory(79.9));
        Assert.Status(DiagnosticStatus.Warning, HealthEvaluator.EvaluateMemory(80));
        Assert.Status(DiagnosticStatus.Warning, HealthEvaluator.EvaluateMemory(94.9));
        Assert.Status(DiagnosticStatus.Failed, HealthEvaluator.EvaluateMemory(95));
    }

    private static void StorageThresholdBoundariesAreInclusive()
    {
        Assert.Status(DiagnosticStatus.Good, HealthEvaluator.EvaluateStorage(84.9));
        Assert.Status(DiagnosticStatus.Warning, HealthEvaluator.EvaluateStorage(85));
        Assert.Status(DiagnosticStatus.Warning, HealthEvaluator.EvaluateStorage(94.9));
        Assert.Status(DiagnosticStatus.Failed, HealthEvaluator.EvaluateStorage(95));
    }

    private static void InvalidUsageIsUnavailable()
    {
        Assert.Status(DiagnosticStatus.Unavailable, HealthEvaluator.EvaluateCpu(null));
        Assert.Status(DiagnosticStatus.Unavailable, HealthEvaluator.EvaluateMemory(-0.01));
        Assert.Status(DiagnosticStatus.Unavailable, HealthEvaluator.EvaluateStorage(100.01));
        Assert.Status(DiagnosticStatus.Unavailable, HealthEvaluator.EvaluateCpu(double.NaN));
        Assert.Status(DiagnosticStatus.Unavailable, HealthEvaluator.EvaluateCpu(double.NegativeInfinity));
    }

    private static void UsageCalculationRejectsInvalidCounts()
    {
        Assert.AlmostEqual(50d, HealthEvaluator.CalculateUsagePercent(8 * Gibibyte, 16 * Gibibyte));
        Assert.Null(HealthEvaluator.CalculateUsagePercent(null, 16 * Gibibyte));
        Assert.Null(HealthEvaluator.CalculateUsagePercent(-1, 16 * Gibibyte));
        Assert.Null(HealthEvaluator.CalculateUsagePercent(1, 0));
        Assert.Null(HealthEvaluator.CalculateUsagePercent(17 * Gibibyte, 16 * Gibibyte));
    }

    private static void NetworkOutcomesMapToStates()
    {
        var connected = HealthEvaluator.EvaluateNetworkAdapter(CheckOutcome.Succeeded);
        Assert.Status(DiagnosticStatus.Good, connected);
        Assert.Equal("Connected", connected.Message);

        Assert.Status(DiagnosticStatus.Failed, HealthEvaluator.EvaluateNetworkAdapter(CheckOutcome.Failed));
        Assert.Status(DiagnosticStatus.Good, HealthEvaluator.EvaluateDns(CheckOutcome.Succeeded));
        Assert.Status(DiagnosticStatus.Failed, HealthEvaluator.EvaluateDns(CheckOutcome.Failed));
        Assert.Status(DiagnosticStatus.Unavailable, HealthEvaluator.EvaluateDns(CheckOutcome.Unavailable));
        Assert.Status(DiagnosticStatus.Good, HealthEvaluator.EvaluateInternet(CheckOutcome.Succeeded));
        Assert.Status(DiagnosticStatus.Failed, HealthEvaluator.EvaluateInternet(CheckOutcome.Failed));
        Assert.Status(DiagnosticStatus.Unavailable, HealthEvaluator.EvaluateInternet((CheckOutcome)999));
    }

    private static void NetworkSnapshotIsImmutable()
    {
        var dnsServers = new[] { " 1.1.1.1 ", " ", "8.8.8.8" };
        var snapshot = CreateNetworkSnapshot(dnsServers);

        dnsServers[0] = "changed";

        Assert.Equal(2, snapshot.DnsServers.Count);
        Assert.Equal("1.1.1.1", snapshot.DnsServers[0]);
        Assert.Equal("8.8.8.8", snapshot.DnsServers[1]);
    }

    private static void ReportIsCompleteAndDeterministic()
    {
        var snapshot = CreateCompleteSnapshot();
        var report = ReportGenerator.Generate(snapshot);

        var expectedLines = new[]
        {
            "TECHCHECK DIAGNOSTIC REPORT",
            "Generated: 2026-08-19 16:30",
            string.Empty,
            "SYSTEM",
            "Computer: DESKTOP-123",
            "Manufacturer: Dell",
            "Model: Latitude 5440",
            "Serial Number: ABC123",
            "Windows: Windows 11 Pro",
            "Architecture: 64-bit",
            "User: Technician",
            "Uptime: 2 days 4 hours",
            string.Empty,
            "PROCESSOR",
            "Name: Intel Core i7",
            "Logical Processors: 12",
            "Usage: 42%",
            string.Empty,
            "MEMORY",
            "Installed: 16 GB",
            "Used: 9 GB",
            "Available: 7 GB",
            "Usage: 56.3%",
            string.Empty,
            "STORAGE",
            "Drive: C:",
            "Total: 476 GB",
            "Used: 338 GB",
            "Free: 138 GB",
            "Usage: 71%",
            string.Empty,
            "SYSTEM HEALTH",
            "CPU: PASS - Normal (42% used)",
            "Memory: PASS - Normal (56.3% used)",
            "Storage: PASS - Normal (71% used)",
            string.Empty,
            "NETWORK",
            "Adapter: Intel Wi-Fi 6",
            "Connection Type: Wi-Fi",
            "IPv4: 192.168.1.25",
            "Subnet Mask: 255.255.255.0",
            "Gateway: 192.168.1.1",
            "DNS Servers: 1.1.1.1, 8.8.8.8",
            "MAC Address: 00-11-22-33-44-55",
            "Adapter Status: PASS - Connected",
            "DNS Resolution: PASS - Resolved",
            "Internet Access: PASS - Connected",
        };
        var expected = string.Join("\r\n", expectedLines) + "\r\n";

        Assert.Equal(expected, report);
        Assert.Equal(report, ReportGenerator.Generate(snapshot));
    }

    private static void ReportHandlesUnavailableFields()
    {
        var unavailable = new DiagnosticResult(DiagnosticStatus.Unavailable, DisplayFormatter.Unavailable);
        var system = new SystemSnapshot(
            new ComputerSnapshot(null, null, null, null, null, null, null, null),
            new ProcessorSnapshot(null, null, null),
            new MemorySnapshot(null, null, null, null),
            new StorageSnapshot(null, null, null, null, null));
        var network = new NetworkSnapshot(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            unavailable,
            unavailable,
            unavailable);
        var snapshot = new DiagnosticSnapshot(
            new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero),
            system,
            HealthEvaluator.Evaluate(system),
            network);

        var report = ReportGenerator.Generate(snapshot);

        Assert.Contains("Computer: Unavailable\r\n", report);
        Assert.Contains("Uptime: Unavailable\r\n", report);
        Assert.Contains("Installed: Unavailable\r\n", report);
        Assert.Contains("DNS Servers: Unavailable\r\n", report);
        Assert.Contains("CPU: UNAVAILABLE\r\n", report);
        Assert.Contains("Internet Access: UNAVAILABLE\r\n", report);
    }

    private static void ReportStatusLabelsCoverEveryState()
    {
        Assert.Equal("PASS - Fine", ReportGenerator.FormatDiagnosticResult(new(DiagnosticStatus.Good, "Fine")));
        Assert.Equal("WARNING - Check", ReportGenerator.FormatDiagnosticResult(new(DiagnosticStatus.Warning, "Check")));
        Assert.Equal("FAIL - Failed", ReportGenerator.FormatDiagnosticResult(new(DiagnosticStatus.Failed, "Failed")));
        Assert.Equal("UNAVAILABLE", ReportGenerator.FormatDiagnosticResult(new(DiagnosticStatus.Unavailable, "")));
        Assert.Equal("UNAVAILABLE", ReportGenerator.FormatDiagnosticResult(new((DiagnosticStatus)999, "")));
    }

    private static void FormattingIsCultureInvariant()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.Equal("1.5 KB", DisplayFormatter.FormatBytes(1536));
            Assert.Equal("71.3%", DisplayFormatter.FormatPercent(71.25));
            Assert.Contains("Generated: 2026-08-19 16:30\r\n", ReportGenerator.Generate(CreateCompleteSnapshot()));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static void NullAggregatesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ReportGenerator.Generate(null!));
        Assert.Throws<ArgumentNullException>(() => HealthEvaluator.Evaluate(null!));
        Assert.Throws<ArgumentNullException>(() => ReportGenerator.FormatDiagnosticResult(null!));
    }

    private static NetworkSnapshot CreateNetworkSnapshot(IEnumerable<string>? dnsServers)
    {
        return new NetworkSnapshot(
            "Intel Wi-Fi 6",
            "Wi-Fi",
            "192.168.1.25",
            "255.255.255.0",
            "192.168.1.1",
            dnsServers,
            "00-11-22-33-44-55",
            HealthEvaluator.EvaluateNetworkAdapter(CheckOutcome.Succeeded),
            HealthEvaluator.EvaluateDns(CheckOutcome.Succeeded),
            HealthEvaluator.EvaluateInternet(CheckOutcome.Succeeded));
    }

    private static DiagnosticSnapshot CreateCompleteSnapshot()
    {
        var system = new SystemSnapshot(
            new ComputerSnapshot(
                "DESKTOP-123",
                "Dell",
                "Latitude 5440",
                "ABC123",
                "Windows 11 Pro",
                "64-bit",
                "Technician",
                new TimeSpan(2, 4, 0, 0)),
            new ProcessorSnapshot("Intel Core i7", 12, 42),
            new MemorySnapshot(16 * Gibibyte, 9 * Gibibyte, 7 * Gibibyte, 56.25),
            new StorageSnapshot("C:", 476 * Gibibyte, 338 * Gibibyte, 138 * Gibibyte, 71));

        return new DiagnosticSnapshot(
            new DateTimeOffset(2026, 8, 19, 16, 30, 0, TimeSpan.FromHours(-4)),
            system,
            HealthEvaluator.Evaluate(system),
            CreateNetworkSnapshot(["1.1.1.1", "8.8.8.8"]));
    }
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected <{expected}> but received <{actual}>.");
        }
    }

    public static void Contains(string expectedSubstring, string actual)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected text to contain <{expectedSubstring}>.");
        }
    }

    public static void Null(object? actual)
    {
        if (actual is not null)
        {
            throw new InvalidOperationException($"Expected <null> but received <{actual}>.");
        }
    }

    public static void AlmostEqual(double expected, double? actual, double tolerance = 0.000_001d)
    {
        if (actual is null || Math.Abs(expected - actual.Value) > tolerance)
        {
            throw new InvalidOperationException($"Expected approximately <{expected}> but received <{actual}>.");
        }
    }

    public static void Status(DiagnosticStatus expected, DiagnosticResult result)
    {
        Equal(expected, result.Status);
    }

    public static void Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name} to be thrown.");
    }
}
