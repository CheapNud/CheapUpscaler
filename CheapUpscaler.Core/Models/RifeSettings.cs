namespace CheapUpscaler.Core.Models;

/// <summary>
/// Settings for RIFE frame interpolation
/// </summary>
public class RifeSettings
{
    /// <summary>
    /// Frame interpolation multiplier (2x, 4x, 8x)
    /// </summary>
    public int InterpolationMultiplier { get; set; } = 2;

    /// <summary>
    /// Target frames per second
    /// </summary>
    public int TargetFps { get; set; } = 60;

    /// <summary>
    /// Quality preset (draft, medium, high)
    /// </summary>
    public string QualityPreset { get; set; } = "medium";
}
