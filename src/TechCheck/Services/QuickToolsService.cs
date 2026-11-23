using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace TechCheck.Services;

/// <summary>
/// The fixed set of native Windows tools that TechCheck can open.
/// </summary>
public enum QuickTool
{
    TaskManager,
    DeviceManager,
    EventViewer,
    NetworkConnections,
    WindowsSettings,
    WindowsUpdate,
    CommandPrompt,
    PowerShell,
}

/// <summary>
/// Opens a small, fixed allow-list of Windows administration tools.
/// </summary>
public sealed class QuickToolsService
{
    public void Launch(QuickTool tool)
    {
        ProcessStartInfo startInfo = CreateStartInfo(tool);
        using Process? process = Process.Start(startInfo);
    }

    public bool TryLaunch(QuickTool tool, out string? errorMessage)
    {
        try
        {
            Launch(tool);
            errorMessage = null;
            return true;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            errorMessage = $"Could not open {GetDisplayName(tool)}.";
            return false;
        }
    }

    public static string GetDisplayName(QuickTool tool) => tool switch
    {
        QuickTool.TaskManager => "Task Manager",
        QuickTool.DeviceManager => "Device Manager",
        QuickTool.EventViewer => "Event Viewer",
        QuickTool.NetworkConnections => "Network Connections",
        QuickTool.WindowsSettings => "Windows Settings",
        QuickTool.WindowsUpdate => "Windows Update",
        QuickTool.CommandPrompt => "Command Prompt",
        QuickTool.PowerShell => "PowerShell",
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Unknown quick tool."),
    };

    private static ProcessStartInfo CreateStartInfo(QuickTool tool) => tool switch
    {
        QuickTool.TaskManager => CreateSystemExecutableStartInfo("taskmgr.exe"),
        QuickTool.DeviceManager => CreateSystemExecutableStartInfo(
            "mmc.exe",
            GetRequiredSystemFile("devmgmt.msc")),
        QuickTool.EventViewer => CreateSystemExecutableStartInfo(
            "mmc.exe",
            GetRequiredSystemFile("eventvwr.msc")),
        QuickTool.NetworkConnections => CreateSystemExecutableStartInfo(
            "control.exe",
            GetRequiredSystemFile("ncpa.cpl")),
        QuickTool.WindowsSettings => CreateShellStartInfo("ms-settings:"),
        QuickTool.WindowsUpdate => CreateShellStartInfo("ms-settings:windowsupdate"),
        QuickTool.CommandPrompt => CreateSystemExecutableStartInfo("cmd.exe"),
        QuickTool.PowerShell => CreateSystemExecutableStartInfo(
            Path.Combine("WindowsPowerShell", "v1.0", "powershell.exe"),
            "-NoLogo"),
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Unknown quick tool."),
    };

    private static ProcessStartInfo CreateSystemExecutableStartInfo(
        string relativeExecutablePath,
        params string[] fixedArguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = GetRequiredSystemFile(relativeExecutablePath),
            UseShellExecute = true,
        };

        foreach (string argument in fixedArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string GetRequiredSystemFile(string relativePath)
    {
        string systemDirectory = GetTrustedSystemDirectory();
        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(Path.Combine(systemDirectory, relativePath));
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException)
        {
            throw new InvalidOperationException("A required Windows tool is unavailable.", exception);
        }

        string systemDirectoryPrefix =
            Path.TrimEndingDirectorySeparator(systemDirectory) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(systemDirectoryPrefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fullPath))
        {
            throw new InvalidOperationException("A required Windows tool is unavailable.");
        }

        return fullPath;
    }

    private static string GetTrustedSystemDirectory()
    {
        string systemDirectory = Environment.SystemDirectory;
        if (!string.IsNullOrWhiteSpace(systemDirectory) && Directory.Exists(systemDirectory))
        {
            return Path.GetFullPath(systemDirectory);
        }

        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string fallbackDirectory = Path.Combine(windowsDirectory, "System32");
        if (string.IsNullOrWhiteSpace(windowsDirectory) || !Directory.Exists(fallbackDirectory))
        {
            throw new InvalidOperationException("The Windows system directory is unavailable.");
        }

        return Path.GetFullPath(fallbackDirectory);
    }

    private static ProcessStartInfo CreateShellStartInfo(string target) => new()
    {
        FileName = target,
        UseShellExecute = true,
    };
}
