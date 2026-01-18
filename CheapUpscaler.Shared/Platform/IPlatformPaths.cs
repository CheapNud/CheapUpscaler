namespace CheapUpscaler.Shared.Platform;

/// <summary>
/// Platform-agnostic path provider for tool locations
/// Abstracts Windows vs Linux path differences
/// </summary>
public interface IPlatformPaths
{
    /// <summary>Platform identifier (Windows, Linux, macOS)</summary>
    string PlatformName { get; }

    /// <summary>Executable file extension (.exe on Windows, empty on Linux)</summary>
    string ExecutableExtension { get; }

    /// <summary>Shared library extension (.dll on Windows, .so on Linux)</summary>
    string LibraryExtension { get; }

    /// <summary>Path separator character</summary>
    char PathSeparator { get; }

    /// <summary>Common vspipe search paths for this platform</summary>
    IEnumerable<string> GetVspipeSearchPaths();

    /// <summary>Common Python search paths for this platform</summary>
    IEnumerable<string> GetPythonSearchPaths();

    /// <summary>Common FFmpeg search paths for this platform</summary>
    IEnumerable<string> GetFFmpegSearchPaths();

    /// <summary>VapourSynth plugin directories for this platform</summary>
    IEnumerable<string> GetVapourSynthPluginPaths();

    /// <summary>Default model storage path</summary>
    string GetDefaultModelsPath();

    /// <summary>Application data directory</summary>
    string GetAppDataPath();

    /// <summary>
    /// Resolve command to full path using platform-specific lookup
    /// (where.exe on Windows, which on Linux)
    /// </summary>
    Task<string?> ResolveCommandPathAsync(string command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the full path for an executable with proper extension
    /// </summary>
    string GetExecutablePath(string basePath, string executableName);

    /// <summary>
    /// Get the full path for a library with proper extension
    /// </summary>
    string GetLibraryPath(string basePath, string libraryName);
}
