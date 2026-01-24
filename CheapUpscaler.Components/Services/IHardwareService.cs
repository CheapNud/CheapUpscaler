using CheapHelpers.MediaProcessing.Models;

namespace CheapUpscaler.Components.Services;

/// <summary>
/// Abstraction for hardware detection service.
/// Desktop implementations use HardwareDetectionService, web uses a stub.
/// </summary>
public interface IHardwareService
{
    /// <summary>
    /// Detect hardware capabilities of the system
    /// </summary>
    Task<HardwareCapabilities> DetectHardwareAsync();
}
