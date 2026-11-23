using System.ComponentModel;
using System.Runtime.CompilerServices;
using TechCheck.Core;
using TechCheck.Services;

namespace TechCheck.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly string[] SectionTitles = ["Overview", "System", "Network", "Quick Tools"];

    private readonly SystemDiagnosticsService _systemDiagnostics;
    private readonly NetworkDiagnosticsService _networkDiagnostics;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _activeCheckCancellation;
    private DiagnosticSnapshot? _snapshot;
    private int _selectedSectionIndex;
    private bool _isChecking;
    private bool _disposed;
    private string _checkStatus = "Ready";

    public MainViewModel(
        SystemDiagnosticsService systemDiagnostics,
        NetworkDiagnosticsService networkDiagnostics)
    {
        _systemDiagnostics = systemDiagnostics;
        _networkDiagnostics = networkDiagnostics;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SelectedSectionIndex
    {
        get => _selectedSectionIndex;
        set
        {
            if (value < 0 || value >= SectionTitles.Length || !SetProperty(ref _selectedSectionIndex, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CurrentSectionTitle));
        }
    }

    public string CurrentSectionTitle => SectionTitles[SelectedSectionIndex];

    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }

    public string CheckStatus
    {
        get => _checkStatus;
        private set => SetProperty(ref _checkStatus, value);
    }

    public bool HasResults => _snapshot is not null;

    public string LastCheckedText => _snapshot is null
        ? string.Empty
        : $"Updated {_snapshot.GeneratedAt.ToLocalTime():HH:mm:ss}";

    public string DeviceName => Value(_snapshot?.System.Computer.ComputerName);

    public string WindowsSummary => Value(_snapshot?.System.Computer.WindowsVersion);

    public string WindowsFullName => WindowsSummary;

    public string WindowsArchitecture => Value(_snapshot?.System.Computer.WindowsArchitecture);

    public string CurrentUser => Value(_snapshot?.System.Computer.CurrentUser);

    public string Manufacturer => Value(_snapshot?.System.Computer.Manufacturer);

    public string Model => Value(_snapshot?.System.Computer.Model);

    public string SerialNumber => Value(_snapshot?.System.Computer.SerialNumber);

    public string UptimeText => DisplayFormatter.FormatCompactUptime(_snapshot?.System.Computer.Uptime);

    public string ProcessorName => Value(_snapshot?.System.Processor.Name);

    public string LogicalProcessorCount => DisplayFormatter.FormatCount(_snapshot?.System.Processor.LogicalProcessorCount);

    public string CpuUsageText => DisplayFormatter.FormatPercent(_snapshot?.System.Processor.UsagePercent);

    public string InstalledMemory => DisplayFormatter.FormatBytes(_snapshot?.System.Memory.TotalBytes);

    public string MemoryBreakdown =>
        $"{DisplayFormatter.FormatBytes(_snapshot?.System.Memory.UsedBytes)} used · " +
        $"{DisplayFormatter.FormatBytes(_snapshot?.System.Memory.AvailableBytes)} available";

    public string MemoryUsageText => DisplayFormatter.FormatPercent(_snapshot?.System.Memory.UsagePercent);

    public string StorageDriveAndTotal =>
        $"{Value(_snapshot?.System.Storage.DriveName)} · " +
        $"{DisplayFormatter.FormatBytes(_snapshot?.System.Storage.TotalBytes)} total";

    public string StorageBreakdown =>
        $"{DisplayFormatter.FormatBytes(_snapshot?.System.Storage.UsedBytes)} used · " +
        $"{DisplayFormatter.FormatBytes(_snapshot?.System.Storage.FreeBytes)} free";

    public string StorageUsageText => DisplayFormatter.FormatPercent(_snapshot?.System.Storage.UsagePercent);

    public DiagnosticStatus CpuStatus => _snapshot?.Health.Cpu.Status ?? DiagnosticStatus.Unavailable;

    public string CpuHealthText => _snapshot?.Health.Cpu.Message ?? DisplayFormatter.Unavailable;

    public DiagnosticStatus MemoryStatus => _snapshot?.Health.Memory.Status ?? DiagnosticStatus.Unavailable;

    public string MemoryHealthText => _snapshot?.Health.Memory.Message ?? DisplayFormatter.Unavailable;

    public DiagnosticStatus StorageStatus => _snapshot?.Health.Storage.Status ?? DiagnosticStatus.Unavailable;

    public string StorageHealthText => _snapshot?.Health.Storage.Message ?? DisplayFormatter.Unavailable;

    public string AdapterName => Value(_snapshot?.Network.AdapterName);

    public string ConnectionType => Value(_snapshot?.Network.ConnectionType);

    public string IPv4Address => Value(_snapshot?.Network.Ipv4Address);

    public string SubnetMask => Value(_snapshot?.Network.SubnetMask);

    public string DefaultGateway => Value(_snapshot?.Network.DefaultGateway);

    public string DnsServers => DisplayFormatter.FormatDnsServers(_snapshot?.Network.DnsServers);

    public string MacAddress => Value(_snapshot?.Network.MacAddress);

    public DiagnosticStatus AdapterStatus => _snapshot?.Network.AdapterStatus.Status ?? DiagnosticStatus.Unavailable;

    public string AdapterHealthText => _snapshot?.Network.AdapterStatus.Message ?? DisplayFormatter.Unavailable;

    public DiagnosticStatus DnsStatus => _snapshot?.Network.DnsStatus.Status ?? DiagnosticStatus.Unavailable;

    public string DnsHealthText => _snapshot?.Network.DnsStatus.Message ?? DisplayFormatter.Unavailable;

    public DiagnosticStatus InternetStatus => _snapshot?.Network.InternetStatus.Status ?? DiagnosticStatus.Unavailable;

    public string InternetHealthText => _snapshot?.Network.InternetStatus.Message ?? DisplayFormatter.Unavailable;

    public string? CreateReport() => _snapshot is null ? null : ReportGenerator.Generate(_snapshot);

    public async Task RunCheckAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsChecking)
        {
            return;
        }

        IsChecking = true;
        CheckStatus = "Checking…";

        using var checkCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _activeCheckCancellation = checkCancellation;

        try
        {
            Task<(SystemSnapshot Snapshot, bool HadError)> systemTask =
                CaptureSystemSafelyAsync(checkCancellation.Token);
            Task<(NetworkSnapshot Snapshot, bool HadError)> networkTask =
                CaptureNetworkSafelyAsync(checkCancellation.Token);

            await Task.WhenAll(systemTask, networkTask);
            checkCancellation.Token.ThrowIfCancellationRequested();

            var systemResult = await systemTask;
            var networkResult = await networkTask;
            var health = HealthEvaluator.Evaluate(systemResult.Snapshot);

            _snapshot = new DiagnosticSnapshot(
                DateTimeOffset.Now,
                systemResult.Snapshot,
                health,
                networkResult.Snapshot);

            CheckStatus = systemResult.HadError || networkResult.HadError
                ? "Check complete · some details unavailable"
                : "Check complete";
            OnPropertyChanged(string.Empty);
        }
        catch (OperationCanceledException) when (checkCancellation.IsCancellationRequested)
        {
            CheckStatus = _disposed ? "Closing…" : "Check cancelled";
        }
        finally
        {
            if (ReferenceEquals(_activeCheckCancellation, checkCancellation))
            {
                _activeCheckCancellation = null;
            }

            IsChecking = false;
        }
    }

    public void SetStatus(string message)
    {
        if (!_disposed && !string.IsNullOrWhiteSpace(message))
        {
            CheckStatus = message;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activeCheckCancellation?.Cancel();
        _lifetimeCancellation.Cancel();
        _networkDiagnostics.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private async Task<(SystemSnapshot Snapshot, bool HadError)> CaptureSystemSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return (await _systemDiagnostics.CaptureAsync(cancellationToken), false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return (CreateUnavailableSystemSnapshot(), true);
        }
    }

    private async Task<(NetworkSnapshot Snapshot, bool HadError)> CaptureNetworkSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return (await _networkDiagnostics.CaptureAsync(cancellationToken), false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return (CreateUnavailableNetworkSnapshot(), true);
        }
    }

    private static SystemSnapshot CreateUnavailableSystemSnapshot() => new(
        new ComputerSnapshot(null, null, null, null, null, null, null, null),
        new ProcessorSnapshot(null, null, null),
        new MemorySnapshot(null, null, null, null),
        new StorageSnapshot(null, null, null, null, null));

    private static NetworkSnapshot CreateUnavailableNetworkSnapshot()
    {
        var unavailable = new DiagnosticResult(DiagnosticStatus.Unavailable, DisplayFormatter.Unavailable);
        return new NetworkSnapshot(null, null, null, null, null, null, null, unavailable, unavailable, unavailable);
    }

    private static string Value(string? value) => DisplayFormatter.ValueOrUnavailable(value);

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
