namespace CheapUpscaler.Shared.Models;

// Per-job settings serialized into UpscaleJob.SettingsJson.
// Property names ARE the wire format (PascalCase) - renaming them breaks persisted jobs
// and the REST API. Properties are settable because the MudBlazor settings panels edit
// these instances in place.

/// <summary>
/// RIFE frame interpolation job settings
/// </summary>
public record RifeJobSettings
{
    public int Multiplier { get; set; } = 2;
    public int TargetFps { get; set; } = 60;
    public string QualityPreset { get; set; } = "Medium";
}

/// <summary>
/// Real-CUGAN job settings
/// </summary>
public record RealCuganJobSettings
{
    public int NoiseLevel { get; set; } = -1;
    public int Scale { get; set; } = 2;
    public bool UseFp16 { get; set; } = true;
}

/// <summary>
/// Real-ESRGAN job settings
/// </summary>
public record RealEsrganJobSettings
{
    public string Model { get; set; } = "RealESRGAN_x4plus";
    public int Scale { get; set; } = 4;
    public int TileSize { get; set; } = 512;
    public bool UseFp16 { get; set; } = true;
    public bool UseTensorRT { get; set; } = false;
}

/// <summary>
/// Non-AI (traditional) upscaling job settings
/// </summary>
public record NonAiJobSettings
{
    public string Algorithm { get; set; } = "Lanczos";
    public int Scale { get; set; } = 2;
}
