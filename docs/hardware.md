# Hardware Acceleration Guide

Performance optimization for RTX 3080 + Ryzen 9 5900X (and similar systems).

---

## Critical: GPU vs CPU

### GPU Encoding: FFmpeg (RIFE/Upscaling Workflow)

**Why FFmpeg's NVENC Rocks:**
- RTX 3080 NVENC: ~500 fps @ 1080p HEVC
- Ryzen 9 5900X libx265: ~30-60 fps
- **8-10x faster encoding**
- 4% CPU usage vs 100% all cores

**For RTX 3080:** Let that beast flex
- Dedicated hardware encoder
- Minimal CPU overhead
- Perfect for RIFE frame reassembly

```bash
# GPU rendering with FFmpeg
ffmpeg -framerate 60 -i frame_%06d.png -i audio.m4a \
  -c:v hevc_nvenc -preset p7 -rc vbr -cq 19 \
  -c:a copy output.mp4
```

---

## Performance Comparison

### Scenario: Reassemble RIFE Frames (10,000 frames)

| Method | Time | CPU Usage | GPU Usage |
|--------|------|-----------|-----------|
| FFmpeg + libx265 | ~30 min | 12 cores @ 100% | 0% |
| FFmpeg + NVENC | ~3 min | 1 core @ 20% | 95% |

**Winner: NVENC** (10x faster, FFmpeg implementation is solid)

---

## Configuration

### For AI Upscaling Pipeline (GPU)

```csharp
public class FFmpegRenderSettings
{
    // RTX 3080 - HELL YES USE IT
    public bool UseHardwareAcceleration { get; set; } = true; // ALWAYS TRUE

    // NVENC settings
    public string VideoCodec { get; set; } = "hevc_nvenc"; // or "h264_nvenc"
    public string NvencPreset { get; set; } = "p7"; // p1=fast, p7=slow/best
    public string RateControl { get; set; } = "vbr"; // vbr or cq
    public int Quality { get; set; } = 19; // 18-23 recommended

    // CPU barely used
    public int ThreadCount { get; set; } = 2; // Just for demux/mux
}
```

---

## NVENC Presets

### HEVC (H.265) NVENC

| Preset | Speed | Quality | Best For |
|--------|-------|---------|----------|
| p1 | Fastest | Low | Quick previews |
| p4 | Fast | Medium | Balanced encoding |
| p7 | Slowest | Best | Final output |

### H.264 NVENC

| Preset | Speed | Quality | Best For |
|--------|-------|---------|----------|
| fast | Fastest | Low | Quick previews |
| medium | Balanced | Medium | General use |
| slow | Slowest | Best | Final output |

**Recommended:** Use `hevc_nvenc` with `p7` preset for RIFE reassembly

---

## Rate Control Modes

### VBR (Variable Bitrate)
- **Best for:** General purpose, file size matters
- **Quality:** Excellent
- **Setting:** `-rc vbr -cq 19`
- **File size:** Predictable

### CQ (Constant Quality)
- **Best for:** Maximum quality, file size doesn't matter
- **Quality:** Best
- **Setting:** `-rc constqp -qp 19`
- **File size:** Variable

### CBR (Constant Bitrate)
- **Best for:** Streaming
- **Quality:** Variable
- **Setting:** `-rc cbr -b:v 10M`
- **File size:** Fixed

**Recommended:** VBR mode with CQ 19-23 for RIFE

---

## Quality Settings

### CRF/CQ Values (Lower = Better Quality)

| Value | Quality | File Size | Use Case |
|-------|---------|-----------|----------|
| 15-17 | Visually lossless | Huge | Archival |
| 18-20 | Excellent | Large | High quality masters |
| 21-23 | Very good | Medium | Normal use |
| 24-26 | Good | Small | Web/streaming |
| 27-30 | Acceptable | Very small | Low priority |

**Recommended:** CQ 19-21 for RIFE-interpolated content

---

## Hardware Detection

```csharp
public class HardwareCapabilities
{
    public bool HasNvidiaGpu { get; set; }
    public string GpuModel { get; set; }
    public int CpuCoreCount { get; set; }
    public bool NvencAvailable { get; set; }

    public bool ShouldUseFFmpegNvenc => NvencAvailable; // ALWAYS if available

    public string EstimatedRenderTime(TimeSpan duration, bool useNvenc)
    {
        if (useNvenc && NvencAvailable)
        {
            // NVENC: ~500fps @ 1080p HEVC
            var seconds = duration.TotalSeconds * 1.2; // 20% overhead
            return TimeSpan.FromSeconds(seconds);
        }
        else
        {
            // CPU: ~30-60fps @ 1080p libx265
            var multiplier = CpuCoreCount / 12.0; // Normalize to 12-core
            var seconds = duration.TotalSeconds * 20 / multiplier;
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
```

---

## VRAM Requirements

### RIFE Interpolation

| Resolution | Model | VRAM Usage |
|------------|-------|------------|
| 720p | 4.6 | 2-3 GB |
| 1080p | 4.6 | 4-6 GB |
| 1440p | 4.6 | 6-8 GB |
| 4K | UHD | 8-10 GB |

### Real-ESRGAN Upscaling

| Input → Output | Tile Size | FP16 | VRAM Usage |
|----------------|-----------|------|------------|
| 720p → 1440p | 512px | Yes | 3-4 GB |
| 720p → 4K | 512px | Yes | 4-5 GB |
| 1080p → 4K | 384px | Yes | 5-6 GB |
| 1080p → 8K | 256px | Yes | 6-8 GB |

---

## Bottom Line

1. **FFmpeg RIFE pipeline:** NVENC all day every day
2. **Real-ESRGAN:** FP16 + tile mode for best VRAM/speed balance
3. Your RTX 3080 is a BEAST for FFmpeg
4. Your Ryzen 9 5900X handles CPU-bound preprocessing well
