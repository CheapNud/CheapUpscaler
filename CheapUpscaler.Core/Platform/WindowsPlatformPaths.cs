using System.Diagnostics;
using CheapUpscaler.Shared.Platform;

namespace CheapUpscaler.Core.Platform;

/// <summary>
/// Windows-specific path resolution and tool detection
/// </summary>
public class WindowsPlatformPaths : IPlatformPaths
{
    public string PlatformName => "Windows";
    public string ExecutableExtension => ".exe";
    public string LibraryExtension => ".dll";
    public char PathSeparator => ';';

    public IEnumerable<string> GetVspipeSearchPaths() =>
    [
        @"C:\Program Files\VapourSynth\core",
        @"C:\Program Files (x86)\VapourSynth\core",
        @"C:\Python314\Scripts",
        @"C:\Python313\Scripts",
        @"C:\Python312\Scripts",
        @"C:\Python311\Scripts",
        @"C:\Python310\Scripts",
        @"C:\Python39\Scripts",
        @"C:\Python38\Scripts",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VapourSynth\core"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python311\Scripts"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python310\Scripts"),
    ];

    public IEnumerable<string> GetPythonSearchPaths() =>
    [
        @"C:\Python314",
        @"C:\Python313",
        @"C:\Python312",
        @"C:\Python311",
        @"C:\Python310",
        @"C:\Python39",
        @"C:\Python38",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python314"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python313"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python312"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python311"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python310"),
    ];

    public IEnumerable<string> GetFFmpegSearchPaths() =>
    [
        @"C:\Program Files (x86)\SVP 4\utils",
        @"C:\ffmpeg\bin",
        @"C:\Program Files\ffmpeg\bin",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"ffmpeg\bin"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ffmpeg\bin"),
    ];

    public IEnumerable<string> GetVapourSynthPluginPaths() =>
    [
        @"C:\Program Files\VapourSynth\plugins",
        @"C:\Program Files (x86)\VapourSynth\plugins",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VapourSynth\plugins"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"VapourSynth\plugins64"),
    ];

    public string GetDefaultModelsPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheapUpscaler", "models");

    public string GetAppDataPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheapUpscaler");

    public async Task<string?> ResolveCommandPathAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
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
                var firstPath = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                return File.Exists(firstPath) ? firstPath : null;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // where command failed
        }

        return null;
    }

    public string GetExecutablePath(string basePath, string executableName)
    {
        var name = executableName.EndsWith(ExecutableExtension, StringComparison.OrdinalIgnoreCase)
            ? executableName
            : executableName + ExecutableExtension;
        return Path.Combine(basePath, name);
    }

    public string GetLibraryPath(string basePath, string libraryName)
    {
        var name = libraryName.EndsWith(LibraryExtension, StringComparison.OrdinalIgnoreCase)
            ? libraryName
            : libraryName + LibraryExtension;
        return Path.Combine(basePath, name);
    }
}
