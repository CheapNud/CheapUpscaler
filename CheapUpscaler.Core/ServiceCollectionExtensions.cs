using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using CheapUpscaler.Core.Services.VapourSynth;
using CheapUpscaler.Core.Services.RIFE;
using CheapUpscaler.Core.Services.RealCUGAN;
using CheapUpscaler.Core.Services.RealESRGAN;
using CheapUpscaler.Core.Services.Upscaling;
using CheapHelpers.MediaProcessing.Services;

namespace CheapUpscaler.Core;

/// <summary>
/// Dependency injection extension methods for CheapUpscaler.Core services
/// </summary>
/// <remarks>
/// SVP Integration: RifeInterpolationService is configured via factory pattern to automatically
/// detect SVP 4 Pro installation and use its bundled RIFE, Python, and TensorRT components.
/// If SVP is not installed, paths will be empty and RIFE features will be unavailable.
/// Future: Add AppSettings integration for manual path configuration.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all CheapUpscaler AI upscaling services to the service collection
    /// </summary>
    public static IServiceCollection AddUpscalerServices(this IServiceCollection services)
    {
        // VapourSynth environment (shared by AI services)
        services.AddSingleton<IVapourSynthEnvironment, VapourSynthEnvironment>();

        // RIFE frame interpolation - configured via factory to get SVP paths
        services.AddTransient(CreateRifeService);
        services.AddTransient<RifeVariantDetector>();

        // AI upscaling services
        services.AddTransient<RealCuganService>();
        services.AddTransient<RealEsrganService>();

        // Non-AI upscaling
        services.AddTransient<NonAiUpscalingService>();

        return services;
    }

    /// <summary>
    /// Add only VapourSynth environment (minimal dependency for other services)
    /// </summary>
    public static IServiceCollection AddVapourSynthEnvironment(this IServiceCollection services)
    {
        services.AddSingleton<IVapourSynthEnvironment, VapourSynthEnvironment>();
        return services;
    }

    /// <summary>
    /// Add only RIFE services for frame interpolation
    /// </summary>
    public static IServiceCollection AddRifeServices(this IServiceCollection services)
    {
        services.AddSingleton<IVapourSynthEnvironment, VapourSynthEnvironment>();
        services.AddTransient(CreateRifeService);
        services.AddTransient<RifeVariantDetector>();
        return services;
    }

    /// <summary>
    /// Add only upscaling services (Real-ESRGAN, Real-CUGAN, Non-AI)
    /// </summary>
    public static IServiceCollection AddUpscalingServices(this IServiceCollection services)
    {
        services.AddSingleton<IVapourSynthEnvironment, VapourSynthEnvironment>();
        services.AddTransient<RealCuganService>();
        services.AddTransient<RealEsrganService>();
        services.AddTransient<NonAiUpscalingService>();
        return services;
    }

    /// <summary>
    /// Factory method to create RifeInterpolationService with SVP-detected paths
    /// </summary>
    /// <remarks>
    /// Detection priority:
    /// 1. SVP 4 Pro installation (includes RIFE, Python, TensorRT)
    /// 2. Future: AppSettings.ToolPaths.RifeFolderPath for manual configuration
    /// 3. Empty paths (RIFE features unavailable, will show error at runtime)
    /// </remarks>
    private static RifeInterpolationService CreateRifeService(IServiceProvider serviceProvider)
    {
        var svpDetection = serviceProvider.GetRequiredService<SvpDetectionService>();
        var svp = svpDetection.DetectSvpInstallation();

        string rifePath;
        string pythonPath;

        if (svp.IsInstalled && !string.IsNullOrEmpty(svp.RifePath))
        {
            rifePath = svp.RifePath;
            pythonPath = !string.IsNullOrEmpty(svp.PythonPath) ? svp.PythonPath : "";
            Debug.WriteLine($"[RIFE] Using SVP installation: {rifePath}");
        }
        else
        {
            // SVP not found - RIFE will not be available
            // TODO: Check AppSettings.ToolPaths.RifeFolderPath for manual configuration
            rifePath = "";
            pythonPath = "";
            Debug.WriteLine("[RIFE] WARNING: SVP not detected. RIFE frame interpolation will not be available.");
            Debug.WriteLine("[RIFE] Install SVP 4 Pro from https://www.svp-team.com/get/ for RIFE support.");
        }

        return new RifeInterpolationService(rifePath, pythonPath);
    }
}
