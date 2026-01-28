using CheapUpscaler.Components.Models;

namespace CheapUpscaler.Components.Services;

/// <summary>
/// Service for checking external dependency status
/// </summary>
public interface IDependencyChecker
{
    /// <summary>
    /// Check all dependencies and return their status
    /// </summary>
    Task<DependencyStatus> CheckAllDependenciesAsync();
}
