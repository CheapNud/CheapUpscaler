using SysProcess = System.Diagnostics.Process;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Core.Services.VapourSynth;

/// <summary>
/// Shared vspipe -> FFmpeg pipeline plumbing for all VapourSynth-based services.
/// Handles process lifetime (kill on cancellation AND on failure), y4m piping,
/// \r-based vspipe progress parsing, and audio/subtitle muxing from the source file.
/// </summary>
public static class VspipePipeline
{
    /// <summary>
    /// Quote a filesystem path as a Python string literal.
    /// Raw strings (r'...') break on apostrophes and trailing backslashes,
    /// so emit a regular literal with escaped backslashes and quotes.
    /// </summary>
    public static string PyQuote(string path) =>
        "'" + path.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

    /// <summary>
    /// Python snippet that reads the source color matrix from frame props instead of
    /// assuming BT.709. Falls back to a resolution-based guess (709 for HD, 601 for SD)
    /// when the source is unspecified. Must run after `clip` is loaded; conversions
    /// should then use matrix_in=_matrix / matrix=_matrix.
    /// </summary>
    public const string MatrixDetectSnippet = @"
_props = clip.get_frame(0).props
_matrix = _props.get('_Matrix', 2)
if _matrix in (0, 2, 3):
    _matrix = 1 if clip.height >= 720 else 5
";

    /// <summary>
    /// Build FFmpeg arguments that encode the piped y4m video AND mux audio
    /// (and subtitles, for mkv output) back in from the original source file.
    /// </summary>
    public static string BuildEncodeArguments(string sourceVideoPath, string outputVideoPath, string preset = "slow")
    {
        // Subtitle copy is only reliable for mkv containers; mp4 would need mov_text conversion
        var subtitleArgs = Path.GetExtension(outputVideoPath).Equals(".mkv", StringComparison.OrdinalIgnoreCase)
            ? "-map 1:s? -c:s copy "
            : "";

        return $"-i - -i \"{sourceVideoPath}\" -map 0:v:0 -map 1:a? -c:a copy {subtitleArgs}" +
               $"-map_metadata 1 -c:v libx264 -preset {preset} -crf 18 -pix_fmt yuv420p -y \"{outputVideoPath}\"";
    }

    /// <summary>
    /// Resolve the FFmpeg executable, preferring SVP's bundled build on Windows when none is given.
    /// </summary>
    public static string ResolveFfmpegPath(string? ffmpegPath)
    {
        if (!string.IsNullOrEmpty(ffmpegPath) && ffmpegPath != "ffmpeg")
            return ffmpegPath;

        const string svpFfmpeg = @"C:\Program Files (x86)\SVP 4\utils\ffmpeg.exe";
        return File.Exists(svpFfmpeg) ? svpFfmpeg : "ffmpeg";
    }

