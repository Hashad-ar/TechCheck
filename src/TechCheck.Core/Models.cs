using System.Collections.ObjectModel;

namespace TechCheck.Core;

public enum DiagnosticStatus
{
    Good,
    Warning,
    Failed,
    Unavailable,
}

public enum CheckOutcome
{
    Succeeded,
    Failed,
    Unavailable,
}

public sealed record DiagnosticResult(DiagnosticStatus Status, string Message)
{
    public bool IsGood => Status == DiagnosticStatus.Good;
}

public sealed record ComputerSnapshot(
    string? ComputerName,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? WindowsVersion,
    string? WindowsArchitecture,
    string? CurrentUser,
    TimeSpan? Uptime);

public sealed record ProcessorSnapshot(
    string? Name,
    int? LogicalProcessorCount,
    double? UsagePercent);

public sealed record MemorySnapshot(
    long? TotalBytes,
    long? UsedBytes,
    long? AvailableBytes,
    double? UsagePercent);

public sealed record StorageSnapshot(
    string? DriveName,
    long? TotalBytes,
    long? UsedBytes,
    long? FreeBytes,
    double? UsagePercent);

public sealed record SystemSnapshot(
    ComputerSnapshot Computer,
    ProcessorSnapshot Processor,
    MemorySnapshot Memory,
    StorageSnapshot Storage);

public sealed record SystemHealthSnapshot(
    DiagnosticResult Cpu,
    DiagnosticResult Memory,
    DiagnosticResult Storage);

public sealed record NetworkSnapshot
{
    public NetworkSnapshot(
        string? adapterName,
        string? connectionType,
        string? ipv4Address,
        string? subnetMask,
        string? defaultGateway,
        IEnumerable<string>? dnsServers,
        string? macAddress,
        DiagnosticResult adapterStatus,
        DiagnosticResult dnsStatus,
        DiagnosticResult internetStatus)
    {
        AdapterName = adapterName;
        ConnectionType = connectionType;
        Ipv4Address = ipv4Address;
        SubnetMask = subnetMask;
        DefaultGateway = defaultGateway;
        DnsServers = new ReadOnlyCollection<string>(
            dnsServers?
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToArray()
                ?? []);
        MacAddress = macAddress;
        AdapterStatus = adapterStatus;
        DnsStatus = dnsStatus;
        InternetStatus = internetStatus;
    }

    public string? AdapterName { get; }

    public string? ConnectionType { get; }

    public string? Ipv4Address { get; }

    public string? SubnetMask { get; }

    public string? DefaultGateway { get; }

    public IReadOnlyList<string> DnsServers { get; }

    public string? MacAddress { get; }

    public DiagnosticResult AdapterStatus { get; }

    public DiagnosticResult DnsStatus { get; }

    public DiagnosticResult InternetStatus { get; }
}

public sealed record DiagnosticSnapshot(
    DateTimeOffset GeneratedAt,
    SystemSnapshot System,
    SystemHealthSnapshot Health,
    NetworkSnapshot Network);
