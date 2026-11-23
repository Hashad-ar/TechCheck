namespace TechCheck.Core;

public static class HealthEvaluator
{
    public static SystemHealthSnapshot Evaluate(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SystemHealthSnapshot(
            EvaluateCpu(snapshot.Processor.UsagePercent),
            EvaluateMemory(snapshot.Memory.UsagePercent),
            EvaluateStorage(snapshot.Storage.UsagePercent));
    }

    public static DiagnosticResult EvaluateCpu(double? usagePercent)
    {
        return EvaluateUsage(
            usagePercent,
            DiagnosticThresholds.CpuWarningPercent,
            DiagnosticThresholds.CpuFailedPercent);
    }

    public static DiagnosticResult EvaluateMemory(double? usagePercent)
    {
        return EvaluateUsage(
            usagePercent,
            DiagnosticThresholds.MemoryWarningPercent,
            DiagnosticThresholds.MemoryFailedPercent);
    }

    public static DiagnosticResult EvaluateStorage(double? usagePercent)
    {
        return EvaluateUsage(
            usagePercent,
            DiagnosticThresholds.StorageWarningPercent,
            DiagnosticThresholds.StorageFailedPercent);
    }

    public static DiagnosticResult EvaluateNetworkAdapter(CheckOutcome outcome)
    {
        return MapCheckOutcome(outcome, "Connected", "No active adapter");
    }

    public static DiagnosticResult EvaluateDns(CheckOutcome outcome)
    {
        return MapCheckOutcome(outcome, "Resolved", "Resolution failed");
    }

    public static DiagnosticResult EvaluateInternet(CheckOutcome outcome)
    {
        return MapCheckOutcome(outcome, "Connected", "Connection failed");
    }

    public static DiagnosticResult MapCheckOutcome(
        CheckOutcome outcome,
        string successMessage,
        string failureMessage,
        string unavailableMessage = DisplayFormatter.Unavailable)
    {
        return outcome switch
        {
            CheckOutcome.Succeeded => new DiagnosticResult(DiagnosticStatus.Good, successMessage),
            CheckOutcome.Failed => new DiagnosticResult(DiagnosticStatus.Failed, failureMessage),
            CheckOutcome.Unavailable => new DiagnosticResult(DiagnosticStatus.Unavailable, unavailableMessage),
            _ => new DiagnosticResult(DiagnosticStatus.Unavailable, unavailableMessage),
        };
    }

    public static double? CalculateUsagePercent(long? usedBytes, long? totalBytes)
    {
        if (usedBytes is null or < 0 || totalBytes is null or <= 0 || usedBytes > totalBytes)
        {
            return null;
        }

        return usedBytes.Value * 100d / totalBytes.Value;
    }

    private static DiagnosticResult EvaluateUsage(
        double? usagePercent,
        double warningThreshold,
        double failedThreshold)
    {
        if (!DisplayFormatter.IsValidPercent(usagePercent))
        {
            return new DiagnosticResult(DiagnosticStatus.Unavailable, DisplayFormatter.Unavailable);
        }

        var formattedPercent = DisplayFormatter.FormatPercent(usagePercent);

        if (usagePercent >= failedThreshold)
        {
            return new DiagnosticResult(
                DiagnosticStatus.Failed,
                $"Critical usage ({formattedPercent} used)");
        }

        if (usagePercent >= warningThreshold)
        {
            return new DiagnosticResult(
                DiagnosticStatus.Warning,
                $"High usage ({formattedPercent} used)");
        }

        return new DiagnosticResult(
            DiagnosticStatus.Good,
            $"Normal ({formattedPercent} used)");
    }
}
