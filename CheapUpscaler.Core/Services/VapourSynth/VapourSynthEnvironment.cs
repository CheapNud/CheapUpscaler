using SysProcess = System.Diagnostics.Process;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CheapHelpers.MediaProcessing.Services;

namespace CheapUpscaler.Core.Services.VapourSynth;

/// <summary>
/// Manages VapourSynth environment detection and validation
/// Consolidates Python + vspipe detection for all AI services
/// Priority order: SVP Python > System PATH Python
/// </summary>
public class VapourSynthEnvironment : IVapourSynthEnvironment
{
    private readonly SvpDetectionService _svpDetection;
    private bool _isInitialized;
    private string _pythonPath = string.Empty;
    private string? _vspipePath;
    private bool _isUsingSvpPython;
    private string? _pythonVersion;
    private string? _vapourSynthVersion;

    public VapourSynthEnvironment(SvpDetectionService svpDetection)
    {
        _svpDetection = svpDetection;
    }

    public string PythonPath
    {
        get
        {
            EnsureInitialized();
            return _pythonPath;
        }
    }

    public string? VsPipePath
    {
        get
        {
            EnsureInitialized();
            return _vspipePath;
        }
    }

    public bool IsUsingSvpPython
    {
        get
        {
            EnsureInitialized();
            return _isUsingSvpPython;
        }
    }

    public string? PythonVersion
    {
        get
        {
            EnsureInitialized();
            return _pythonVersion;
        }
    }

    public string? VapourSynthVersion
    {
        get
        {
            EnsureInitialized();
            return _vapourSynthVersion;
        }
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        DetectEnvironment();
        _isInitialized = true;
    }

    private void DetectEnvironment()
    {
        Debug.WriteLine("=== VapourSynth Environment Detection ===");

        // 1. Try SVP's Python first (preferred)
        var svp = _svpDetection.DetectSvpInstallation();
        if (svp.IsInstalled && !string.IsNullOrEmpty(svp.PythonPath) && File.Exists(svp.PythonPath))
        {
            _pythonPath = svp.PythonPath;
            _isUsingSvpPython = true;
            Debug.WriteLine($"Using SVP's Python: {_pythonPath}");
        }
        else
        {
            // 2. Fall back to system PATH
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _pythonPath = IsPythonInPath("python") ? "python" :
                              IsPythonInPath("python3") ? "python3" : "python";
            }
            else
            {
                _pythonPath = "python3";
            }
            _isUsingSvpPython = false;
            Debug.WriteLine($"Using system PATH Python: {_pythonPath}");
        }

        // Detect vspipe
        _vspipePath = FindVsPipe();
        if (_vspipePath != null)
        {
            Debug.WriteLine($"Found vspipe: {_vspipePath}");
        }
        else
        {
            Debug.WriteLine("vspipe not found");
        }

        // Detect versions
        _pythonVersion = DetectPythonVersionSync();
        _vapourSynthVersion = DetectVapourSynthVersionSync();

        Debug.WriteLine($"Python version: {_pythonVersion ?? "unknown"}");
        Debug.WriteLine($"VapourSynth version: {_vapourSynthVersion ?? "unknown"}");
        Debug.WriteLine("==========================================");
    }

    private bool IsPythonInPath(string pythonCommand)
    {
        try
        {
            var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonCommand,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private string? FindVsPipe()
    {
        var possiblePaths = new[]
        {
            @"C:\Program Files\VapourSynth\core\vspipe.exe",
            @"C:\Program Files (x86)\VapourSynth\core\vspipe.exe",
            @"C:\Python311\Scripts\vspipe.exe",
            @"C:\Python310\Scripts\vspipe.exe",
            @"C:\Python39\Scripts\vspipe.exe",
            @"C:\Python38\Scripts\vspipe.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VapourSynth\core\vspipe.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Try to find in PATH
        try
        {
            var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "vspipe",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(2000);

            if (process.ExitCode == 0)
            {
                return "vspipe";
            }
        }
        catch { }

        return null;
    }

    private string? DetectPythonVersionSync()
    {
        try
        {
            var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var errorOutput = process.StandardError.ReadToEnd();
            process.WaitForExit(2000);

            var versionOutput = !string.IsNullOrWhiteSpace(output) ? output : errorOutput;

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(versionOutput))
            {
                var versionMatch = Regex.Match(versionOutput, @"Python ([\d\.]+)");
                return versionMatch.Success ? versionMatch.Groups[1].Value : versionOutput.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Python version detection failed: {ex.Message}");
        }

        return null;
    }

    private string? DetectVapourSynthVersionSync()
    {
        if (string.IsNullOrEmpty(_vspipePath))
            return null;

        try
        {
            var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _vspipePath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var firstLine = output.Split('\n')[0];
                var versionMatch = Regex.Match(firstLine, @"VapourSynth Video Processing Library\s+Copyright.*\s+(R\d+)");
                return versionMatch.Success ? versionMatch.Groups[1].Value : firstLine.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"VapourSynth version detection failed: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> IsPythonAvailableAsync()
    {
        EnsureInitialized();

        try
        {
            var (exitCode, _, _) = await RunPythonCommandAsync("--version", timeoutMs: 2000);
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsVapourSynthAvailableAsync()
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(_vspipePath))
            return false;

        try
        {
            var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _vspipePath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool isValid, string? errorMessage)> ValidateEnvironmentAsync()
    {
        EnsureInitialized();

        if (!await IsPythonAvailableAsync())
        {
            return (false, $"Python not available at: {_pythonPath}");
        }

        if (string.IsNullOrEmpty(_vspipePath))
        {
            return (true, "Python available, but VapourSynth (vspipe) not found. Some features may not work.");
        }

        if (!await IsVapourSynthAvailableAsync())
        {
            return (true, $"Python available, but VapourSynth not working at: {_vspipePath}");
        }

        return (true, null);
    }

    public async Task<(int exitCode, string output, string error)> RunPythonCommandAsync(string arguments, int timeoutMs = 5000)
    {
        EnsureInitialized();

        var process = new SysProcess
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            return (-1, string.Empty, "Process timed out");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        return (process.ExitCode, output, error);
    }

    public async Task<string?> GetPythonFullPathAsync()
    {
        EnsureInitialized();

        if (Path.IsPathRooted(_pythonPath))
            return _pythonPath;

        try
        {
            var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = _pythonPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var firstPath = output.Split('\n')[0].Trim();
                return File.Exists(firstPath) ? firstPath : null;
            }
        }
        catch { }

        return _pythonPath;
    }
}
