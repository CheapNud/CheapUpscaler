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
| **Done** | Worker Service | Ubuntu Server (Docker) | 24/7 headless processing on Tranquility |

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

### API Key Authentication (Shared)

**Applies to both Worker (Docker) and Desktop (Blazor) — not Docker-only.**
Currently the API is completely unsecured. All endpoints are wide open.

**Implementation:**
- API key middleware in `CheapUpscaler.Shared` so both projects can register it
- Checks `X-Api-Key` header against configured key
- Key configured via:
  - Worker: environment variable `Worker__ApiKey` in docker-compose.yml
  - Desktop: `AppSettings.json` or settings UI
- Blazor UI requests (same origin) bypass the check — internal SignalR/Blazor calls are not API consumers
- `/health` endpoint excluded from auth (monitoring needs unauthenticated access)
- Return `401 Unauthorized` with no body on invalid/missing key

**Tautulli compatibility:**
Tautulli webhook agent supports custom headers — configure `{"X-Api-Key": "your-key"}` on the webhook notification agent.

### Queue Control API

Expose `WorkerQueueService` pause/resume/throttle through REST endpoints for external automation.
The service already has `PauseQueue()`, `ResumeQueue()`, `IsQueuePaused` — none are exposed in `JobsController`.
Endpoints must be protected by the API key middleware above.

**Proposed endpoints:**
- `POST /api/queue/pause` — pause processing (finish current job, don't start new ones)
  - Accepts optional `{ "reason": "Plex transcode active", "source": "tautulli" }`
- `POST /api/queue/resume` — resume processing
  - Accepts optional `{ "source": "tautulli" }`
- `GET  /api/queue/status` — returns `{ isPaused, pauseReason, pauseSource, pausedAt, activeJobs, pendingJobs, maxConcurrentJobs }`
- `PUT  /api/queue/settings` — adjust `MaxConcurrentJobs` at runtime

**Pause reason tracking (UI):**
The queue can be paused for multiple reasons — the UI MUST show WHY it's paused:
- "Paused: Plex transcode active (via Tautulli)" — external API pause
- "Paused: manually paused" — user clicked pause in UI
- "Paused: no pending jobs" — auto-pause after queue drained
- "Paused: scheduled quiet hours" — cron-triggered pause
Track `PauseReason`, `PauseSource`, and `PausedAt` in `WorkerQueueService` so the Blazor UI
can display a clear MudAlert explaining the current state instead of just a paused/running toggle.

**Tautulli integration (Plex transcode-aware pausing):**
Tautulli can distinguish direct play from transcode — native Plex webhooks cannot.
- Trigger: "Transcode Decision Change" with condition `Video Decision | is | transcode`
- Playback Start (transcode) → Tautulli webhook → `POST /api/queue/pause { "reason": "Plex transcode active", "source": "tautulli" }`
- Playback Stop → Tautulli webhook → `POST /api/queue/resume { "source": "tautulli" }`
- Both containers on same Docker network — HTTP call is just `http://cheapupscaler:5080`
- Requires Tautulli webhook agent pointing at the queue control endpoint

**Use cases:**
- Plex (or any service) pauses upscaling during heavy GPU/transcode load
- Cron job: pause at night for quiet hours, resume in the morning
- Home Assistant / automation integration
- Manual throttle from any HTTP client without needing the Blazor UI

**Resource coexistence with Plex (same Helios-One VM, shared GPU):**
- Plex transcoding uses NVENC/NVDEC (dedicated fixed-function hardware)
- TensorRT upscaling uses CUDA/Tensor cores (separate hardware blocks)
- These run simultaneously without directly competing for the same silicon
- VRAM is the shared resource — Plex transcoding ~100-200MB, Real-ESRGAN 1-4GB depending on tile size/model
- Docker `cpus`, `cpuset`, `mem_limit` can isolate CPU/RAM between containers
- Queue pause API enables yielding to Plex on demand

### Other

- [ ] Batch processing (multiple files)
- [ ] Preset system (save/load processing configurations)
- [ ] Processing pipeline builder (chain multiple operations)
- [x] Watch folder for automatic processing (Worker service - FileWatcherService)
- [ ] Integration with CheapShotcutRandomizer (direct handoff)
- [ ] Hardware benchmark tool
- [ ] Processing history/statistics

---

## Next Branch: SVP Detection & Path Configuration Fixes

Issues from PR #2 review and code audit. See: https://github.com/CheapNud/CheapUpscaler/pull/2

### Critical Fixes (Breaking without SVP)

- [x] **Add `RifeFolderPath` to `AppSettings.ToolPaths`** - User-configurable RIFE path in Settings
- [x] **Fix empty string fallbacks in DI factory** - Program.cs overrides Core factory with settings-first logic
  - Priority: 1) AppSettings.ToolPaths.RifeFolderPath, 2) SVP auto-detection, 3) empty (RIFE unavailable)
- [x] **Make VapourSynth script generation conditional** (`RifeInterpolationService.cs`)
  - Validates paths exist before generating script, throws descriptive exceptions
- [x] **Extract factory to avoid DRY violation** - `CreateRifeService()` in ServiceCollectionExtensions.cs

### Code Quality (From PR Bot Review)

