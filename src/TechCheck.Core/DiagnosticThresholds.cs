namespace TechCheck.Core;

public static class DiagnosticThresholds
{
    public const double CpuWarningPercent = 80d;
    public const double CpuFailedPercent = 95d;

    public const double MemoryWarningPercent = 80d;
    public const double MemoryFailedPercent = 95d;

    public const double StorageWarningPercent = 85d;
    public const double StorageFailedPercent = 95d;
}
