using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CheapUpscaler.Shared.Services;

/// <summary>
/// Process-level probing for external tools, shared by the desktop and worker hosts.
/// </summary>
public static class ToolProbe
{
    private const int ProbeTimeoutMs = 5000;

    /// <summary>
    /// Locate an ffmpeg binary in the well-known install locations for the current OS.
    /// Falls back to the bare command name when ffmpeg answers from PATH, or null when not found.
    /// </summary>
    public static string? FindFFmpeg()
    {
        string[] candidates = OperatingSystem.IsWindows()
            ?
            [
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", "ffmpeg.exe")
            ]
            : ["/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg"];

        var installed = candidates.FirstOrDefault(File.Exists);
        if (installed != null) return installed;

        // Nothing on disk - see if the OS resolves it from PATH (typical for Docker)
        var command = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        return RunsSuccessfully(command, "-version") ? command : null;
    }

    /// <summary>
    /// Read the version string out of "ffmpeg -version", or null when it cannot be determined.
    /// </summary>
    public static string? GetFFmpegVersion(string ffmpegPath)
    {
        var firstLine = ReadFirstLine(ffmpegPath, "-version");
        if (firstLine == null) return null;

        // Parse version from "ffmpeg version N.N.N ..."
        var match = Regex.Match(firstLine, @"version\s+(\S+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ReadFirstLine(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(NoWindow(fileName, arguments));
            if (process == null) return null;

            var firstLine = process.StandardOutput.ReadLine();
            KillIfStillRunning(process);
            return firstLine;
        }
        catch
        {
            return null;
        }
    }

    private static bool RunsSuccessfully(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(NoWindow(fileName, arguments));
            if (process == null) return false;

            return KillIfStillRunning(process) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Waits for exit and kills the process if it hangs. Returns true when it exited on its own.</summary>
    private static bool KillIfStillRunning(Process process)
    {
        if (process.WaitForExit(ProbeTimeoutMs)) return true;

        try { process.Kill(entireProcessTree: true); } catch { }
        return false;
    }

    private static ProcessStartInfo NoWindow(string fileName, string arguments) => new()
    {
        FileName = fileName,
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
}
