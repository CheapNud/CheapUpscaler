# Non-AI Upscaling

Ultra-fast upscaling alternatives using traditional algorithms. **100x faster** than AI upscaling.

For general installation guidance, see [Installation Guide](../installation.md).

## Requirements
- **FFmpeg** (automatically detected by DependencyChecker or installable via Dependency Manager)

## Available Algorithms

### Lanczos
- **Best for**: General content, photos, smooth gradients
- **Speed**: ~10x real-time (6-12 seconds per minute of video)
- **Quality**: Smooth interpolation, good for most use cases

### xBR (Scale by Rules)
- **Best for**: Anime, cartoons, sharp edges
- **Speed**: ~5x real-time (12-18 seconds per minute of video)
- **Quality**: Pattern-recognition preserves edges and details

### HQx (High Quality magnification)
- **Best for**: Pixel art, sprites, retro game footage
- **Speed**: ~5x real-time (12-18 seconds per minute of video)
- **Quality**: Specialized magnification for pixel art

## Usage

Non-AI upscaling is built into the app with no additional setup required:

1. Open Render Queue → Add Render Job
2. Select video source
3. Enable "Non-AI Upscaling" toggle
4. Choose algorithm (Lanczos, xBR, or HQx)
5. Select scale factor (2x, 3x, or 4x)
6. Add to queue

## Performance Comparison

**Processing 1-minute 1080p video on RTX 3080:**

| Method | Time | Speed Advantage |
|--------|------|-----------------|
| Real-ESRGAN | ~30 minutes | 1x (baseline) |
| Real-CUGAN | ~3-6 minutes | 5-10x faster |
| Lanczos | ~6-12 seconds | 150-300x faster |
| xBR / HQx | ~12-18 seconds | 100-150x faster |

## When to Use

**Use Non-AI for:**
- Quick previews and iterations
- Batch processing where speed is critical
- Content where AI upscaling overhead isn't justified
- Systems without high-end GPUs

**Use AI (Real-CUGAN/ESRGAN) for:**
- Final high-quality outputs
- Content requiring detail enhancement
- When quality is more important than speed

## Links

- **FFmpeg:** https://ffmpeg.org
