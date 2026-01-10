# RIFE Frame Interpolation

AI-powered frame interpolation to increase video frame rates (30fps → 60fps, etc.).

For general installation guidance, see [Installation Guide](../installation.md).

**Note:** The Dependency Manager can automatically detect SVP installations and guide you through setup.

## Recommended: SVP's RIFE (TensorRT Accelerated)

**Requirements:**
- NVIDIA GPU (RTX 20 series or newer)
- SVP 4 Pro
- VapourSynth

**Installation via Dependency Manager:**
1. Navigate to **Dependency Manager** in the application
2. Check status of "SVP 4 Pro (RIFE TensorRT)" and "VapourSynth"
3. Follow automated installation instructions if missing
4. The app will automatically detect SVP's Python installation

**Manual Installation:**

1. **Install SVP 4 Pro**
   - Download: https://www.svp-team.com/get/
   - Select "RIFE AI engine" during installation
   - Installs to: `C:\Program Files (x86)\SVP 4\rife\`

2. **Install VapourSynth** (see [VapourSynth Setup](vapoursynth.md))

3. **Configure in App**
   - Settings → RIFE Folder Path
   - Browse to: `C:\Program Files (x86)\SVP 4\rife`

**Available Models:**
- 4.6 (balanced, default)
- 4.14-4.26 (various improvements)
- 4.22-lite, 4.25-lite (faster)
- UHD (for 4K+)
- Anime (optimized for animation)

## Alternative: Practical-RIFE (Standalone Python)

**Requirements:**
- Python 3.8-3.11
- NVIDIA GPU with CUDA
- 4GB+ VRAM

**Installation via Dependency Manager:**
1. Navigate to **Dependency Manager** in the application
2. Check status of "Python 3.8-3.11" and "Practical-RIFE"
3. Follow automated installation instructions

**Advanced/Manual Installation:**
```cmd
# Install Python 3.11 (check "Add to PATH")
# Then:
cd C:\
git clone https://github.com/hzwer/Practical-RIFE.git
cd Practical-RIFE

pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118
pip install opencv-python numpy
```

**Test:**
```cmd
python inference_video.py --video input.mp4 --multi 2
```

## Performance Benchmarks

### RTX 3080 (10GB VRAM)

| Operation | Speed | Notes |
|-----------|-------|-------|
| RIFE 30→60fps (1080p) | 120 fps | TensorRT accelerated |

### Processing Time (1-minute video on RTX 3080)

| Pipeline | Time | Stages |
|----------|------|--------|
| Source + RIFE | ~11min | Source → RIFE |
| Source + RIFE + ESRGAN | ~21min | Source → RIFE → ESRGAN |

## Troubleshooting

### Performance Issues

**Slow Processing:**
1. Enable FP16 mode (biggest improvement)
2. Use lite models (4.22-lite, etc.)
3. Update NVIDIA drivers

### TensorRT Errors

**"TensorRT initialization timeout"**
- First run takes 5-15 minutes (CUDA kernel compilation)
- Check Task Manager for GPU activity
- Update NVIDIA drivers

## Links

- **RIFE:** https://github.com/hzwer/Practical-RIFE
- **SVP:** https://www.svp-team.com
- **VapourSynth:** https://github.com/vapoursynth/vapoursynth
- **TensorRT:** https://developer.nvidia.com/tensorrt
