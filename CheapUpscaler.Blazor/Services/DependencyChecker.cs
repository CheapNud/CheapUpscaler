using System.Diagnostics;
using System.Text.RegularExpressions;
using CheapUpscaler.Components.Models;
using CheapUpscaler.Components.Services;
using CheapUpscaler.Core.Services.VapourSynth;
using CheapUpscaler.Core.Services.RIFE;
using CheapHelpers.MediaProcessing.Services;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Checks for all required and optional dependencies for video upscaling
/// </summary>
public class DependencyChecker(
    IVapourSynthEnvironment vapourSynthEnvironment,
    ExecutableDetectionService executableDetectionService,
    SvpDetectionService svpDetectionService) : IDependencyChecker
{
    /// <summary>
    /// Known DLL file names for SVP and TensorRT detection
    /// </summary>
    private static class KnownDlls
    {
        public const string TensorRt = "vstrt.dll";
        public const string RifeVs = "rife_vs.dll";
        public const string Rife = "rife.dll";
        public const string NvInfer = "nvinfer.dll";
    }

    /// <summary>
    /// Check all dependencies and return overall status
    /// </summary>
    public async Task<DependencyStatus> CheckAllDependenciesAsync()
    {
        var dependencies = new List<DependencyInfo>();

        // Run checks in parallel for performance
        var tasks = new List<Task<DependencyInfo>>
        {
            CheckPythonAsync(),
            CheckVapourSynthAsync(),
            CheckFFmpegAsync(),
            CheckVsMlrtAsync(),
            CheckCudaAsync(),
            CheckTensorRtAsync(),
            CheckRifeAsync()
        };

        var results = await Task.WhenAll(tasks);
        dependencies.AddRange(results);

        return new DependencyStatus { AllDependencies = dependencies };
    }

    private async Task<DependencyInfo> CheckPythonAsync()
    {
        var info = new DependencyInfo
        {
            Name = "Python",
            Description = "Python interpreter for VapourSynth scripts",
            Category = DependencyCategory.Required,
            InstallInstructions = "Install Python 3.8+ from python.org or use SVP's bundled Python",
            DownloadUrl = "https://www.python.org/downloads/"
        };

        try
        {
            var isAvailable = await vapourSynthEnvironment.IsPythonAvailableAsync();
            info.IsInstalled = isAvailable;

            if (isAvailable)
            {
                info.Version = vapourSynthEnvironment.PythonVersion;
                info.Path = await vapourSynthEnvironment.GetPythonFullPathAsync();
            }
        }
        catch (Exception ex)
        {
            info.IsInstalled = false;
            info.ErrorMessage = ex.Message;
        }

        return info;
    }

    private async Task<DependencyInfo> CheckVapourSynthAsync()
    {
        var info = new DependencyInfo
        {
            Name = "VapourSynth",
            Description = "Video processing framework for AI upscaling",
            Category = DependencyCategory.Required,
            InstallInstructions = "Install VapourSynth from vapoursynth.com or use SVP's bundled version",
            DownloadUrl = "https://www.vapoursynth.com/"
        };

        try
        {
            var isAvailable = await vapourSynthEnvironment.IsVapourSynthAvailableAsync();
            info.IsInstalled = isAvailable;

            if (isAvailable)
            {
                info.Version = vapourSynthEnvironment.VapourSynthVersion;
                info.Path = vapourSynthEnvironment.VsPipePath;
            }
        }
        catch (Exception ex)
        {
            info.IsInstalled = false;
            info.ErrorMessage = ex.Message;
        }

        return info;
    }

    private Task<DependencyInfo> CheckFFmpegAsync()
    {
        var info = new DependencyInfo
        {
            Name = "FFmpeg",
            Description = "Video encoding/decoding for input/output processing",
            Category = DependencyCategory.Required,
            InstallInstructions = "Download FFmpeg from ffmpeg.org and add to PATH",
            DownloadUrl = "https://ffmpeg.org/download.html"
        };

        try
        {
            var ffmpegPath = executableDetectionService.DetectFFmpeg(useSvpEncoders: false, customPath: null);
            info.IsInstalled = ffmpegPath != null;
            info.Path = ffmpegPath;

            if (info.IsInstalled)
            {
                // Try to get version
                info.Version = GetFFmpegVersion(ffmpegPath!);
            }
        }
        catch (Exception ex)
        {
            info.IsInstalled = false;
            info.ErrorMessage = ex.Message;
        }

        return Task.FromResult(info);
    }

    private async Task<DependencyInfo> CheckVsMlrtAsync()
    {
        var info = new DependencyInfo
        {
            Name = "vs-mlrt",
            Description = "VapourSynth ML Runtime for Real-CUGAN and Real-ESRGAN",
            Category = DependencyCategory.Recommended,
            InstallInstructions = "Install via pip: pip install vsmlrt",
            DownloadUrl = "https://github.com/AmusementClub/vs-mlrt"
        };

        try
        {
            // Check if vsmlrt Python package is importable
            var (exitCode, _, _) = await vapourSynthEnvironment.RunPythonCommandAsync(
                "-c \"import vsmlrt; print(vsmlrt.__version__)\"",
                timeoutMs: 10000);

            info.IsInstalled = exitCode == 0;

            if (!info.IsInstalled)
            {
                // Alternative check - try importing from vs
                var (exitCode2, _, _) = await vapourSynthEnvironment.RunPythonCommandAsync(
                    "-c \"from vsmlrt import Backend; print('OK')\"",
                    timeoutMs: 10000);

                info.IsInstalled = exitCode2 == 0;
            }
        }
        catch (Exception ex)
        {
            info.IsInstalled = false;
            info.ErrorMessage = ex.Message;
        }

        return info;
    }

    private Task<DependencyInfo> CheckCudaAsync()
    {
        var info = new DependencyInfo
        {
            Name = "CUDA Toolkit",
            Description = "NVIDIA GPU acceleration for AI processing",
            Category = DependencyCategory.Recommended,
            InstallInstructions = "Install CUDA Toolkit from NVIDIA Developer site",
            DownloadUrl = "https://developer.nvidia.com/cuda-downloads"
        };

        try
        {
            // Check if nvcc or CUDA libraries are available
            var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
            info.IsInstalled = !string.IsNullOrEmpty(cudaPath) && Directory.Exists(cudaPath);

            if (info.IsInstalled)
            {
                info.Path = cudaPath;
                // Try to extract version from path (e.g., "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.0")
                var versionMatch = Regex.Match(cudaPath!, @"v(\d+\.\d+)");
                if (versionMatch.Success)
                {
                    info.Version = versionMatch.Groups[1].Value;
                }
            }
        }
        catch (Exception ex)
        {
            info.IsInstalled = false;
            info.ErrorMessage = ex.Message;
        }

        return Task.FromResult(info);
    }

    private Task<DependencyInfo> CheckTensorRtAsync()
    {
        var info = new DependencyInfo
        {
            Name = "TensorRT",
            Description = "NVIDIA inference optimizer for faster AI processing",
            Category = DependencyCategory.Optional,
            InstallInstructions = "Install SVP 4 Pro (includes TensorRT), or install standalone from NVIDIA",
            DownloadUrl = "https://www.svp-team.com/get/"
        };

        try
        {
            // 1. Check for SVP's bundled TensorRT first
            var svp = svpDetectionService.DetectSvpInstallation();
            if (svp.IsInstalled && !string.IsNullOrEmpty(svp.RifePath))
            {
                // SVP bundles TensorRT with RIFE - check for vstrt.dll
                var vstrtDll = Path.Combine(svp.RifePath, KnownDlls.TensorRt);
                if (File.Exists(vstrtDll))
                {
                    info.IsInstalled = true;
                    info.Path = svp.RifePath;
                    info.Version = "SVP Bundled";
                    Debug.WriteLine($"Found SVP TensorRT at: {svp.RifePath}");
                    return Task.FromResult(info);
                }
            }

            // 2. Check common standalone TensorRT installation paths
            var tensorRtPaths = new[]
            {
                Environment.GetEnvironmentVariable("TENSORRT_PATH"),
                @"C:\Program Files\NVIDIA\TensorRT",
                @"C:\TensorRT"
            };

            foreach (var tensorRtPath in tensorRtPaths.Where(p => !string.IsNullOrEmpty(p)))
            {
                if (Directory.Exists(tensorRtPath))
                {
                    info.IsInstalled = true;
                    info.Path = tensorRtPath;
                    Debug.WriteLine($"Found standalone TensorRT at: {tensorRtPath}");
                    break;
                }
            }

            // 3. Check if nvinfer.dll is in system PATH
            if (!info.IsInstalled)
            {
                var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [];
                foreach (var dir in pathDirs)
                {
                    if (File.Exists(Path.Combine(dir, KnownDlls.NvInfer)))
                    {
                        info.IsInstalled = true;
                        info.Path = dir;
                        Debug.WriteLine($"Found TensorRT in PATH: {dir}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            info.IsInstalled = false;
            info.ErrorMessage = ex.Message;
        }

        return Task.FromResult(info);
    }

    private Task<DependencyInfo> CheckRifeAsync()
    {
        var info = new DependencyInfo
        {
            Name = "RIFE",
            Description = "AI frame interpolation (SVP TensorRT or standalone)",
            Category = DependencyCategory.Optional,
            InstallInstructions = "Install SVP 4 Pro for TensorRT RIFE, or download rife-ncnn-vulkan",
            DownloadUrl = "https://www.svp-team.com/get/"
        };

        try
        {
            // 1. Check for SVP's integrated RIFE first (preferred)
            var svp = svpDetectionService.DetectSvpInstallation();
            if (svp.IsInstalled && !string.IsNullOrEmpty(svp.RifePath))
            {
                // Check for SVP RIFE DLLs
                var rifeDll = Path.Combine(svp.RifePath, KnownDlls.RifeVs);
                var rifeAltDll = Path.Combine(svp.RifePath, KnownDlls.Rife);

                if (File.Exists(rifeDll) || File.Exists(rifeAltDll))
                {
                    info.IsInstalled = true;
                    info.Path = svp.RifePath;
                    info.Version = "SVP TensorRT";
                    Debug.WriteLine($"Found SVP RIFE at: {svp.RifePath}");
                    return Task.FromResult(info);
                }
            }

            // 2. Fall back to standalone RIFE executables
            var rifePaths = new[]
            {
                @"C:\RIFE",
                @"C:\Program Files\RIFE",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RIFE")
            };

            foreach (var searchPath in rifePaths)
            {
                if (!Directory.Exists(searchPath)) continue;

                var variants = RifeVariantDetector.DetectAvailableVariants(searchPath);
                if (variants.Count > 0)
                {
                    info.IsInstalled = true;
                    info.Path = searchPath;
                    info.Version = string.Join(", ", variants);
                    Debug.WriteLine($"Found standalone RIFE at: {searchPath}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            info.IsInstalled = false;
            info.ErrorMessage = ex.Message;
        }

        return Task.FromResult(info);
    }

    private static string? GetFFmpegVersion(string ffmpegPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = process.StandardOutput.ReadLine();
            process.WaitForExit(5000);

            // Parse version from "ffmpeg version N.N.N ..."
            if (output != null)
            {
                var match = Regex.Match(output, @"version\s+(\S+)");
                if (match.Success) return match.Groups[1].Value;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the auto-detected RIFE folder path for use in Settings.
    /// Returns SVP's RIFE path if installed, or first found standalone RIFE location.
    /// </summary>
    public string? GetDetectedRifePath()
    {
        // 1. Check SVP installation first (preferred)
        var svp = svpDetectionService.DetectSvpInstallation();
        if (svp.IsInstalled && !string.IsNullOrEmpty(svp.RifePath))
        {
            var rifeDll = Path.Combine(svp.RifePath, KnownDlls.RifeVs);
            var rifeAltDll = Path.Combine(svp.RifePath, KnownDlls.Rife);
            if (File.Exists(rifeDll) || File.Exists(rifeAltDll))
            {
                return svp.RifePath;
            }
        }

        // 2. Check standalone RIFE locations
        var searchPaths = new[]
        {
            @"C:\RIFE",
            @"C:\Program Files\RIFE",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RIFE")
        };

        foreach (var searchPath in searchPaths)
        {
            if (Directory.Exists(searchPath) &&
                (File.Exists(Path.Combine(searchPath, "rife-ncnn-vulkan.exe")) ||
                 File.Exists(Path.Combine(searchPath, "rife-tensorrt.exe"))))
            {
                return searchPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the auto-detected Python path for use in Settings.
    /// </summary>
    public string? GetDetectedPythonPath()
    {
        var svp = svpDetectionService.DetectSvpInstallation();
        if (svp.IsInstalled && !string.IsNullOrEmpty(svp.PythonPath))
        {
            return svp.PythonPath;
        }

        return vapourSynthEnvironment.PythonPath;
    }
}
