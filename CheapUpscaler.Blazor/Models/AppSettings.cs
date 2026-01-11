namespace CheapUpscaler.Blazor.Models;

/// <summary>
/// Application settings for CheapUpscaler
/// </summary>
public class AppSettings
{
    /// <summary>Tool path overrides (null = auto-detect)</summary>
    public ToolPaths ToolPaths { get; set; } = new();

    /// <summary>Default settings for each upscale type</summary>
    public DefaultUpscaleSettings DefaultSettings { get; set; } = new();

    /// <summary>UI preferences</summary>
    public UiSettings Ui { get; set; } = new();

    /// <summary>Queue behavior settings</summary>
    public QueueSettings Queue { get; set; } = new();
}

/// <summary>
/// Custom tool path overrides
/// </summary>
public class ToolPaths
{
    /// <summary>Path to VapourSynth installation (null = auto-detect)</summary>
    public string? VapourSynthPath { get; set; }

    /// <summary>Path to Python executable (null = auto-detect)</summary>
    public string? PythonPath { get; set; }

    /// <summary>Path to FFmpeg executable (null = auto-detect)</summary>
    public string? FFmpegPath { get; set; }

    /// <summary>Path to vspipe executable (null = auto-detect)</summary>
    public string? VspipePath { get; set; }
}

/// <summary>
/// Default settings for each upscale type
/// </summary>
public class DefaultUpscaleSettings
{
    public RifeDefaults Rife { get; set; } = new();
    public RealCuganDefaults RealCugan { get; set; } = new();
    public RealEsrganDefaults RealEsrgan { get; set; } = new();
    public NonAiDefaults NonAi { get; set; } = new();
}

public class RifeDefaults
{
    public int Multiplier { get; set; } = 2;
    public int TargetFps { get; set; } = 60;
    public string QualityPreset { get; set; } = "Medium";
}

public class RealCuganDefaults
{
    public int NoiseLevel { get; set; } = -1;
    public int Scale { get; set; } = 2;
    public bool UseFp16 { get; set; } = true;
}

public class RealEsrganDefaults
{
    public string Model { get; set; } = "RealESRGAN_x4plus";
    public int Scale { get; set; } = 4;
    public int TileSize { get; set; } = 512;
    public bool UseFp16 { get; set; } = true;
}

public class NonAiDefaults
{
    public string Algorithm { get; set; } = "Lanczos";
    public int Scale { get; set; } = 2;
}

/// <summary>
/// UI preferences
/// </summary>
public class UiSettings
{
    /// <summary>Use dark mode</summary>
    public bool DarkMode { get; set; } = true;

    /// <summary>Show notifications for completed jobs</summary>
    public bool ShowCompletionNotifications { get; set; } = true;

    /// <summary>Play sound on job completion</summary>
    public bool PlayCompletionSound { get; set; } = false;
}

/// <summary>
/// Queue behavior settings
/// </summary>
public class QueueSettings
{
    /// <summary>Maximum concurrent jobs (GPU limited, usually 1)</summary>
    public int MaxConcurrentJobs { get; set; } = 1;

    /// <summary>Auto-start queue when jobs are added</summary>
    public bool AutoStartQueue { get; set; } = false;

    /// <summary>Default output directory (null = same as source)</summary>
    public string? DefaultOutputDirectory { get; set; }
}
