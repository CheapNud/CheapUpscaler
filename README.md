# CheapUpscaler

AI video upscaling and frame interpolation desktop app for Windows.

## Features

- **RIFE** - Frame interpolation (2x/4x/8x) via SVP or Practical-RIFE
- **Real-CUGAN** - Anime/cartoon upscaling with TensorRT acceleration
- **Real-ESRGAN** - General content upscaling with multiple models
- **Non-AI** - Traditional algorithms (Lanczos, xBR, HQx, Bicubic)
- Background job queue with pause/resume/cancel
- SQLite persistence for job history
- Dependency manager with auto-detection

## Requirements

- Windows 10/11 (x64)
- .NET 10.0
- NVIDIA GPU recommended for AI upscaling

Optional dependencies (the app detects what's installed):
- FFmpeg
- VapourSynth + Python
- vs-mlrt / TensorRT
- SVP or Practical-RIFE

## Build

```bash
git clone https://github.com/CheapNud/CheapUpscaler.git
cd CheapUpscaler
dotnet run --project CheapUpscaler.Blazor
```

## Project Structure

```
CheapUpscaler.Core/     # Upscaling services (RIFE, Real-CUGAN, Real-ESRGAN, Non-AI)
CheapUpscaler.Blazor/   # Desktop UI (CheapAvaloniaBlazor + MudBlazor)
```

## Tech Stack

- [CheapAvaloniaBlazor](https://github.com/CheapNud/CheapAvaloniaBlazor) - Desktop framework
- [MudBlazor](https://mudblazor.com/) - UI components
- EF Core SQLite - Job persistence
- FFMpegCore - Video processing

## Related

- [CheapShotcutRandomizer](https://github.com/CheapNud/CheapShotcutRandomizer) - Video editor/randomizer
- [CheapHelpers](https://github.com/CheapNud/CheapHelpers) - Shared utilities

## License

MIT
