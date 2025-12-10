namespace CheapUpscaler.Core.Services.RIFE;

/// <summary>
/// Progress tracking for video processing pipeline
/// </summary>
public class VideoProcessingProgress
{
    public enum ProcessingStage
    {
        Analyzing,
        ExtractingAudio,
        ExtractingFrames,
        InterpolatingFrames,
        ReassemblingVideo,
        Complete
    }

    public ProcessingStage CurrentStage { get; set; }
    public double StageProgress { get; set; }
    public double OverallProgress => CalculateOverallProgress();
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    public string CurrentStageDescription => CurrentStage switch
    {
        ProcessingStage.Analyzing => "Analyzing input video",
        ProcessingStage.ExtractingAudio => "Extracting audio track",
        ProcessingStage.ExtractingFrames => "Extracting frames from video",
        ProcessingStage.InterpolatingFrames => "Interpolating frames with RIFE AI",
        ProcessingStage.ReassemblingVideo => "Reassembling video with hardware encoder",
        ProcessingStage.Complete => "Processing complete",
        _ => "Processing"
    };

    private double CalculateOverallProgress()
    {
        return CurrentStage switch
        {
            ProcessingStage.Analyzing => 0 + (StageProgress * 0.02),
            ProcessingStage.ExtractingAudio => 2 + (StageProgress * 0.03),
            ProcessingStage.ExtractingFrames => 5 + (StageProgress * 0.15),
            ProcessingStage.InterpolatingFrames => 20 + (StageProgress * 0.60),
            ProcessingStage.ReassemblingVideo => 80 + (StageProgress * 0.20),
            ProcessingStage.Complete => 100,
            _ => 0
        };
    }

    public static VideoProcessingProgress Create(ProcessingStage stage, double stageProgress = 0)
    {
        return new VideoProcessingProgress
        {
            CurrentStage = stage,
            StageProgress = Math.Clamp(stageProgress, 0, 100)
        };
    }

    public override string ToString()
    {
        return $"{CurrentStageDescription}: {StageProgress:F1}% (Overall: {OverallProgress:F1}%)";
    }
}
