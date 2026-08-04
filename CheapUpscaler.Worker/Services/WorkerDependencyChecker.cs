using CheapUpscaler.Components.Models;
using CheapUpscaler.Components.Services;
using CheapUpscaler.Core.Services.VapourSynth;
using CheapUpscaler.Shared.Services;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Dependency checker optimized for Docker/headless environments.
/// Checks core dependencies: FFmpeg, VapourSynth, Python.
/// In Docker, most dependencies are pre-installed during image build.
/// </summary>
public class WorkerDependencyChecker(
    IVapourSynthEnvironment vapourSynthEnvironment,
    ILogger<WorkerDependencyChecker> logger) : IDependencyChecker
{
    public async Task<DependencyStatus> CheckAllDependenciesAsync()
    {
        var dependencies = new List<DependencyInfo>();

        // Run checks in parallel for performance
        var tasks = new List<Task<DependencyInfo>>
        {
            CheckFFmpegAsync(),
            CheckPythonAsync(),
            CheckVapourSynthAsync()
        };

        var results = await Task.WhenAll(tasks);
        dependencies.AddRange(results);

        return new DependencyStatus { AllDependencies = dependencies };
    }

    private Task<DependencyInfo> CheckFFmpegAsync()
    {
        var info = new DependencyInfo
        {
            Name = "FFmpeg",
            Description = "Video encoding/decoding for input/output processing",
            Category = DependencyCategory.Required,
            InstallInstructions = "apt-get install ffmpeg (Linux) or download from ffmpeg.org",
            DownloadUrl = "https://ffmpeg.org/download.html"
        };

        try
        {
            var ffmpegPath = ToolProbe.FindFFmpeg();
            info.IsInstalled = ffmpegPath != null;
            info.Path = ffmpegPath;

            if (info.IsInstalled)
            {
                info.Version = ToolProbe.GetFFmpegVersion(ffmpegPath!);
            }
        }
        catch (Exception ex)
        {
            info.IsInstalled = false;
            info.ErrorMessage = ex.Message;
            logger.LogWarning(ex, "Error checking FFmpeg");
        }

        return Task.FromResult(info);
    }

    private async Task<DependencyInfo> CheckPythonAsync()
    {
        var info = new DependencyInfo
        {
            Name = "Python",
            Description = "Python interpreter for VapourSynth scripts",
            Category = DependencyCategory.Required,
            InstallInstructions = "apt-get install python3 (Linux) or install Python 3.8+",
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
            logger.LogWarning(ex, "Error checking Python");
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
            InstallInstructions = "pip install vapoursynth",
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
            logger.LogWarning(ex, "Error checking VapourSynth");
        }

        return info;
    }
}