- [x] **Add constants for magic strings** - `KnownDlls` class in DependencyChecker.cs
- [x] **Safe path handling** - Validated paths before use in GenerateSvpRifeScript
- [x] **Use proper logging** - Replace `Debug.WriteLine` with `ILogger<T>` injection
  - All services use `ILogger<T>?` as optional constructor parameter
  - RifeVariantDetector static methods accept `ILogger?` parameter
- [x] **Add XML documentation** - SVP integration explained in ServiceCollectionExtensions

### Dead Code Cleanup

- [x] ~~**Remove or integrate `RifeVariantDetector`**~~ - KEEP for future Linux/Docker support
  - SVP not available on Linux, so Practical-RIFE with standalone executables (`rife-tensorrt.exe`, `rife-ncnn-vulkan.exe`) is needed
  - Variant detection will be useful for Ubuntu Worker Service deployment
- [x] **Remove `RifeOptions.BuildArguments()`** - Removed (was for CLI RIFE)
- [x] ~~**Remove GitHub RIFE code path**~~ - NOT dead code: supports Practical-RIFE as SVP alternative

### Temp File Management

- [x] **Use `TemporaryFileManager` from CheapHelpers.MediaProcessing** for VapourSynth script files
  - Uses `using var tempManager = new TemporaryFileManager()` with `GetTempFilePath("svp_rife", ".vpy")`
  - Automatic cleanup via `IDisposable` pattern

### Future Enhancement Ideas

- [x] **Model detection** - `RifeInterpolationService.GetAvailableModels()` scans ONNX files
  - `UpscaleProcessorService.ProcessRifeAsync()` pre-validates and falls back to available model
- [x] **Engine auto-selection** - TensorRT if ONNX available, NCNN_VK if only .bin/.param
  - `RifeInterpolationService.AutoSelectEngine()` and `GetAvailableEngines()` methods
  - `UpscaleProcessorService.ProcessRifeAsync()` uses auto-selection
- [x] **Integrate `DependencyChecker` results** - Use detected RIFE path to initialize service at runtime
  - `DependencyChecker.GetDetectedRifePath()` returns SVP or standalone RIFE path
  - `DependencyChecker.GetDetectedPythonPath()` returns SVP or system Python path

---

## Bug: CheapAvaloniaBlazor Static Web Assets (blazor.server.js 404) - FIXED

**Problem**: The Blazor app boots but spams `InvalidOperationException` and `/_framework/blazor.server.js` returns 404.

**Root Cause**: `EmbeddedBlazorHostService.cs` called `WebApplication.CreateBuilder()` without arguments, defaulting to "Production" environment where `UseStaticWebAssets()` is a no-op.

**Fix Applied in CheapAvaloniaBlazor v1.2.4**:
- [x] Hardcoded Development environment in `EmbeddedBlazorHostService.cs`
- [x] Added fat comment explaining WHY it's hardcoded and the over-engineering history
- [x] Removed `UseEnvironment()`, `UseDevelopmentEnvironment()`, `UseProductionEnvironment()` methods
- [x] Removed `EnvironmentName` property from options
- [x] Cleaned up README and docs

**Why hardcode Development?** Desktop apps are localhost-only, never deployed to web servers. Production environment's security features (error hiding, HSTS) are irrelevant. Development mode is required for `UseStaticWebAssets()` to serve `blazor.server.js` from NuGet packages.

**Over-engineering history (v1.2.2-v1.2.3)**: We tried letting users configure environment with `#if DEBUG` patterns, but NuGet libraries are compiled in Release mode so compile-time detection in the library doesn't work. After much complexity, realized the whole thing was solving a non-problem for desktop apps.

---

## Ubuntu Worker Service (CheapUpscaler.Worker) - COMPLETE

Headless worker with REST API + Blazor UI for Docker deployment on Tranquility (Helios-One VM).

- [x] Create CheapUpscaler.Worker project (ASP.NET Core + Blazor Server)
- [x] Extract queue processing logic (WorkerQueueService, WorkerProcessorService)
- [x] API endpoint for job submission (JobsController - full CRUD + download)
- [x] File system watcher for watch folder (FileWatcherService)
- [x] Linux path configuration (LinuxPlatformPaths, LinuxToolLocator)
- [x] Docker container with nvidia-docker GPU passthrough (multi-stage Dockerfile + docker-compose.yml)
- [x] Local Docker Desktop smoke test passed

### Docker Testing

**Target Environment:**
- **Proxmox Host:** Tranquility
- **Target VM:** Helios-One (Ubuntu Server with GPU passthrough for Plex)
- Local Hyper-V Ubuntu won't help — no GPU passthrough available

**Phase 1: Local Smoke Test (Docker Desktop) - COMPLETE**
- [x] Verify container builds successfully
- [x] Test basic functionality (CPU-mode if supported)
- [x] Check dependencies resolve correctly

**Phase 2: Helios-One Deployment (Full GPU Test)**
- [ ] Verify NVIDIA Container Toolkit is installed:
  ```bash
  nvidia-ctk --version
  ```
- [ ] Verify Docker can see GPU:
  ```bash
  docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi
  ```
- [ ] Deploy container with GPU access
- [ ] Validate upscaling works with GPU acceleration
