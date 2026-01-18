using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CheapUpscaler.Core.Services.VapourSynth;
using CheapUpscaler.Core.Services.RIFE;
using CheapUpscaler.Core.Services.RealCUGAN;
using CheapUpscaler.Core.Services.RealESRGAN;
using CheapUpscaler.Core.Services.Upscaling;
using CheapUpscaler.Core.Platform;
using CheapUpscaler.Shared.Platform;
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
    /// Add platform-specific services (IPlatformPaths, IToolLocator)
    /// Automatically detects Windows vs Linux and registers appropriate implementations
    /// </summary>
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IPlatformPaths, WindowsPlatformPaths>();
            services.AddSingleton<IToolLocator, WindowsToolLocator>();
        }
        else
        {
            services.AddSingleton<IPlatformPaths, LinuxPlatformPaths>();
            services.AddSingleton<IToolLocator, LinuxToolLocator>();
        }

        return services;
    }

    /// <summary>
    /// Add all CheapUpscaler AI upscaling services to the service collection
    /// </summary>
    public static IServiceCollection AddUpscalerServices(this IServiceCollection services)
    {
        // Platform-specific services
        services.AddPlatformServices();

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
    private static RifeInterpolationService CreateRifeService(IServiceProvider serviceProvider)
    {
        var svpDetection = serviceProvider.GetRequiredService<SvpDetectionService>();
        var (rifePath, pythonPath) = ResolveRifePaths(null, null, svpDetection);
        return new RifeInterpolationService(rifePath, pythonPath);
    }

    /// <summary>
    /// Resolves RIFE paths using provided configuration and SVP detection fallback.
    /// Shared logic used by both Core factory and Blazor settings-aware factory.
    /// </summary>
    /// <param name="configuredRifePath">User-configured RIFE path (null/empty = use auto-detection)</param>
    /// <param name="configuredPythonPath">User-configured Python path (null/empty = use SVP's Python)</param>
    /// <param name="svpDetection">SVP detection service for auto-detection fallback</param>
    /// <param name="logger">Optional logger for diagnostic output</param>
    /// <returns>Tuple of (rifePath, pythonPath) - may be empty if no RIFE found</returns>
    /// <remarks>
    /// Detection priority:
    /// 1. User-configured path (if provided and exists)
    /// 2. SVP 4 Pro installation (includes RIFE, Python, TensorRT)
    /// 3. Empty paths (RIFE features unavailable, will show error at runtime)
    /// </remarks>
    public static (string rifePath, string pythonPath) ResolveRifePaths(
        string? configuredRifePath,
        string? configuredPythonPath,
        SvpDetectionService svpDetection,
        ILogger? logger = null)
    {
        // 1. Check user-configured path first
        if (!string.IsNullOrEmpty(configuredRifePath))
        {
            if (Directory.Exists(configuredRifePath))
            {
                logger?.LogDebug("[RIFE] Using configured path: {RifePath}", configuredRifePath);
                return (configuredRifePath, configuredPythonPath ?? "");
            }
            logger?.LogWarning("[RIFE] Configured path does not exist: {RifePath}", configuredRifePath);
        }

        // 2. Fall back to SVP auto-detection
        var svp = svpDetection.DetectSvpInstallation();
        if (svp.IsInstalled && !string.IsNullOrEmpty(svp.RifePath))
        {
            var pythonPath = !string.IsNullOrEmpty(svp.PythonPath) ? svp.PythonPath : "";
            logger?.LogDebug("[RIFE] Using SVP installation: {RifePath}", svp.RifePath);
            return (svp.RifePath, pythonPath);
        }

        // 3. RIFE not available
        logger?.LogWarning("[RIFE] No RIFE installation found. Install SVP 4 Pro or configure RifeFolderPath in Settings.");
        return ("", "");
    }
}
