# CheapUpscaler - TODO

## Project Focus
AI video upscaling and frame interpolation. Takes rendered video output (from CheapShotcutRandomizer or any video file) and applies:
- RIFE frame interpolation (SVP/Practical-RIFE)
- Real-CUGAN AI upscaling (anime/cartoon optimized, TensorRT accelerated)
- Real-ESRGAN AI upscaling (general content)
- Non-AI upscaling (Lanczos, xBR, HQx)

## Deployment Strategy
| Phase | Target | Platform | Purpose |
|-------|--------|----------|---------|
| **Now** | Desktop App | Windows | Development, testing, local use with GUI |
| **Future** | Worker Service | Ubuntu Server | 24/7 headless processing on Tranquility |

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

## Blazor UI (CheapUpscaler.Blazor)

### Framework & Infrastructure - COMPLETE
- [x] Convert to CheapAvaloniaBlazor desktop app
- [x] Add MudBlazor UI components
- [x] Add EF Core SQLite for persistence
- [x] Program.cs with HostBuilder setup
- [x] _Host.cshtml with MudBlazor CSS
- [x] _Imports.razor with all namespaces
- [x] App.razor router with NotFound handling

### Navigation & Layout - COMPLETE
- [x] MainLayout with MudBlazor drawer navigation
- [x] Dark mode toggle
- [x] Navigation links (Home, Queue, Dependencies, Settings)
- [x] Home page with quick action cards
- [x] Upscaling methods overview

### Dependency Manager Page - COMPLETE
- [x] DependencyChecker service
- [x] DependencyInfo model
- [x] DependencyStatus model
- [x] VapourSynth detection
- [x] Python detection (with version check)
- [x] FFmpeg detection
- [x] vs-mlrt plugin detection
- [x] RIFE detection (TensorRT and Vulkan variants)
- [x] TensorRT runtime detection
- [x] CUDA toolkit detection
- [x] Health percentage indicator
- [x] Manual installation instructions (tooltips + download links)
- [x] DependencyManager.razor page
- [x] DependencyListItem.razor component

### Upscaling Queue Management - IN PROGRESS
- [x] Create `UpscaleJob` model
- [x] Create `UpscaleJobStatus` enum
- [x] Create `UpscaleType` enum
- [x] Create `UpscaleProgressEventArgs` and `QueueStatistics` models
- [x] Create `IUpscaleQueueService` interface
- [x] Create `UpscaleQueueService` implementation (BackgroundService)
- [x] Create `BackgroundTaskQueue` infrastructure (Channel-based)
- [x] UpscaleQueue.razor page with tabs (Active/Completed/Failed)
- [x] UpscaleJobCard.razor component with progress + actions
- [x] Real-time progress updates via events (throttled 100ms)
- [x] Queue start/pause/stop controls
- [ ] Create `UpscaleJobDbContext` for persistence
- [ ] Create `UpscaleJobRepository`
- [ ] Integrate with actual upscaling services (currently simulated)

### Add Upscale Job Dialog - COMPLETE
- [x] AddUpscaleJobDialog.razor with MudDialog
- [x] Video file picker (via IDesktopInteropService)
- [x] Upscale type selector (RIFE/RealCUGAN/RealESRGAN/NonAI)
- [x] Type-specific settings panels:
  - [x] RifeSettingsPanel.razor (multiplier, target FPS, quality preset)
  - [x] RealCuganSettingsPanel.razor (scale, noise level, FP16)
  - [x] RealEsrganSettingsPanel.razor (model, scale, tile size, FP16)
  - [x] NonAiSettingsPanel.razor (algorithm, scale)
- [x] Output path with auto-naming (suffix based on type/settings)
- [x] Integration with Home page (New Job button)
- [x] Integration with Queue page (Add Job button)

### Video Source Selection - TODO
- [ ] Display source video info (resolution, duration, frame rate, codec)
- [ ] Preview thumbnail generation

### RIFE Interpolation Settings UI - TODO
- [ ] Interpolation multiplier slider (2x, 4x, 8x)
- [ ] Target FPS numeric input (30-240)
- [ ] Quality preset selector (Draft/Medium/High)
- [ ] RIFE variant selector (if multiple installed)
- [ ] GPU selection dropdown

### Real-CUGAN Settings UI - TODO
- [ ] Noise reduction level selector (-1 to 3)
- [ ] Scale factor selector (2x, 3x, 4x)
- [ ] Backend selector (TensorRT/CUDA/CPU)
- [ ] FP16 mode toggle
- [ ] GPU device selector

### Real-ESRGAN Settings UI - TODO
- [ ] Model selector (RealESRGAN_x4plus, anime_6B, x2plus, etc.)
- [ ] Scale factor selector (2x, 4x)
- [ ] Tile size input
- [ ] FP16 mode toggle

### Non-AI Upscaling Settings UI - TODO
- [ ] Algorithm selector (Lanczos, xBR, HQx)
- [ ] Scale factor selector (2x, 3x, 4x)

### Settings Page - TODO
- [ ] SettingsService for load/save
- [ ] AppSettings model
- [ ] Tool path configuration with browse
- [ ] Default settings per upscale type
- [ ] Hardware display (GPU, VRAM, CUDA)

### HardwareInfoCard Component - TODO
- [ ] CPU/GPU display
- [ ] NVENC availability
- [ ] Encoder support matrix

---

## Future Enhancements

- [ ] Batch processing (multiple files)
- [ ] Preset system (save/load processing configurations)
- [ ] Processing pipeline builder (chain multiple operations)
- [ ] Watch folder for automatic processing
- [ ] Integration with CheapShotcutRandomizer (direct handoff)
- [ ] Hardware benchmark tool
- [ ] Processing history/statistics

---

## Future: Ubuntu Worker Service

When ready to deploy to Tranquility (Ubuntu server):

- [ ] Create CheapUpscaler.Worker project (.NET Worker Service)
- [ ] Extract queue processing logic (headless)
- [ ] API endpoint for job submission
- [ ] File system watcher for watch folder
- [ ] Linux path configuration
- [ ] Docker container with nvidia-docker GPU passthrough
