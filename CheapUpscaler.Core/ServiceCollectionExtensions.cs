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
        services.AddTransient(sp =>
        {
            var svpDetection = sp.GetRequiredService<SvpDetectionService>();
            var svp = svpDetection.DetectSvpInstallation();

            var rifePath = svp.IsInstalled && !string.IsNullOrEmpty(svp.RifePath)
                ? svp.RifePath
                : "";
            var pythonPath = svp.IsInstalled && !string.IsNullOrEmpty(svp.PythonPath)
                ? svp.PythonPath
                : "";

            return new RifeInterpolationService(rifePath, pythonPath);
        });
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
        services.AddTransient(sp =>
        {
            var svpDetection = sp.GetRequiredService<SvpDetectionService>();
            var svp = svpDetection.DetectSvpInstallation();

            var rifePath = svp.IsInstalled && !string.IsNullOrEmpty(svp.RifePath)
                ? svp.RifePath
                : "";
            var pythonPath = svp.IsInstalled && !string.IsNullOrEmpty(svp.PythonPath)
                ? svp.PythonPath
                : "";

            return new RifeInterpolationService(rifePath, pythonPath);
        });
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
}
