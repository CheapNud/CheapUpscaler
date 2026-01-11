# Real-ESRGAN AI Upscaling

AI-powered video upscaling for photorealistic content (720p → 1440p/4K).

For general installation guidance, see [Installation Guide](../installation.md).

## Requirements
- **VapourSynth**
- **Python 3.8-3.11**
- **NVIDIA GPU** (RTX 20 series or newer)
- **6GB+ VRAM** (4GB minimum with tiling)

## Installation via Dependency Manager (Recommended)

1. Navigate to **Dependency Manager** in the application
2. Check status of "VapourSynth", "VapourSynth Source Plugin", and "Python 3.8-3.11"
3. Click "Install Missing" or install dependencies individually
4. The DependencyChecker service will verify Python and VapourSynth installation
5. Click "Refresh Status" to verify after installation

**Note:** The app automatically detects SVP's Python installation if available.

## Advanced/Manual Installation Steps

1. **Install VapourSynth** (see [VapourSynth Setup](vapoursynth.md))

2. **Install Python 3.11**
   - Download: https://www.python.org/downloads/
   - Check "Add Python to PATH" during installation

3. **Install vsrealesrgan**
   ```cmd
   pip install torch torchvision --index-url https://download.pytorch.org/whl/cu126
   pip install vsrealesrgan
   ```

4. **Install TensorRT (Optional, 2-3x faster)**
   ```cmd
   pip install torch_tensorrt tensorrt-cu12
   ```

5. **Verify**
   ```cmd
   python -c "import vsrealesrgan; print('vsrealesrgan OK')"
   ```

## Available Models

| Model | Scale | Best For | Speed |
|-------|-------|----------|-------|
| RealESRGAN_x4plus | 4x | General content (default) | Fast |
| RealESRGAN_x4plus_anime_6B | 4x | Anime/animation | Medium |
| RealESRGAN_x2plus | 2x | Moderate upscaling | Fastest |
| realesr-general-x4v3 | 4x | General + denoising | Medium |
| RealESRGAN_AnimeVideo-v3 | 4x | Anime with temporal consistency | Slow |

Models auto-download on first use (stored in `~/.cache/torch/hub/`).

## Performance Settings

**Tile Mode:**
- 256px: Low VRAM (2-4GB), slowest
- 512px: Moderate VRAM (6-8GB), balanced
- 768px: High VRAM (10-12GB), fastest

**FP16 Mode (Recommended):**
- 50% faster, half VRAM usage
- Minimal quality loss
- All RTX GPUs support it

## Performance Benchmarks

### RTX 3080 (10GB VRAM)

| Operation | Speed | Notes |
|-----------|-------|-------|
| ESRGAN 720p→1440p | 6 fps | FP16, tile 512px |
| ESRGAN 1080p→4K | 3 fps | FP16, tile 384px |

### Processing Time (1-minute video on RTX 3080)

| Pipeline | Time | Stages |
|----------|------|--------|
| Source + ESRGAN | ~11min | Source → ESRGAN |
| Source + RIFE + ESRGAN | ~21min | Source → RIFE → ESRGAN |

## Troubleshooting

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
4. Update NVIDIA drivers

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

## Links

- **Real-ESRGAN:** https://github.com/xinntao/Real-ESRGAN
- **vsrealesrgan:** https://github.com/HolyWu/vs-realesrgan
- **VapourSynth:** https://github.com/vapoursynth/vapoursynth
- **PyTorch:** https://pytorch.org
- **TensorRT:** https://developer.nvidia.com/tensorrt
