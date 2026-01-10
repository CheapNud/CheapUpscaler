# Real-CUGAN AI Upscaling

Real-CUGAN is an AI upscaling solution optimized for anime and cartoon content. It is **10-13x faster** than Real-ESRGAN, processing at ~10-20 fps on an RTX 3080 compared to ~1 fps with Real-ESRGAN.

## Quick Start: Use the Dependency Manager

**The application includes a built-in Dependency Manager that automates Real-CUGAN setup:**

1. Navigate to **Dependency Manager** in the application
2. Check status of required dependencies:
   - VapourSynth
   - VapourSynth Source Plugin
   - Python 3.8-3.11
3. Click "Install Missing" or install dependencies individually
4. The DependencyChecker service will verify installation and compatibility
5. Use "Refresh Status" to verify after installation

**The app automatically detects SVP's Python installation if available - no separate Python install needed in that case.**

For advanced users who prefer manual installation, see the detailed instructions below.

## Performance Comparison

| Feature | Real-ESRGAN | Real-CUGAN |
|---------|-------------|------------|
| **Speed (RTX 3080)** | ~0.5-1 fps | ~10-20 fps |
| **1-min video processing** | ~30 minutes | ~3-6 minutes |
| **Content Type** | Photorealistic | Anime/Cartoon |
| **Backend** | PyTorch + CUDA | TensorRT / CUDA / CPU |
| **Installation** | vsrealesrgan | vs-mlrt |

## Prerequisites

**Note:** All prerequisites can be installed and verified via the **Dependency Manager** in the application. Manual installation instructions are provided below for advanced users.

### 1. VapourSynth (Required)

VapourSynth is the video processing framework that hosts the AI upscaling plugins.

**Automated Installation:**
- Navigate to Dependency Manager → Check "VapourSynth" status
- Follow automated installation instructions
- VapourSynthEnvironment service will automatically detect the installation

**Manual Installation:**
- Download from: https://github.com/vapoursynth/vapoursynth/releases
- Install the latest R72+ release for Windows
- Ensure `vspipe.exe` is accessible in your PATH or install directory

**Verify Installation:**
```bash
vspipe --version
```

### 2. Python 3.8-3.11 (Required)

vs-mlrt requires Python to be installed and accessible.

**Automated Detection:**
- The DependencyChecker automatically detects:
  - System Python installations (from PATH)
  - SVP's bundled Python installation (if SVP is installed)
- No manual Python installation needed if SVP is installed

**Manual Installation:**
- Download from: https://www.python.org/downloads/
- **IMPORTANT:** Check "Add Python to PATH" during installation
- Supported versions: Python 3.8, 3.9, 3.10, or 3.11

**Verify Installation:**
```bash
python --version
```

### 3. VapourSynth Source Plugin (Required)

VapourSynth needs a source plugin to load video files.

**Automated Installation:**
- Navigate to Dependency Manager → Check "VapourSynth Source Plugin" status
- Follow automated installation instructions for BestSource (recommended)
- DependencyChecker will verify plugin installation

**Manual Installation - Choose one:**

**Option A: BestSource (Recommended)**
- Download from: https://github.com/vapoursynth/bestsource/releases
- Extract to VapourSynth plugins directory

**Option B: ffms2**
- Download from: https://github.com/FFMS/ffms2/releases
- Extract to VapourSynth plugins directory

**Option C: L-SMASH Source**
- Download from: https://github.com/HomeOfAviSynthPlusEvolution/L-SMASH-Works/releases
- Extract to VapourSynth plugins directory

