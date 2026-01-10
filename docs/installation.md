# Installation Guide

Complete setup instructions for all AI processing features.

---

## Quick Start: Automated Dependency Manager

**The application includes a built-in Dependency Manager that automates installation and verification of all required dependencies.**

### Using the Dependency Manager (Recommended)

1. **Launch the Application**
2. **Navigate to Dependency Manager** (from the main menu)
3. **Review Dependency Status** - The manager automatically detects installed dependencies
4. **Install Missing Dependencies** - Click "Install Missing" or install individual dependencies
5. **Verify Installation** - Click "Refresh Status" to re-check after installation

The Dependency Manager handles:
- Automatic detection of installed tools (FFmpeg, VapourSynth)
- Automatic detection of SVP Python and VapourSynth installations
- Guided installation with multiple strategies (Chocolatey, portable, installer)
- Real-time verification of dependency versions and compatibility
- Integration with existing installations (detects SVP, etc.)

**For advanced users or manual installation, see the detailed instructions below.**

---

## Table of Contents
- [Quick Start: Automated Dependency Manager](#quick-start-automated-dependency-manager)
- [Feature Overview](#feature-overview)
  - [RIFE Frame Interpolation](#rife-frame-interpolation)
  - [Real-CUGAN AI Upscaling](#real-cugan-ai-upscaling)
  - [Real-ESRGAN AI Upscaling](#real-esrgan-ai-upscaling)
  - [Non-AI Upscaling](#non-ai-upscaling)
  - [VapourSynth Setup](#vapoursynth-setup)
- [Troubleshooting](#troubleshooting)
- [Hardware Requirements](#hardware-requirements)

---

## Feature Overview

This section provides brief overviews of each feature. For complete documentation, see the linked feature pages.

### RIFE Frame Interpolation

AI-powered frame interpolation to increase video frame rates (30fps → 60fps, etc.).

**Quick Start:**
- The Dependency Manager can automatically detect SVP installations and guide you through setup
- SVP 4 Pro includes TensorRT-accelerated RIFE (RTX 20 series or newer required)
- Alternative: Standalone Practical-RIFE with Python 3.8-3.11

**Available Models:**
- 4.6 to 4.26 (various quality/speed tradeoffs)
- Lite variants (4.22-lite, 4.25-lite) for faster processing
- UHD and Anime-specific models

**For complete RIFE documentation, see [RIFE Frame Interpolation](features/rife.md)**

### Real-CUGAN AI Upscaling

AI-powered video upscaling optimized for anime and cartoon content (720p → 1440p/4K). **10-13x faster** than Real-ESRGAN.

**Quick Start:**
- Requires VapourSynth, Python 3.8-3.11, and vsmlrt
- Dependency Manager automates installation and verification
- Works on CPU but GPU highly recommended (TensorRT/CUDA)

**Performance (RTX 3080):**
- TensorRT: 15-20 fps (3-4 min per min of video)
- CUDA: 10-15 fps (4-6 min per min of video)
- CPU OpenVINO: 2-3 fps (20-30 min per min of video)

**Backend Options:**
- TensorRT (fastest, RTX 20+ series)
- CUDA (fast, any NVIDIA GPU)
- CPU OpenVINO (fallback for any CPU)

**For complete Real-CUGAN documentation, see [Real-CUGAN AI Upscaling](features/real-cugan.md)**

### Real-ESRGAN AI Upscaling

AI-powered video upscaling for photorealistic content (720p → 1440p/4K).

**Quick Start:**
- Requires VapourSynth, Python 3.8-3.11, and vsrealesrgan
- Dependency Manager automates installation and verification
- Requires NVIDIA GPU (RTX 20 series or newer) with 6GB+ VRAM

**Available Models:**
- RealESRGAN_x4plus (general content, default)
- RealESRGAN_x4plus_anime_6B (anime/animation)
- RealESRGAN_x2plus (2x upscaling, fastest)
- realesr-general-x4v3 (includes denoising)
- RealESRGAN_AnimeVideo-v3 (temporal consistency)

**Performance Settings:**
- Tile Mode: 256px/512px/768px (lower = less VRAM, slower)
- FP16 Mode: 50% faster, half VRAM usage (recommended)
- TensorRT: Optional 2-3x speedup

**For complete Real-ESRGAN documentation, see [Real-ESRGAN AI Upscaling](features/real-esrgan.md)**

### Non-AI Upscaling

Ultra-fast upscaling alternatives using traditional algorithms. **100x faster** than AI upscaling.

**Quick Start:**
- Only requires FFmpeg (automatically detected or installable via Dependency Manager)
- No additional setup required - built into the app

**Available Algorithms:**
- **Lanczos**: General content, photos, smooth gradients (~10x real-time)
- **xBR**: Anime, cartoons, sharp edges (~5x real-time)
- **HQx**: Pixel art, sprites, retro game footage (~5x real-time)

**Performance Comparison (1-minute 1080p video, RTX 3080):**
- Real-ESRGAN: ~30 minutes
- Real-CUGAN: ~3-6 minutes (5-10x faster)
- Lanczos: ~6-12 seconds (150-300x faster)
- xBR/HQx: ~12-18 seconds (100-150x faster)

**When to Use:**
- Quick previews and iterations
- Batch processing where speed is critical
- Systems without high-end GPUs

**For complete Non-AI Upscaling documentation, see [Non-AI Upscaling](features/non-ai-upscaling.md)**

### VapourSynth Setup

Video processing framework required for RIFE, Real-CUGAN, and Real-ESRGAN processing.

**Quick Start:**
- Dependency Manager automates VapourSynth and source plugin installation
- Restart computer after installation (adds vspipe to PATH)
- VapourSynthEnvironment service automatically detects the installation

**Source Plugin Options:**
- **BestSource** (recommended)
- **L-SMASH Source** (alternative)
- **FFMS2** (another alternative)

**For complete VapourSynth documentation, see [VapourSynth Setup](features/vapoursynth.md)**

---

## Troubleshooting

**First Step: Use the Dependency Manager**
- Navigate to **Dependency Manager** in the application
- Click "Refresh Status" to re-check all dependencies
- Review the status and error messages for each dependency
- The DependencyChecker service provides detailed diagnostic information

### VapourSynth Errors

**"No attribute with the name bs/ffms2/lsmas exists"**
- Check Dependency Manager for "VapourSynth Source Plugin" status
- Install a source plugin (BestSource recommended) via Dependency Manager or manually
- Restart application after installing
- Check plugin is in correct folder

**"vspipe not found"**
- Check Dependency Manager for "VapourSynth" status
- Restart computer after VapourSynth installation
- Verify: `where vspipe` shows correct path
- Use Dependency Manager's "Refresh Status" to verify detection

### CUDA/GPU Errors

**"CUDA out of memory"**
- Enable Tile Mode in settings
- Reduce Tile Size (512→384→256)
- Enable FP16 mode
- Close other GPU applications

**"TensorRT initialization timeout"**
- First run takes 5-15 minutes (CUDA kernel compilation)
- Check Task Manager for GPU activity
- Update NVIDIA drivers

### Python Errors

**"vsrealesrgan not installed"**
```cmd
pip install --upgrade pip
pip install vsrealesrgan --force-reinstall
```

**"Module not found"**
- Verify Python version: `python --version` (should be 3.8-3.11)
- Reinstall with correct CUDA version:
  ```cmd
  pip install torch torchvision --index-url https://download.pytorch.org/whl/cu126
  ```

### Performance Issues

**Slow Processing:**
1. Enable FP16 mode (biggest improvement)
2. Increase Tile Size if VRAM allows
3. Disable denoising if not needed
4. Use lite models (4.22-lite, etc.)
5. Update NVIDIA drivers

---

## Hardware Requirements

### Minimum
- **GPU:** NVIDIA GTX 1060 (6GB)
- **CPU:** Quad-core processor
- **RAM:** 8GB
- **VRAM:** 4GB (with tiling)

### Recommended
- **GPU:** NVIDIA RTX 3060+ (8GB+)
- **CPU:** 6+ core processor
- **RAM:** 16GB
- **VRAM:** 8GB+

### Optimal
- **GPU:** NVIDIA RTX 4070+ (12GB+)
- **CPU:** 8+ core processor (Ryzen 9, i7/i9)
- **RAM:** 32GB
- **VRAM:** 12GB+

---

## Performance Benchmarks

### RTX 3080 (10GB VRAM)

| Operation | Speed | Notes |
|-----------|-------|-------|
| RIFE 30→60fps (1080p) | 120 fps | TensorRT accelerated |
| ESRGAN 720p→1440p | 6 fps | FP16, tile 512px |
| ESRGAN 1080p→4K | 3 fps | FP16, tile 384px |

### Processing Time (1-minute video on RTX 3080)

| Pipeline | Time | Stages |
|----------|------|--------|
| Source + RIFE | ~11min | Source → RIFE |
| Source + ESRGAN | ~11min | Source → ESRGAN |
| Source + RIFE + ESRGAN | ~21min | Source → RIFE → ESRGAN |

---

## Links

- **RIFE:** https://github.com/hzwer/Practical-RIFE
- **SVP:** https://www.svp-team.com
- **Real-ESRGAN:** https://github.com/xinntao/Real-ESRGAN
- **vsrealesrgan:** https://github.com/HolyWu/vs-realesrgan
- **VapourSynth:** https://github.com/vapoursynth/vapoursynth
- **PyTorch:** https://pytorch.org
- **TensorRT:** https://developer.nvidia.com/tensorrt
