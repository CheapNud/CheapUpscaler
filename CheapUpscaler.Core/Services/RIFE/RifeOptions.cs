using System.Text.RegularExpressions;

namespace CheapUpscaler.Core.Services.RIFE;

/// <summary>
/// Neural network engine for RIFE processing
/// </summary>
public enum RifeEngine
{
    TensorRT,
    Vulkan,
    NCNN
}

/// <summary>
/// Scene change detection method
/// </summary>
public enum SceneChangeDetection
{
    SvpMotionVectors,
    Disabled,
    ThresholdBased
}

/// <summary>
/// How to process scene changes
/// </summary>
public enum SceneChangeProcessing
{
    RepeatFrame,
    BlendFrames,
    InterpolateNormally
}

/// <summary>
/// Duplicate frames removal strategy
/// </summary>
public enum DuplicateFramesRemoval
{
    DoNotRemove,
    RemoveDuplicates,
    SmartDetection
}

/// <summary>
/// Configuration options for RIFE interpolation
/// Supports both standalone RIFE executables and SVP's integrated RIFE (VapourSynth-based)
/// </summary>
public class RifeOptions
{
    // === Core Engine Settings ===

    public RifeEngine Engine { get; set; } = RifeEngine.TensorRT;
    public int GpuThreads { get; set; } = 2;
    public string ModelName { get; set; } = "4.6";
    public int GpuId { get; set; } = 0;
    public List<string> AvailableGpus { get; set; } = [];

    // === Performance Settings ===

    public bool TtaMode { get; set; } = false;

    // === Scene Change Detection ===

    public SceneChangeDetection SceneDetection { get; set; } = SceneChangeDetection.SvpMotionVectors;
    public SceneChangeProcessing SceneProcessing { get; set; } = SceneChangeProcessing.BlendFrames;
    public DuplicateFramesRemoval DuplicateRemoval { get; set; } = DuplicateFramesRemoval.SmartDetection;

    // === Resolution Settings ===

    public int FrameHeight { get; set; } = 0;
    public bool UhdMode { get; set; } = false;
    public bool UhMode { get => UhdMode; set => UhdMode = value; }

    // === Legacy Settings ===

    public string ThreadConfig { get; set; } = "2:2:2";
    public int TileSize { get; set; } = 0;
    public int InterpolationPasses { get; set; } = 1;

    public string BuildArguments(string inputFolder, string outputFolder)
    {
        var args = new List<string>
        {
            $"-i \"{inputFolder}\"",
            $"-o \"{outputFolder}\"",
            $"-m {ModelName}",
            $"-g {GpuId}",
            $"-j {ThreadConfig}",
            $"-n {InterpolationPasses}"
        };

        if (UhMode) args.Add("-u");
        if (TtaMode) args.Add("-x");
        if (TileSize > 0) args.Add($"-t {TileSize}");

        return string.Join(" ", args);
    }

    public int GetFrameMultiplier() => (int)Math.Pow(2, InterpolationPasses);

    public int InterpolationMultiplier
    {
        get => GetFrameMultiplier();
        set => InterpolationPasses = (int)Math.Log2(value);
    }

    public int ModelNumber
    {
        get
        {
            var cleanName = ModelName.Replace("rife-v", "").Replace("rife-", "");
            var parts = Regex.Split(cleanName, @"[\._]");

            if (parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
            {
                var baseNum = major * (minor < 10 ? 10 : 100) + minor;
                var lowerName = cleanName.ToLower();
                if (lowerName.Contains("lite")) return baseNum * 10 + 1;
                if (lowerName.Contains("heavy")) return baseNum * 10 + 2;
                return baseNum;
            }

            var lowerModel = ModelName.ToLower();
            if (lowerModel.Contains("uhd") || lowerModel.Contains("anime")) return 46;
            return 46;
        }
        set
        {
            if (value % 10 == 1 && value > 100)
            {
                var baseNum = value / 10;
                ModelName = $"{baseNum / 100}.{baseNum % 100}-lite";
            }
            else if (value % 10 == 2 && value > 100)
            {
                var baseNum = value / 10;
                ModelName = $"{baseNum / 100}.{baseNum % 100}-heavy";
            }
            else if (value >= 100)
            {
                ModelName = $"{value / 100}.{value % 100}";
            }
            else
            {
                ModelName = $"{value / 10}.{value % 10}";
            }
        }
    }

    public int TargetFps { get; set; } = 60;
    public double Scale { get; set; } = 1.0;

    public static string[] GetAvailableModels() =>
    [
        "4.6", "4.14", "4.15", "4.17", "4.18", "4.20", "4.21", "4.22", "4.25", "4.26",
        "4.14-lite", "4.15-lite", "4.16-lite", "4.17-lite", "4.22-lite", "4.25-lite",
        "UHD", "anime"
    ];

    public static int[] GetAvailableFrameHeights() => [0, 576, 720, 1080, 1440, 2160];

    public static string GetFrameHeightDisplayName(int height) => height switch
    {
        0 => "Auto",
        576 => "576p (SD)",
        720 => "720p (HD)",
        1080 => "1080p (FHD)",
        1440 => "1440p (QHD)",
        2160 => "2160p (4K UHD)",
        _ => $"{height}p"
    };
}
