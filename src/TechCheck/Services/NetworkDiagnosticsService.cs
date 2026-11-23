using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using TechCheck.Core;

namespace TechCheck.Services;

/// <summary>
/// Captures the primary active adapter and performs two bounded connectivity checks.
/// </summary>
public sealed class NetworkDiagnosticsService : IDisposable
{
    private const string DnsTestHost = "example.com";
    private const string InternetTestUrl = "https://example.com/";
    private const uint PublicRouteProbeAddress = 0x08080808;
    private static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan InternetTimeout = TimeSpan.FromSeconds(4);

    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _disposeGate = new();
    private bool _disposed;

    public NetworkDiagnosticsService()
    {
        SocketsHttpHandler handler = new()
        {
            ConnectTimeout = InternetTimeout,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        _httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TechCheck/1.0");
    }

    public async Task<NetworkSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource operationCancellation =
            CreateOperationCancellation(cancellationToken);
        CancellationToken operationToken = operationCancellation.Token;

        AdapterCapture adapter = CapturePrimaryAdapter(operationToken);
        Task<DiagnosticResult> dnsTask = CheckDnsAsync(operationToken);
        Task<DiagnosticResult> internetTask = CheckInternetAsync(operationToken);

        await Task.WhenAll(dnsTask, internetTask).ConfigureAwait(false);
        operationToken.ThrowIfCancellationRequested();

        return new NetworkSnapshot(
            adapter.AdapterName,
            adapter.ConnectionType,
            adapter.Ipv4Address,
            adapter.SubnetMask,
            adapter.DefaultGateway,
            adapter.DnsServers,
            adapter.MacAddress,
            adapter.Status,
            await dnsTask.ConfigureAwait(false),
            await internetTask.ConfigureAwait(false));
    }

    public void Dispose()
    {
        lock (_disposeGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
            _httpClient.Dispose();
            _lifetimeCancellation.Dispose();
        }
    }

    private CancellationTokenSource CreateOperationCancellation(CancellationToken cancellationToken)
    {
        lock (_disposeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
        }
    }

    private static AdapterCapture CapturePrimaryAdapter(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            uint? bestInterfaceIndex = TryGetBestInterfaceIndex();
            List<AdapterCandidate> candidates = [];

            foreach (NetworkInterface networkInterface in interfaces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (networkInterface.OperationalStatus != OperationalStatus.Up
                    || networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                        or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                try
                {
                    IPInterfaceProperties properties = networkInterface.GetIPProperties();
                    UnicastIPAddressInformation? ipv4Address = properties.UnicastAddresses
                        .Where(static address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Where(static address => !IPAddress.IsLoopback(address.Address)
                            && !address.Address.Equals(IPAddress.Any))
                        .OrderBy(static address => IsLinkLocal(address.Address))
                        .FirstOrDefault();

                    if (ipv4Address is null)
                    {
                        continue;
                    }

                    int? ipv4Index = properties.GetIPv4Properties()?.Index;
                    bool hasGateway = properties.GatewayAddresses.Any(
                        static gateway => IsUsableAddress(gateway.Address));
                    candidates.Add(new AdapterCandidate(
                        networkInterface,
                        properties,
                        ipv4Address,
                        ipv4Index,
                        hasGateway));
                }
                catch (NetworkInformationException)
                {
                    // A disappearing adapter should not prevent another adapter being selected.
                }
            }

            AdapterCandidate? selected = candidates
                .OrderByDescending(candidate => GetCandidateScore(candidate, bestInterfaceIndex))
                .FirstOrDefault();

            return selected is null
                ? AdapterCapture.NoActiveAdapter
                : CreateAdapterCapture(selected);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is NetworkInformationException
            or SocketException
            or InvalidOperationException)
        {
            return AdapterCapture.Unavailable;
        }
    }

    private static AdapterCapture CreateAdapterCapture(AdapterCandidate candidate)
    {
        string? subnetMask = GetSubnetMask(candidate.Ipv4Address);
        string? gateway = candidate.Properties.GatewayAddresses
            .Select(static address => address.Address)
            .Where(static address => IsUsableAddress(address))
            .OrderByDescending(static address => address.AddressFamily == AddressFamily.InterNetwork)
            .Select(static address => address.ToString())
            .FirstOrDefault();
        string[] dnsServers = candidate.Properties.DnsAddresses
            .Where(static address => IsUsableAddress(address))
            .Select(static address => address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        byte[] physicalAddress = candidate.Interface.GetPhysicalAddress().GetAddressBytes();
        string? macAddress = physicalAddress.Length == 0
            ? null
            : string.Join(':', physicalAddress.Select(static value => value.ToString("X2")));

        return new AdapterCapture(
            Normalize(candidate.Interface.Name) ?? Normalize(candidate.Interface.Description),
            GetConnectionType(candidate.Interface.NetworkInterfaceType),
            candidate.Ipv4Address.Address.ToString(),
            subnetMask,
            gateway,
            dnsServers,
            macAddress,
            new DiagnosticResult(DiagnosticStatus.Good, "Connected"));
    }

    private static async Task<DiagnosticResult> CheckDnsAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(DnsTimeout);

        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(
                DnsTestHost,
                timeoutCancellation.Token).ConfigureAwait(false);

            return addresses.Length > 0
                ? new DiagnosticResult(DiagnosticStatus.Good, "Resolution succeeded")
                : new DiagnosticResult(DiagnosticStatus.Failed, "No DNS result");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DiagnosticResult(DiagnosticStatus.Failed, "Resolution timed out");
        }
        catch (SocketException)
        {
            return new DiagnosticResult(DiagnosticStatus.Failed, "Resolution failed");
        }
    }