    /// <summary>
    /// Run `vspipe -p script - -c y4m | ffmpeg ...` to completion.
    /// Progress is parsed from vspipe stderr (\r-separated "Frame: X/Y" updates).
    /// Both child processes are killed on cancellation and on any failure path,
    /// so a dead ffmpeg can never leave vspipe orphaned on the GPU.
    /// </summary>
    public static async Task<(bool Success, int VspipeExitCode, int FfmpegExitCode)> RunAsync(
        string vspipePath,
        string scriptPath,
        string ffmpegPath,
        string ffmpegArguments,
        IProgress<double>? progress,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var vspipeStartInfo = new ProcessStartInfo
        {
            FileName = vspipePath,
            Arguments = $"-p \"{scriptPath}\" - -c y4m",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var ffmpegStartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = ffmpegArguments,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        logger?.LogDebug("Pipeline: {Vspipe} {VspipeArgs} | {Ffmpeg} {FfmpegArgs}",
            vspipePath, vspipeStartInfo.Arguments, ffmpegPath, ffmpegArguments);

        using var vspipe = SysProcess.Start(vspipeStartInfo);
        using var ffmpeg = SysProcess.Start(ffmpegStartInfo);

        if (vspipe == null || ffmpeg == null)
            throw new InvalidOperationException("Failed to start vspipe or ffmpeg process");

        using var vspipeKill = cancellationToken.Register(() => TryKill(vspipe, "vspipe", logger));
        using var ffmpegKill = cancellationToken.Register(() => TryKill(ffmpeg, "ffmpeg", logger));

        try
        {
            // Pipe vspipe stdout into ffmpeg stdin. If ffmpeg dies early the copy throws
            // a broken pipe; vspipe must then be killed or it blocks forever on a full pipe.
            var pipeTask = Task.Run(async () =>
            {
                try
                {
                    await vspipe.StandardOutput.BaseStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, cancellationToken);
                    ffmpeg.StandardInput.Close();
                }
                catch (OperationCanceledException)
                {
                    logger?.LogDebug("[pipeline] Pipe operation cancelled");
                }
                catch (Exception ex)
                {
                    logger?.LogWarning("[pipeline] Pipe broke ({Message}) - shutting down vspipe", ex.Message);
                    TryKill(vspipe, "vspipe", logger);
                }
            }, cancellationToken);

            // vspipe emits progress updates terminated by \r (not \n), so read char-wise
            var progressTask = Task.Run(async () =>
            {
                var framePattern = new Regex(@"Frame:\s*(\d+)/(\d+)");
                var buffer = new char[256];
                var lineBuilder = new System.Text.StringBuilder();
                var reader = vspipe.StandardError;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                    if (charsRead == 0) break;

                    for (int i = 0; i < charsRead; i++)
                    {
                        var c = buffer[i];
                        if (c == '\r' || c == '\n')
                        {
                            if (lineBuilder.Length > 0)
                            {
                                var line = lineBuilder.ToString();
                                logger?.LogDebug("[vspipe] {Line}", line);

                                var match = framePattern.Match(line);
                                if (match.Success &&
                                    int.TryParse(match.Groups[1].Value, out var current) &&
                                    int.TryParse(match.Groups[2].Value, out var total) &&
                                    total > 0)
                                {
                                    progress?.Report((double)current / total * 100);
                                }
                                lineBuilder.Clear();
                            }
                        }
                        else
                        {
                            lineBuilder.Append(c);
                        }
                    }
                }
            }, cancellationToken);

            var ffmpegMonitorTask = Task.Run(async () =>
            {
                string? line;
                while ((line = await ffmpeg.StandardError.ReadLineAsync(cancellationToken)) != null)
                {
                    logger?.LogDebug("[ffmpeg] {Line}", line);
                }
            }, cancellationToken);

            await Task.WhenAll(
                vspipe.WaitForExitAsync(cancellationToken),
                ffmpeg.WaitForExitAsync(cancellationToken),
                pipeTask,
                progressTask,
                ffmpegMonitorTask);

            var success = vspipe.ExitCode == 0 && ffmpeg.ExitCode == 0;
            if (!success)
            {
                logger?.LogError("Pipeline failed - vspipe exit: {VspipeExit}, ffmpeg exit: {FfmpegExit}",
                    vspipe.ExitCode, ffmpeg.ExitCode);
            }

            return (success, vspipe.ExitCode, ffmpeg.ExitCode);
        }
        finally
        {
            // Failure or cancellation must never leave a GPU-bound vspipe orphaned
            TryKill(vspipe, "vspipe", logger);
            TryKill(ffmpeg, "ffmpeg", logger);
        }
    }

    private static void TryKill(SysProcess targetProcess, string processName, ILogger? logger)
    {
        try
        {
            if (!targetProcess.HasExited)
            {
                logger?.LogDebug("[pipeline] Killing {ProcessName} (pid {Pid})", processName, targetProcess.Id);
                targetProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Already exited or inaccessible - nothing to do
        }
    }
}
