using System.Text.Json;

namespace CheapUpscaler.Shared.Models;

/// <summary>
/// Application settings for CheapUpscaler
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Shared serializer options for every settings file (desktop and worker), so files stay
    /// portable between hosts. Case-insensitive reads keep the older PascalCase worker files valid.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

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

    /// <summary>
    /// Path to RIFE folder (null = auto-detect from SVP installation).
    /// For SVP users: typically C:\Program Files (x86)\SVP 4\rife
    /// For standalone: path containing rife_vs.dll or inference_video.py
    /// </summary>
    public string? RifeFolderPath { get; set; }
}

/// <summary>
/// Default settings for each upscale type. Stores the same records the job dialog and the
/// settings panels edit, so seeding a new job is a plain record copy.
/// </summary>
public class DefaultUpscaleSettings
{
    public RifeJobSettings Rife { get; set; } = new();
    public RealCuganJobSettings RealCugan { get; set; } = new();
    public RealEsrganJobSettings RealEsrgan { get; set; } = new();
    public NonAiJobSettings NonAi { get; set; } = new();
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
}

/// <summary>
/// Queue behavior settings
/// </summary>
public class QueueSettings
{
    /// <summary>Maximum concurrent jobs (GPU limited, usually 1)</summary>
    public int MaxConcurrentJobs { get; set; } = 1;

    /// <summary>Default output directory (null = same as source)</summary>
    public string? DefaultOutputDirectory { get; set; }
}
