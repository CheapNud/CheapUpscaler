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

### Upscaling Queue Management - COMPLETE
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

### Database Persistence - COMPLETE
- [x] Create `UpscaleJobDbContext` (SQLite in AppData)
- [x] Create `UpscaleJobEntity` with model conversion
- [x] Create `IUpscaleJobRepository` interface
- [x] Create `UpscaleJobRepository` implementation (EF Core)
- [x] Update `UpscaleQueueService` to use repository
- [x] Jobs persist across app restarts
- [x] Running jobs marked as failed on shutdown recovery
- [x] Pending jobs re-queued on startup
- [x] Integrate with actual upscaling services via UpscaleProcessorService

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

### Video Source Selection - COMPLETE
- [x] Display source video info (resolution, duration, frame rate, codec)
- [x] VideoInfoService using FFMpegCore/FFProbe
- [x] VideoInfo model with formatted display properties
- [ ] Preview thumbnail generation (optional future enhancement)

### RIFE Interpolation Settings UI - COMPLETE
- [x] Interpolation multiplier selector (2x, 4x, 8x)
- [x] Target FPS numeric input (24-240)
- [x] Quality preset selector (Fast/Medium/High)
- [ ] RIFE variant selector (if multiple installed) - future enhancement
- [ ] GPU selection dropdown - future enhancement

### Real-CUGAN Settings UI - COMPLETE
- [x] Noise reduction level selector (-1 to 3)
- [x] Scale factor selector (2x, 3x, 4x)
- [x] FP16 mode toggle
- [ ] Backend selector (TensorRT/CUDA/CPU) - future enhancement
- [ ] GPU device selector - future enhancement

### Real-ESRGAN Settings UI - COMPLETE
- [x] Model selector (RealESRGAN_x4plus, anime_6B, x2plus, etc.)
- [x] Scale factor selector (2x, 4x)
- [x] Tile size input
- [x] FP16 mode toggle

### Non-AI Upscaling Settings UI - COMPLETE
- [x] Algorithm selector (Lanczos, xBR, HQx, Bicubic)
- [x] Scale factor selector (2x, 3x, 4x)

### Settings Page - COMPLETE
- [x] AppSettings model (ToolPaths, DefaultUpscaleSettings, UiSettings, QueueSettings)
- [x] SettingsService for load/save (JSON to AppData)
- [x] Tool path configuration with browse (VapourSynth, Python, FFmpeg, vspipe)
- [x] Default settings per upscale type (expansion panels)
- [x] Queue settings (concurrent jobs, auto-start, output directory)
- [x] UI settings (dark mode, notifications, sounds)
- [x] Save/Reset to Defaults buttons

### HardwareInfoCard Component - COMPLETE
- [x] CPU/GPU display
- [x] NVIDIA GPU detection
- [x] NVENC availability
- [x] Quick Sync detection (Intel CPUs)
- [x] Processing capabilities chips (TensorRT, CUDA, NVENC, CPU)
- [x] Multi-GPU list display

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

## Next Branch: SVP Detection & Path Configuration Fixes

Issues from PR #2 review and code audit. See: https://github.com/CheapNud/CheapUpscaler/pull/2

### Critical Fixes (Breaking without SVP)

- [ ] **Add `RifeFolderPath` to `AppSettings.ToolPaths`** - Allow user-configurable RIFE path in Settings UI
- [ ] **Fix empty string fallbacks in DI factory** (`ServiceCollectionExtensions.cs:30-35`)
  - Currently: `rifePath = svp.RifePath ?: ""` → crashes at runtime
  - Should: Check AppSettings first, then SVP, then fail gracefully with error message
- [ ] **Make VapourSynth script generation conditional** (`RifeInterpolationService.cs`)
  - Validate paths exist before generating script
  - `svpModelPath` is currently hardcoded, breaks without SVP
- [ ] **Extract factory to avoid DRY violation** - Same factory logic duplicated in `AddUpscalerServices` and `AddRifeServices`

### Code Quality (From PR Bot Review)

- [ ] **Add constants for magic strings** - `"vstrt.dll"`, `"rife_vs.dll"`, `"rife.dll"` etc.
- [ ] **Safe path handling** - `Path.GetDirectoryName(pluginPath) ?? ""` returns empty string, should throw
- [ ] **Use proper logging** - Replace `Debug.WriteLine` with `ILogger<T>` injection
- [ ] **Add XML documentation** - Explain SVP integration approach

### Dead Code Cleanup

- [ ] **Remove or integrate `RifeVariantDetector`** - Currently registered but never used in job processing
- [ ] **Remove `RifeOptions.BuildArguments()`** - Dead code, was for CLI RIFE
- [ ] **Remove GitHub RIFE code path** (`RifeInterpolationService.cs:408-436`) - Can never execute

### Temp File Management

- [ ] **Use `TemporaryFileManager` from CheapHelpers.MediaProcessing** for VapourSynth script files
  - Currently: `Path.Combine(Path.GetTempPath(), $"svp_rife_{Guid}.vpy")`
  - Should: Use `TemporaryFileManager.GetTempFilePath("svp_rife", ".vpy")` with proper cleanup

### Future Enhancement Ideas

- [ ] **Model detection** - Detect which ONNX models are actually installed, adjust UI options
- [ ] **Engine auto-selection** - TensorRT if ONNX available, NCNN_VK if only .bin/.param
- [ ] **Integrate `DependencyChecker` results** - Use detected RIFE path to initialize service at runtime

---

## Future: Ubuntu Worker Service

When ready to deploy to Tranquility (Ubuntu server):

- [ ] Create CheapUpscaler.Worker project (.NET Worker Service)
- [ ] Extract queue processing logic (headless)
- [ ] API endpoint for job submission
- [ ] File system watcher for watch folder
- [ ] Linux path configuration
- [ ] Docker container with nvidia-docker GPU passthrough
