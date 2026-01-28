using CheapHelpers.MediaProcessing.Models;
using CheapHelpers.MediaProcessing.Services;
using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Desktop hardware detection service wrapper.
/// Wraps the CheapHelpers.MediaProcessing HardwareDetectionService.
/// </summary>
public class DesktopHardwareService(HardwareDetectionService hardwareDetection) : IHardwareService
{
    public Task<HardwareCapabilities> DetectHardwareAsync()
        => hardwareDetection.DetectHardwareAsync();
}