**VapourSynth Plugins Directory:**
- Windows: `C:\Program Files\VapourSynth\plugins64\` or `%APPDATA%\VapourSynth\plugins64\`

## Real-CUGAN Installation

### Step 1: Install vs-mlrt Python Package

vs-mlrt (VapourSynth Machine Learning RunTime) provides the Real-CUGAN implementation with TensorRT/CUDA support.

```bash
pip install vsmlrt
```

**Or use the in-app installer:**
- Open Settings → Real-CUGAN Configuration
- Click "Install via pip" button
- Wait for installation to complete

### Step 2: Install TensorRT (Optional but Recommended)

TensorRT provides the best performance for Real-CUGAN on NVIDIA GPUs.

**For NVIDIA RTX GPUs (Turing, Ampere, Ada Lovelace):**

#### Option A: Pre-built Runtime (Recommended)
vs-mlrt can use TensorRT binaries that come with certain NVIDIA software:
- CUDA Toolkit 12.x+ (includes TensorRT runtime)
- Download from: https://developer.nvidia.com/cuda-downloads

#### Option B: Standalone TensorRT
1. Download TensorRT from: https://developer.nvidia.com/tensorrt
2. Extract to a permanent location (e.g., `C:\Program Files\TensorRT\`)
3. Add TensorRT's `lib` folder to your system PATH

**Verify TensorRT (optional):**
```bash
python -c "import tensorrt; print(tensorrt.__version__)"
```

**Note:** If TensorRT is not available, vs-mlrt will fall back to CUDA backend (still 8-10x faster than Real-ESRGAN).

### Step 3: Verify Installation

**Use the Dependency Manager (Recommended):**
1. Navigate to **Dependency Manager** in the application
2. Click **"Refresh Status"** to re-check all dependencies
3. Verify that "VapourSynth", "Python 3.8-3.11", and "VapourSynth Source Plugin" show as installed
4. The DependencyChecker will validate Python version compatibility and VapourSynth functionality

**Manual verification via command line:**
```bash
python -c "from vsmlrt import CUGAN, Backend; print('OK')"
```

## Configuration

### Backend Selection

Real-CUGAN supports three backends:

1. **TensorRT (Fastest, Recommended for RTX GPUs)**
   - Requires NVIDIA RTX GPU (Turing/Ampere/Ada Lovelace)
   - Requires TensorRT runtime
   - Performance: ~15-20 fps on RTX 3080
   - Best for: Maximum speed with RTX 2060 or newer

2. **CUDA (Fast, Compatible)**
   - Requires NVIDIA GPU with CUDA support
   - Uses ONNX Runtime with CUDA
   - Performance: ~10-15 fps on RTX 3080
   - Best for: NVIDIA GTX 1060 or newer

3. **CPU OpenVINO (Slow, Fallback)**
   - Works on any CPU (Intel, AMD)
   - No GPU required
   - Performance: ~2-3 fps on modern CPUs
   - Best for: Systems without NVIDIA GPU

### Recommended Settings

**For Anime Content (Default):**
- Noise: -1 (No Denoising) or 0 (Conservative)
- Scale: 2x
- Backend: TensorRT
- FP16: Enabled
- GPU Streams: 2

**For High-Quality Output:**
- Noise: 0 (Conservative Denoising)
- Scale: 2x (720p → 1440p)
- Backend: TensorRT
- FP16: Enabled
- GPU Streams: 2

**For Maximum Speed:**
- Noise: -1 (No Denoising)
- Scale: 2x
- Backend: TensorRT
- FP16: Enabled
- GPU Streams: 4 (RTX 3090/4090 only)

### Noise Level Guidelines

Real-CUGAN supports multiple denoising levels:

- **-1**: No denoising (pure upscaling, fastest)
- **0**: Conservative denoising (good for clean anime)
- **1**: Light denoising (2x scale only, good for slightly compressed videos)
- **2**: Medium denoising (2x scale only, good for compressed videos)
- **3**: Aggressive denoising (all scales, good for very compressed/noisy videos)

**Note:** Noise levels 1 and 2 only work with 2x scale. For 3x/4x scale, use noise -1, 0, or 3.

## Troubleshooting

**First Step: Use the Dependency Manager**
1. Navigate to **Dependency Manager** in the application
2. Click **"Refresh Status"** to re-check all dependencies
3. Review detailed status messages and error information
4. Use **"Install Missing"** to automatically fix dependency issues

### "vs-mlrt not installed" Error

**Via Dependency Manager:**
- Check Python 3.8-3.11 status in Dependency Manager
- Follow installation instructions if Python is missing or incompatible
- The app will detect SVP's Python automatically if available

**Manual Solution:**
```bash
pip install --upgrade vsmlrt
```

### "No VapourSynth source plugin found" Error

**Via Dependency Manager:**
- Check "VapourSynth Source Plugin" status in Dependency Manager
- Follow automated installation instructions
- DependencyChecker will verify plugin installation after setup

**Manual Solution:**
- Install BestSource, ffms2, or L-SMASH Source (see Prerequisites)
- Verify the plugin is in the VapourSynth plugins directory

### "vspipe.exe not found" Error

**Via Dependency Manager:**
- Check "VapourSynth" status in Dependency Manager
- Follow installation instructions if VapourSynth is missing
- Use "Refresh Status" after installation

**Manual Solution:**
- Reinstall VapourSynth
- Ensure VapourSynth installation directory is in PATH
- Manually add `C:\Program Files\VapourSynth\core\` to PATH

### Slow Performance (< 5 fps on RTX 3080)

**Possible causes:**
1. Not using TensorRT backend → Switch to TensorRT in settings
2. FP16 disabled → Enable FP16 mode in settings
3. GPU Streams = 1 → Increase to 2 (or 4 for RTX 3090/4090)
4. Using CPU backend → Install CUDA/TensorRT and switch to TensorRT backend

### "Model download failed" Error

**Solution:**
- Ensure internet connection is stable
- Models are auto-downloaded on first use (~50-100 MB)
- If download fails, try running the test again (Settings → Refresh Detection)
- Models are cached in: `%APPDATA%\vsmlrt\models\`

### "ImportError: DLL load failed" on Windows

**Solution:**
1. Install Visual C++ Redistributable 2019+ from Microsoft
2. Install CUDA Toolkit 12.x+ for CUDA backend support
3. Ensure Python and all dependencies are 64-bit versions

## Performance Tips

1. **Use TensorRT Backend**: 30-50% faster than CUDA backend
2. **Enable FP16**: 50% speed boost with minimal quality loss on RTX GPUs
3. **Optimize GPU Streams**:
   - RTX 3060/3070: Use 1-2 streams
   - RTX 3080/3090: Use 2-4 streams
   - RTX 4080/4090: Use 4 streams
4. **Choose Appropriate Scale**: 2x is fastest, 4x is slowest
5. **Disable Unnecessary Denoising**: Noise -1 is fastest

## When to Use Real-CUGAN vs Real-ESRGAN

**Use Real-CUGAN for:**
- Anime, cartoons, animated content
- When you need fast processing (10-20 fps)
- When you have limited time (3-6 minutes per minute of video)
- Content with cel-shaded or flat-colored art styles

**Use Real-ESRGAN for:**
- Photorealistic content (live-action videos, photographs)
- When quality is paramount over speed
- Content with complex textures and natural scenes
- When you can afford 30+ minutes per minute of video

## Additional Resources

- **vs-mlrt GitHub**: https://github.com/AmusementClub/vs-mlrt
- **Real-CUGAN Paper**: https://github.com/bilibili/ailab/tree/main/Real-CUGAN
- **VapourSynth Documentation**: https://www.vapoursynth.com/doc/
- **TensorRT**: https://developer.nvidia.com/tensorrt

## Support

For issues specific to Real-CUGAN:
- vs-mlrt Issues: https://github.com/AmusementClub/vs-mlrt/issues
- Real-CUGAN Issues: https://github.com/bilibili/ailab/issues

For issues with this application:
- **First step:** Check **Dependency Manager** for detailed status and error messages
- Use "Refresh Status" button to re-check all dependencies
- Enable Verbose Logging in Settings for detailed debug output
