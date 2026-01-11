namespace CheapUpscaler.Blazor.Models;

/// <summary>
/// Type of upscaling/processing to apply
/// </summary>
public enum UpscaleType
{
    /// <summary>RIFE frame interpolation (increase frame rate)</summary>
    Rife,

    /// <summary>Real-CUGAN AI upscaling (anime/cartoon optimized)</summary>
    RealCugan,

    /// <summary>Real-ESRGAN AI upscaling (general content)</summary>
    RealEsrgan,

    /// <summary>Non-AI upscaling (Lanczos, xBR, HQx)</summary>
    NonAi
}
