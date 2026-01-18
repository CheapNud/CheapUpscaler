namespace CheapUpscaler.Shared.Platform;

/// <summary>
/// Platform-agnostic tool detection interface
/// Abstracts Windows-specific detection (WMI, Registry) from Linux alternatives
/// </summary>
public interface IToolLocator
{
    /// <summary>Detect Python installation</summary>
    Task<ToolInfo?> DetectPythonAsync(CancellationToken cancellationToken = default);

    /// <summary>Detect vspipe (VapourSynth CLI)</summary>
    Task<ToolInfo?> DetectVspipeAsync(CancellationToken cancellationToken = default);

    /// <summary>Detect FFmpeg</summary>
    Task<ToolInfo?> DetectFFmpegAsync(CancellationToken cancellationToken = default);

    /// <summary>Detect FFprobe</summary>
    Task<ToolInfo?> DetectFFprobeAsync(CancellationToken cancellationToken = default);

    /// <summary>Detect CUDA availability</summary>
    Task<bool> IsCudaAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Detect TensorRT availability</summary>
    Task<bool> IsTensorRtAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Get GPU information (name, VRAM, capabilities)</summary>
    Task<GpuInfo?> GetGpuInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Get all detected GPUs</summary>
    Task<IReadOnlyList<GpuInfo>> GetAllGpusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a detected tool
/// </summary>
public record ToolInfo(
    string Path,
    string? Version = null,
    bool IsValid = true
);

/// <summary>
/// Information about a detected GPU
/// </summary>
public record GpuInfo(
    string Name,
    long? VramMB = null,
    bool NvencAvailable = false,
    bool CudaAvailable = false,
    bool TensorRtAvailable = false,
    int DeviceIndex = 0
);
