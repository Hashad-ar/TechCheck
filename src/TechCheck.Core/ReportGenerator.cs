using System.Globalization;

namespace TechCheck.Core;

public static class ReportGenerator
{
    private const string LineEnding = "\r\n";

    public static string Generate(DiagnosticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var system = snapshot.System;
        var computer = system.Computer;
        var processor = system.Processor;
        var memory = system.Memory;
        var storage = system.Storage;
        var network = snapshot.Network;

        var lines = new List<string>
        {
            "TECHCHECK DIAGNOSTIC REPORT",
            $"Generated: {snapshot.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}",
            string.Empty,
            "SYSTEM",
            $"Computer: {DisplayFormatter.ValueOrUnavailable(computer.ComputerName)}",
            $"Manufacturer: {DisplayFormatter.ValueOrUnavailable(computer.Manufacturer)}",
            $"Model: {DisplayFormatter.ValueOrUnavailable(computer.Model)}",
            $"Serial Number: {DisplayFormatter.ValueOrUnavailable(computer.SerialNumber)}",
            $"Windows: {DisplayFormatter.ValueOrUnavailable(computer.WindowsVersion)}",
            $"Architecture: {DisplayFormatter.ValueOrUnavailable(computer.WindowsArchitecture)}",
            $"User: {DisplayFormatter.ValueOrUnavailable(computer.CurrentUser)}",
            $"Uptime: {DisplayFormatter.FormatUptime(computer.Uptime)}",
            string.Empty,
            "PROCESSOR",
            $"Name: {DisplayFormatter.ValueOrUnavailable(processor.Name)}",
            $"Logical Processors: {DisplayFormatter.FormatCount(processor.LogicalProcessorCount)}",
            $"Usage: {DisplayFormatter.FormatPercent(processor.UsagePercent)}",
            string.Empty,
            "MEMORY",
            $"Installed: {DisplayFormatter.FormatBytes(memory.TotalBytes)}",
            $"Used: {DisplayFormatter.FormatBytes(memory.UsedBytes)}",
            $"Available: {DisplayFormatter.FormatBytes(memory.AvailableBytes)}",
            $"Usage: {DisplayFormatter.FormatPercent(memory.UsagePercent)}",
            string.Empty,
            "STORAGE",
            $"Drive: {DisplayFormatter.ValueOrUnavailable(storage.DriveName)}",
            $"Total: {DisplayFormatter.FormatBytes(storage.TotalBytes)}",
            $"Used: {DisplayFormatter.FormatBytes(storage.UsedBytes)}",
            $"Free: {DisplayFormatter.FormatBytes(storage.FreeBytes)}",
            $"Usage: {DisplayFormatter.FormatPercent(storage.UsagePercent)}",
            string.Empty,
            "SYSTEM HEALTH",
            $"CPU: {FormatDiagnosticResult(snapshot.Health.Cpu)}",
            $"Memory: {FormatDiagnosticResult(snapshot.Health.Memory)}",
            $"Storage: {FormatDiagnosticResult(snapshot.Health.Storage)}",
            string.Empty,
            "NETWORK",
            $"Adapter: {DisplayFormatter.ValueOrUnavailable(network.AdapterName)}",
            $"Connection Type: {DisplayFormatter.ValueOrUnavailable(network.ConnectionType)}",
            $"IPv4: {DisplayFormatter.ValueOrUnavailable(network.Ipv4Address)}",
            $"Subnet Mask: {DisplayFormatter.ValueOrUnavailable(network.SubnetMask)}",
            $"Gateway: {DisplayFormatter.ValueOrUnavailable(network.DefaultGateway)}",
            $"DNS Servers: {DisplayFormatter.FormatDnsServers(network.DnsServers)}",
            $"MAC Address: {DisplayFormatter.ValueOrUnavailable(network.MacAddress)}",
            $"Adapter Status: {FormatDiagnosticResult(network.AdapterStatus)}",
            $"DNS Resolution: {FormatDiagnosticResult(network.DnsStatus)}",
            $"Internet Access: {FormatDiagnosticResult(network.InternetStatus)}",
        };

        return string.Join(LineEnding, lines) + LineEnding;
    }

    public static string FormatDiagnosticResult(DiagnosticResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var status = result.Status switch
        {
            DiagnosticStatus.Good => "PASS",
            DiagnosticStatus.Warning => "WARNING",
            DiagnosticStatus.Failed => "FAIL",
            DiagnosticStatus.Unavailable => "UNAVAILABLE",
            _ => "UNAVAILABLE",
        };

        var message = DisplayFormatter.ValueOrUnavailable(result.Message);
        return message == DisplayFormatter.Unavailable
            ? status
            : $"{status} - {message}";
    }
}
