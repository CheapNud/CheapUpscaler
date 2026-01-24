using System.Diagnostics;
using CheapUpscaler.Shared.Platform;

namespace CheapUpscaler.Core.Platform;

/// <summary>
/// Linux-specific path resolution and tool detection
/// Designed for both native Linux and Docker container environments
/// </summary>
public class LinuxPlatformPaths : IPlatformPaths
{
    public string PlatformName => "Linux";
    public string ExecutableExtension => string.Empty;
    public string LibraryExtension => ".so";
    public char PathSeparator => ':';

    public IEnumerable<string> GetVspipeSearchPaths() =>
    [
        "/usr/bin",
        "/usr/local/bin",
        "/opt/vapoursynth/bin",
        "/app/bin",  // Docker
        "/home/linuxbrew/.linuxbrew/bin",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/bin"),
    ];

    public IEnumerable<string> GetPythonSearchPaths() =>
    [
        "/usr/bin",
        "/usr/local/bin",
        "/opt/python/bin",
        "/app/bin",  // Docker
        "/home/linuxbrew/.linuxbrew/bin",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/bin"),
    ];

    public IEnumerable<string> GetFFmpegSearchPaths() =>
    [
        "/usr/bin",
        "/usr/local/bin",
        "/opt/ffmpeg/bin",
        "/app/bin",  // Docker
        "/home/linuxbrew/.linuxbrew/bin",
    ];

    public IEnumerable<string> GetVapourSynthPluginPaths() =>
    [
        "/usr/lib/vapoursynth",
        "/usr/local/lib/vapoursynth",
        "/opt/vapoursynth/lib/vapoursynth",
        "/app/lib/vapoursynth",  // Docker
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/lib/vapoursynth"),
    ];

    public string GetDefaultModelsPath()
    {
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdgDataHome))
        {
            return Path.Combine(xdgDataHome, "cheapupscaler", "models");
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/cheapupscaler/models");
    }

    public string GetAppDataPath()
    {
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdgDataHome))
        {
            return Path.Combine(xdgDataHome, "cheapupscaler");
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/cheapupscaler");
    }

    public async Task<string?> ResolveCommandPathAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var path = output.Trim();
                return File.Exists(path) ? path : null;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // which command failed
        }

        return null;
    }

    public string GetExecutablePath(string basePath, string executableName)
    {
        // Linux executables don't have extensions, but remove .exe if present
        var name = executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? executableName[..^4]
            : executableName;
        return Path.Combine(basePath, name);
    }

    public string GetLibraryPath(string basePath, string libraryName)
    {
        // Convert .dll to .so if needed
        var name = libraryName;
        if (libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            name = libraryName[..^4] + LibraryExtension;
        }
        else if (!libraryName.EndsWith(LibraryExtension, StringComparison.OrdinalIgnoreCase))
        {
            name = libraryName + LibraryExtension;
        }
        return Path.Combine(basePath, name);
    }
}
