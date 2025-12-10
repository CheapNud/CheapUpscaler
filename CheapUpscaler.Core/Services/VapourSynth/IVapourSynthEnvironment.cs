namespace CheapUpscaler.Core.Services.VapourSynth;

/// <summary>
/// Provides access to VapourSynth environment (Python + vspipe)
/// Consolidates detection logic for all AI upscaling services
/// Priority: SVP Python > System PATH Python > Manual configuration
/// </summary>
public interface IVapourSynthEnvironment
{
    /// <summary>
    /// Path to Python executable (detected or configured)
    /// Priority: SVP's Python > System PATH > Manual
    /// </summary>
    string PythonPath { get; }

    /// <summary>
    /// Path to vspipe executable (VapourSynth command-line tool)
    /// </summary>
    string? VsPipePath { get; }

    /// <summary>
    /// Indicates if Python was found from SVP installation
    /// </summary>
    bool IsUsingSvpPython { get; }

    /// <summary>
    /// Python version string (e.g., "3.11.5")
    /// </summary>
    string? PythonVersion { get; }

    /// <summary>
    /// VapourSynth version string (e.g., "R65")
    /// </summary>
    string? VapourSynthVersion { get; }

    /// <summary>
    /// Check if Python is available and working
    /// </summary>
    Task<bool> IsPythonAvailableAsync();

    /// <summary>
    /// Check if VapourSynth (vspipe) is available and working
    /// </summary>
    Task<bool> IsVapourSynthAvailableAsync();

    /// <summary>
    /// Validate entire environment (Python + VapourSynth)
    /// </summary>
    Task<(bool isValid, string? errorMessage)> ValidateEnvironmentAsync();

    /// <summary>
    /// Run a Python command and capture output (for validation/testing)
    /// </summary>
    Task<(int exitCode, string output, string error)> RunPythonCommandAsync(string arguments, int timeoutMs = 5000);

    /// <summary>
    /// Get full path to Python executable (resolves "python" to actual path)
    /// </summary>
    Task<string?> GetPythonFullPathAsync();
}
