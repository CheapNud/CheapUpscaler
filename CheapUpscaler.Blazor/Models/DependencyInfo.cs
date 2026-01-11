namespace CheapUpscaler.Blazor.Models;

/// <summary>
/// Information about a single dependency
/// </summary>
public class DependencyInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required DependencyCategory Category { get; init; }
    public bool IsInstalled { get; set; }
    public string? Version { get; set; }
    public string? Path { get; set; }
    public string? InstallInstructions { get; set; }
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Error message if detection failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

public enum DependencyCategory
{
    Required,
    Optional,
    Recommended
}
