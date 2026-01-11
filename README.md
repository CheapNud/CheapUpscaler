# CheapUpscaler

AI video upscaling and frame interpolation desktop app for Windows.

## Features

- **RIFE** - Frame interpolation (2x/4x/8x)
- **Real-CUGAN** - Anime/cartoon upscaling (TensorRT)
- **Real-ESRGAN** - General upscaling
- **Non-AI** - Lanczos, xBR, HQx
- Job queue with persistence
- Dependency detection

## Requirements

- Windows 10/11
- .NET 10.0
- NVIDIA GPU (recommended)
- FFmpeg, VapourSynth, Python (optional, detected automatically)

## Build

```bash
git clone https://github.com/CheapNud/CheapUpscaler.git
git clone https://github.com/CheapNud/CheapHelpers.git
cd CheapUpscaler
dotnet run --project CheapUpscaler.Blazor
```

## Stack

- [CheapAvaloniaBlazor](https://github.com/CheapNud/CheapAvaloniaBlazor)
- MudBlazor
- EF Core SQLite

## License

MIT
