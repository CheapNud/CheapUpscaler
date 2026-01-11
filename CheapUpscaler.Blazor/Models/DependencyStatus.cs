namespace CheapUpscaler.Blazor.Models;

/// <summary>
/// Overall dependency status for the application
/// </summary>
public class DependencyStatus
{
    public List<DependencyInfo> AllDependencies { get; init; } = [];

    public IEnumerable<DependencyInfo> Required =>
        AllDependencies.Where(d => d.Category == DependencyCategory.Required);

    public IEnumerable<DependencyInfo> Optional =>
        AllDependencies.Where(d => d.Category == DependencyCategory.Optional);

    public IEnumerable<DependencyInfo> Recommended =>
        AllDependencies.Where(d => d.Category == DependencyCategory.Recommended);

    public bool AllRequiredInstalled =>
        Required.All(d => d.IsInstalled);

    public int InstalledCount =>
        AllDependencies.Count(d => d.IsInstalled);

    public int TotalCount =>
        AllDependencies.Count;

    public int RequiredInstalledCount =>
        Required.Count(d => d.IsInstalled);

    public int RequiredTotalCount =>
        Required.Count();

    /// <summary>
    /// Health percentage (0-100) based on required + weighted optional
    /// Required: 70% weight, Optional/Recommended: 30% weight
    /// </summary>
    public int HealthPercentage
    {
        get
        {
            if (TotalCount == 0) return 100;

            var requiredCount = RequiredTotalCount;
            var requiredInstalled = RequiredInstalledCount;
            var optionalCount = AllDependencies.Count(d => d.Category != DependencyCategory.Required);
            var optionalInstalled = AllDependencies.Count(d => d.Category != DependencyCategory.Required && d.IsInstalled);

            // Required contributes 70%, optional 30%
            double requiredScore = requiredCount > 0 ? (requiredInstalled / (double)requiredCount) * 70 : 70;
            double optionalScore = optionalCount > 0 ? (optionalInstalled / (double)optionalCount) * 30 : 30;

            return (int)Math.Round(requiredScore + optionalScore);
        }
    }

    public string SummaryMessage
    {
        get
        {
            if (AllRequiredInstalled && InstalledCount == TotalCount)
                return "All dependencies installed. Ready to process videos.";

            if (AllRequiredInstalled)
                return $"Core dependencies ready. {TotalCount - InstalledCount} optional dependencies missing.";

            var missingRequired = Required.Where(d => !d.IsInstalled).Select(d => d.Name);
            return $"Missing required: {string.Join(", ", missingRequired)}";
        }
    }
}