    private async Task<DiagnosticResult> CheckInternetAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(InternetTimeout);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                InternetTestUrl,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCancellation.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? new DiagnosticResult(DiagnosticStatus.Good, "HTTPS connection succeeded")
                : new DiagnosticResult(DiagnosticStatus.Failed, $"HTTPS returned {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DiagnosticResult(DiagnosticStatus.Failed, "HTTPS connection timed out");
        }
        catch (HttpRequestException)
        {
            return new DiagnosticResult(DiagnosticStatus.Failed, "HTTPS connection failed");
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static int GetCandidateScore(AdapterCandidate candidate, uint? bestInterfaceIndex)
    {
        int score = candidate.Ipv4Index is int index
            && bestInterfaceIndex is uint bestIndex
            && index >= 0
            && (uint)index == bestIndex
                ? 1_000
                : 0;

        if (candidate.HasGateway)
        {
            score += 100;
        }

        score += candidate.Interface.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => 30,
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT => 25,
            NetworkInterfaceType.Ppp => 20,
            _ => 10,
        };

        return score;
    }

    private static uint? TryGetBestInterfaceIndex()
    {
        try
        {
            return GetBestInterface(PublicRouteProbeAddress, out uint interfaceIndex) == 0
                ? interfaceIndex
                : null;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    private static string? GetSubnetMask(UnicastIPAddressInformation address)
    {
        try
        {
            IPAddress mask = address.IPv4Mask;
            if (!mask.Equals(IPAddress.Any))
            {
                return mask.ToString();
            }

            int prefixLength = address.PrefixLength;
            if (prefixLength is < 0 or > 32)
            {
                return null;
            }

            byte[] maskBytes = new byte[4];
            for (int index = 0; index < maskBytes.Length; index++)
            {
                int remainingBits = Math.Clamp(prefixLength - (index * 8), 0, 8);
                maskBytes[index] = remainingBits == 0
                    ? (byte)0
                    : (byte)(0xFF << (8 - remainingBits));
            }

            return new IPAddress(maskBytes).ToString();
        }
        catch (Exception exception) when (exception is NetworkInformationException
            or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static string GetConnectionType(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT => "Ethernet",
        NetworkInterfaceType.Ppp => "PPP",
        _ => type.ToString(),
    };

    private static bool IsUsableAddress(IPAddress address) =>
        !address.Equals(IPAddress.Any)
        && !address.Equals(IPAddress.IPv6Any)
        && !IPAddress.IsLoopback(address);

    private static bool IsLinkLocal(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record AdapterCandidate(
        NetworkInterface Interface,
        IPInterfaceProperties Properties,
        UnicastIPAddressInformation Ipv4Address,
        int? Ipv4Index,
        bool HasGateway);

    private sealed record AdapterCapture(
        string? AdapterName,
        string? ConnectionType,
        string? Ipv4Address,
        string? SubnetMask,
        string? DefaultGateway,
        IReadOnlyList<string> DnsServers,
        string? MacAddress,
        DiagnosticResult Status)
    {
        public static AdapterCapture NoActiveAdapter { get; } = new(
            null,
            null,
            null,
            null,
            null,
            [],
            null,
            new DiagnosticResult(DiagnosticStatus.Failed, "No active adapter"));

        public static AdapterCapture Unavailable { get; } = new(
            null,
            null,
            null,
            null,
            null,
            [],
            null,
            new DiagnosticResult(DiagnosticStatus.Unavailable, "Adapter details unavailable"));
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetBestInterface(uint destinationAddress, out uint bestInterfaceIndex);
}
