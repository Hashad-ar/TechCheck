using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;
using TechCheck.Core;

namespace TechCheck.Services;

/// <summary>
/// Collects a single local system snapshot using built-in .NET and Windows APIs.
/// </summary>
public sealed class SystemDiagnosticsService
{
    private const string BiosRegistryPath = @"HARDWARE\DESCRIPTION\System\BIOS";
    private const string CpuRegistryPath = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
    private const string WindowsRegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private static readonly TimeSpan CpuSampleInterval = TimeSpan.FromMilliseconds(250);

    public async Task<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task<double?> cpuUsageTask = SampleCpuUsageAsync(cancellationToken);
        ComputerSnapshot computer = CaptureComputer();
        ProcessorSnapshot processor = new(
            ReadRegistryString(CpuRegistryPath, "ProcessorNameString"),
            Environment.ProcessorCount,
            await cpuUsageTask.ConfigureAwait(false));

        cancellationToken.ThrowIfCancellationRequested();
        return new SystemSnapshot(computer, processor, CaptureMemory(), CaptureStorage());
    }

    private static ComputerSnapshot CaptureComputer()
    {
        string? manufacturer = NormalizeHardwareValue(
            ReadRegistryString(BiosRegistryPath, "SystemManufacturer"));
        string? model = NormalizeHardwareValue(
            ReadRegistryString(BiosRegistryPath, "SystemProductName"));
        string? serialNumber = NormalizeHardwareValue(
            ReadRegistryString(BiosRegistryPath, "SystemSerialNumber"));

        return new ComputerSnapshot(
            Normalize(Environment.MachineName),
            manufacturer,
            model,
            serialNumber,
            GetWindowsVersion(),
            GetWindowsArchitecture(),
            GetCurrentUser(),
            GetUptime());
    }

    private static MemorySnapshot CaptureMemory()
    {
        MemoryStatusEx status = new();
        if (!GlobalMemoryStatusEx(ref status))
        {
            return new MemorySnapshot(null, null, null, null);
        }

        long? totalBytes = ToNullableInt64(status.TotalPhysical);
        long? availableBytes = ToNullableInt64(status.AvailablePhysical);
        if (totalBytes is null || availableBytes is null || availableBytes > totalBytes)
        {
            return new MemorySnapshot(totalBytes, null, availableBytes, null);
        }

        long usedBytes = totalBytes.Value - availableBytes.Value;
        double usagePercent = totalBytes.Value == 0
            ? 0
            : usedBytes * 100d / totalBytes.Value;

        return new MemorySnapshot(totalBytes, usedBytes, availableBytes, usagePercent);
    }

    private static StorageSnapshot CaptureStorage()
    {
        try
        {
            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string? driveRoot = Path.GetPathRoot(windowsDirectory);
            if (string.IsNullOrWhiteSpace(driveRoot))
            {
                return UnavailableStorage();
            }

            DriveInfo drive = new(driveRoot);
            if (!drive.IsReady)
            {
                return new StorageSnapshot(drive.Name, null, null, null, null);
            }

            long totalBytes = drive.TotalSize;
            long freeBytes = drive.AvailableFreeSpace;
            long usedBytes = Math.Max(0, totalBytes - freeBytes);
            double usagePercent = totalBytes == 0 ? 0 : usedBytes * 100d / totalBytes;

            return new StorageSnapshot(drive.Name, totalBytes, usedBytes, freeBytes, usagePercent);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or ArgumentException)
        {
            return UnavailableStorage();
        }
    }

    private static async Task<double?> SampleCpuUsageAsync(CancellationToken cancellationToken)
    {
        if (!TryGetSystemTimes(out CpuTimes firstSample))
        {
            return null;
        }

        await Task.Delay(CpuSampleInterval, cancellationToken).ConfigureAwait(false);

        if (!TryGetSystemTimes(out CpuTimes secondSample)
            || secondSample.Kernel < firstSample.Kernel
            || secondSample.User < firstSample.User
            || secondSample.Idle < firstSample.Idle)
        {
            return null;
        }

        ulong kernelDelta = secondSample.Kernel - firstSample.Kernel;
        ulong userDelta = secondSample.User - firstSample.User;
        ulong idleDelta = secondSample.Idle - firstSample.Idle;
        ulong totalDelta = kernelDelta + userDelta;

        if (totalDelta == 0 || idleDelta > totalDelta)
        {
            return null;
        }

        double usage = (totalDelta - idleDelta) * 100d / totalDelta;
        return Math.Clamp(usage, 0, 100);
    }

    private static bool TryGetSystemTimes(out CpuTimes times)
    {
        if (!GetSystemTimes(out NativeFileTime idle, out NativeFileTime kernel, out NativeFileTime user))
        {
            times = default;
            return false;
        }

        times = new CpuTimes(idle.Value, kernel.Value, user.Value);
        return true;
    }

    private static string? GetWindowsVersion()
    {
        string? productName = ReadRegistryString(WindowsRegistryPath, "ProductName");
        string? displayVersion = ReadRegistryString(WindowsRegistryPath, "DisplayVersion")
            ?? ReadRegistryString(WindowsRegistryPath, "ReleaseId");
        string? buildText = ReadRegistryString(WindowsRegistryPath, "CurrentBuildNumber")
            ?? ReadRegistryString(WindowsRegistryPath, "CurrentBuild");
        string? revisionText = ReadRegistryString(WindowsRegistryPath, "UBR");

        if (int.TryParse(buildText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int build)
            && build >= 22000
            && productName is not null)
        {
            productName = productName.Replace(
                "Windows 10",
                "Windows 11",
                StringComparison.OrdinalIgnoreCase);
        }

        string? buildLabel = buildText;
        if (buildText is not null && int.TryParse(revisionText, out int revision))
        {
            buildLabel = $"{buildText}.{revision.ToString(CultureInfo.InvariantCulture)}";
        }

        List<string> versionParts = [];
        if (productName is not null)
        {
            versionParts.Add(productName);
        }

        if (displayVersion is not null)
        {
            versionParts.Add(displayVersion);
        }

        string version = string.Join(' ', versionParts);
        if (buildLabel is not null)
        {
            version = string.IsNullOrWhiteSpace(version)
                ? $"Windows build {buildLabel}"
                : $"{version} (build {buildLabel})";
        }

        return Normalize(version) ?? Normalize(RuntimeInformation.OSDescription);
    }

    private static string GetWindowsArchitecture()
    {
        string architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "64-bit (x64)",
            Architecture.X86 => "32-bit (x86)",
            Architecture.Arm64 => "64-bit (ARM64)",
            Architecture.Arm => "32-bit (ARM)",
            Architecture.Wasm => "WebAssembly",
            Architecture.S390x => "s390x",
            Architecture.LoongArch64 => "64-bit (LoongArch64)",
            _ => RuntimeInformation.OSArchitecture.ToString(),
        };

        return architecture;
    }

    private static string? GetCurrentUser()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return Normalize(identity.Name);
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException)
        {
            return Normalize(Environment.UserName);
        }
    }

    private static TimeSpan? GetUptime()
    {
        long milliseconds = Environment.TickCount64;
        return milliseconds < 0 ? null : TimeSpan.FromMilliseconds(milliseconds);
    }

    private static string? ReadRegistryString(string subKeyPath, string valueName)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? subKey = baseKey.OpenSubKey(subKeyPath, writable: false);
            object? value = subKey?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return Normalize(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        catch (Exception exception) when (exception is SecurityException
            or UnauthorizedAccessException
            or IOException)
        {
            return null;
        }
    }

    private static string? NormalizeHardwareValue(string? value)
    {
        string? normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        return normalized.Equals("Default string", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("System Serial Number", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase)
            || normalized.All(static character => character is '0' or ' ' or '-')
                ? null
                : normalized;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static long? ToNullableInt64(ulong value) =>
        value <= long.MaxValue ? (long)value : null;

    private static StorageSnapshot UnavailableStorage() => new(null, null, null, null, null);

    private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeFileTime
    {
        private readonly uint _lowDateTime;
        private readonly uint _highDateTime;

        public ulong Value => ((ulong)_highDateTime << 32) | _lowDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out NativeFileTime idleTime,
        out NativeFileTime kernelTime,
        out NativeFileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
