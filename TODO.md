# CheapUpscaler - TODO

## Project Focus
AI video upscaling and frame interpolation. Takes rendered video output (from CheapShotcutRandomizer or any video file) and applies:
- RIFE frame interpolation (SVP/Practical-RIFE)
- Real-CUGAN AI upscaling (anime/cartoon optimized, TensorRT accelerated)
- Real-ESRGAN AI upscaling (general content)
- Non-AI upscaling (Lanczos, xBR, HQx)

---

## Core Library (CheapUpscaler.Core) - COMPLETE

- [x] `RifeInterpolationService` - RIFE wrapper (SVP + Practical-RIFE)
- [x] `RifeVariantDetector` - Detect installed RIFE variants
- [x] `RealCuganService` - VapourSynth + vs-mlrt + TensorRT
- [x] `RealEsrganService` - Real-ESRGAN processing
- [x] `NonAiUpscalingService` - Lanczos/xBR/HQx upscaling
- [x] `VapourSynthEnvironment` - Python/VapourSynth environment management
- [x] Models: `RifeSettings`, `RealCuganOptions`, `RealEsrganOptions`, `NonAiUpscalingOptions`
- [x] `ServiceCollectionExtensions` - DI registration

---

## Blazor UI (CheapUpscaler.Blazor) - TODO

### Video Source Selection
- [ ] Create video file browser dialog (mp4, mkv, avi, mov)
- [ ] Display source video info (resolution, duration, frame rate, codec)
- [ ] Preview thumbnail generation
- [ ] Output path selection with auto-naming based on processing type

### RIFE Interpolation Settings UI
- [ ] Interpolation multiplier slider (2x, 4x, 8x)
- [ ] Target FPS numeric input (30-240)
- [ ] Quality preset selector (Draft/Medium/High)
- [ ] RIFE variant selector (if multiple installed)
- [ ] GPU selection dropdown
- [ ] Estimated output FPS display

### Real-CUGAN Settings UI
- [ ] Noise reduction level selector (-1 to 3)
- [ ] Scale factor selector (2x, 3x, 4x)
- [ ] Backend selector (TensorRT/CUDA/CPU)
- [ ] FP16 mode toggle
- [ ] Parallel streams slider (1-8)
- [ ] GPU device selector
- [ ] Noise/scale compatibility warning
- [ ] Estimated processing speed display

### Real-ESRGAN Settings UI
- [ ] Model selector (RealESRGAN_x4plus, anime_6B, x2plus, general-x4v3, AnimeVideo-v3)
- [ ] Scale factor selector (2x, 4x)
- [ ] Tile size input (0-1024)
- [ ] FP16 mode toggle
- [ ] Tile mode toggle
- [ ] GPU device selector
- [ ] Performance warning (slow processing)

### Non-AI Upscaling Settings UI
- [ ] Algorithm selector (Lanczos, xBR, HQx)
- [ ] Scale factor selector (2x, 3x, 4x)
- [ ] Content type hint (general vs pixel art)
- [ ] Performance estimate (near real-time)

### Upscaling Queue Management
- [ ] Create `UpscaleJob` model (similar to RenderJob)
- [ ] Create `IUpscaleQueueService` interface
- [ ] Create `UpscaleQueueService` implementation
- [ ] Create `UpscaleJobRepository` for persistence
- [ ] Queue page with active/completed/failed tabs
- [ ] Job card component with progress, ETA, file sizes
- [ ] Start/pause/cancel queue controls
- [ ] Individual job pause/resume/cancel/retry
- [ ] Real-time progress updates via events

### Dependency Manager Page
- [ ] VapourSynth detection and installation
- [ ] Python detection (with version check)
- [ ] PyTorch detection and installation
- [ ] vs-mlrt plugin detection and installation
- [ ] RIFE detection (SVP and Practical-RIFE variants)
- [ ] TensorRT runtime detection
- [ ] CUDA toolkit detection
- [ ] Health percentage indicator
- [ ] Auto-install missing dependencies button
- [ ] Manual installation instructions

### Settings Page
- [ ] Default RIFE settings
- [ ] Default Real-CUGAN settings
- [ ] Default Real-ESRGAN settings
- [ ] Default Non-AI upscaling settings
- [ ] Custom paths (VapourSynth, Python, RIFE folder)
- [ ] Hardware detection display (GPU, VRAM, CUDA version)
- [ ] Theme/appearance settings

### Navigation & Layout
- [ ] Create MainLayout with sidebar navigation
- [ ] Home/Dashboard page with quick actions
- [ ] Upscale page (main processing UI)
- [ ] Queue page
- [ ] Dependencies page
- [ ] Settings page
- [ ] About page

---

## Future Enhancements

- [ ] Batch processing (multiple files)
- [ ] Preset system (save/load processing configurations)
- [ ] Processing pipeline builder (chain multiple operations)
- [ ] Watch folder for automatic processing
- [ ] Integration with CheapShotcutRandomizer (direct handoff)
- [ ] Hardware benchmark tool
- [ ] Processing history/statistics
