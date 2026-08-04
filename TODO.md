<!--
  TODO.md — CheapUpscaler project work tracker
  Last updated: 2026-08-04

  RULES FOR AI AGENTS:
  - Update the "Last updated" date above whenever you modify this file
  - Items use checkbox format: - [ ] incomplete, - [x] complete
  - Never remove completed items — they serve as history. Move them to "## Done" when a category gets cluttered.
  - Each item gets ONE line. Details go in sub-bullets indented with 2 spaces.
  - Prefix each item with the date it was added: - [ ] (2026-03-17) Description
  - When completing, change to: - [x] (2026-03-17 → 2026-03-18) Description
  - Tag the SOURCE of each item at the end in brackets:
      [code-todo] = from // TODO comment in source code
      [plan] = from a plan document or planning session
      [bug] = from a bug encountered during dev/deploy
      [audit] = from a code audit or review
      [user] = explicitly requested by the user
  - For [code-todo] items, ALWAYS include file:line reference so devs can navigate directly
  - Categories: Blocking, Planned, Future, Done
  - New items go at the TOP of their category
  - Do not create separate TODO_*.md files — everything goes here
  - Keep it terse. If it needs more than 3 sub-bullets, link to a plan document.
  - Do NOT create, rename, or remove categories — the fixed set is: Blocking, Planned, Future, Done
  - When asked for planned work or TODO analysis, ALWAYS include Future items too — list them below Planned and note them as future work
-->

# TODO

## Blocking

- [ ] (2026-08-04) Queue deadlocks on startup when >100 pending jobs exist [audit]
  - InitializeAsync enqueues into bounded Channel(100, FullMode.Wait) before the consumer loop starts; item 101 blocks forever, service silently never runs
  - Same bounded write with no CancellationToken can hang POST /api/jobs and the Blazor circuit after the row is committed
- [ ] (2026-08-04) Headless Worker never processes anything without a human [audit]
  - Queue boots paused, only the UI Start button unpauses, and CheckAndAutoPauseQueueAsync re-pauses after every drain; AutoStartQueue setting read by nobody
  - No queue start/stop/pause REST endpoints — the Blazor UI is the only control plane
- [ ] (2026-08-04) Desktop Cancel doesn't cancel — UpscaleQueueService has no per-job CancellationTokenSource registry (Worker twin has one); ffmpeg/vspipe keep running after UI says Cancelled [audit]
- [ ] (2026-08-04) Generated VapourSynth scripts break on real-world paths [audit]
  - Paths interpolated as r'{path}' in all three AI script generators: any apostrophe in a filename = Python syntax error
- [ ] (2026-08-04) Hardcoded BT.709 color matrix corrupts SD and HDR content [audit]
  - matrix_in_s='709' both directions in all AI scripts: BT.601 SD shifts color, BT.2020/HDR mangled and crushed to 8-bit; derive from source props instead
- [ ] (2026-08-04) Audio track lost on ALL AI-upscaled output (RIFE/RealCUGAN/RealESRGAN) [bug]
  - vspipe→ffmpeg pipelines never mux the source back in; add second input + `-map 0:v -map 1:a? -c:a copy`, same for subtitles
  - Also copy color metadata (range/primaries/trc) from source — currently lost
- [ ] (2026-08-04) Pause doesn't pause — status flips to Paused but vspipe/ffmpeg keep running; completion handler then never updates a Paused job, stuck forever [bug]
  - Resume equally broken (flips to Pending, nothing re-queues). Fix properly or remove the buttons
- [ ] (2026-08-04) RealCUGAN cancellation never kills child processes — RIFE/ESRGAN register graceful shutdown, CUGAN doesn't [bug]

## Planned

- [ ] (2026-08-04) Consolidate the duplicated queue engine into Shared [audit]
  - WorkerQueueService (516 lines) and UpscaleQueueService (422) are ~90% identical and have already drifted (cancellation, auto-pause, forced 60fps); one class + a single IJobProcessor interface, hosts keep only their processor impl
  - Same treatment for VideoInfoService/WorkerVideoInfoService twins and the dependency-checker GetFFmpegVersion drift (Worker copy lost its timeout kill, leaks hung processes)
  - Move IUpscaleQueueService, ISettingsService, AppSettings out of the MudBlazor Components project into Shared
- [ ] (2026-08-04) Job-settings DTOs defined three times with drifted defaults [audit]
  - Nested classes in AddUpscaleJobDialog (consumed by services via using static), separate records in WorkerProcessorService: ESRGAN defaults differ (x4plus/512 vs x4plus-anime/0)
  - Move to Shared, wire both processors through UpscaleJob.GetSettings<T> (currently dead code); shared JsonSerializerOptions with PropertyNameCaseInsensitive
- [ ] (2026-08-04) Adopt EF migrations — EnsureCreated freezes the schema [audit]
  - Blocks every column addition (metadata columns, JobName which exists on the model but not the entity); add Initial migration, switch both hosts to Database.Migrate()
  - UpscaleJobEntity.UpdateFrom drops SourceVideoPath/OutputPath/UpscaleType/SettingsJson — post-insert edits never persist; derive from one field list
- [ ] (2026-08-04) Pipeline process lifecycle hardening in Core [audit]
  - Orphaned GPU processes: on mid-pipeline failure nothing kills vspipe/ffmpeg (kill handlers only fire on cancellation) — add try/finally kill
  - Progress only works for ESRGAN: RIFE reads \n-lines but vspipe emits \r updates; CUGAN vspipe call is missing -p entirely
  - RIFE test-run blocks a thread up to 20 min with sync WaitForExit; constructors spawn python probes; silent except:pass hides plugin load failures
- [ ] (2026-08-04) Worker API trust boundary for remote clients [audit]
  - Client-supplied absolute OutputPath goes straight to ffmpeg (can overwrite the job DB); path allowlist uses raw StartsWith (/data/input-backup passes as /data/input)
  - SettingsJson stored verbatim, deserialized case-sensitively with silent fallback to defaults — wrong-cased fields run the wrong model and report success; validate at POST with 400
  - Remote push needs POST /api/files upload + token flow: CreateJob currently requires the input path to exist on the worker filesystem
- [ ] (2026-08-04) Queue semantics: MaxConcurrentJobs does nothing and pause blocks the queue [audit]
  - Serial dequeue loop awaits each job so the semaphore never contends; pause spins inside the loop after dequeue, so one paused job jams everything behind it
- [ ] (2026-08-04) Settings layer fixes [audit]
  - Settings page edits the live singleton by reference (every keystroke applies pre-Save); desktop SettingsService never loads at startup so ToolPaths overrides are always ignored (captive transient in singleton processor)
  - Worker LoadAsync drops the config-seeded DefaultOutputDirectory (regression of the output-path fix); two hosts serialize AppSettings with incompatible naming policies
- [ ] (2026-08-04) Platform registration by TFM instead of runtime OS [audit]
  - #if WINDOWS in AddPlatformServices means the net11.0 Worker on Windows gets Linux paths/locators; switch to RuntimeInformation checks; AddPlatformServices registered twice; IPlatformPaths registered but injected nowhere
- [ ] (2026-08-04) File watcher robustness [audit]
  - async void Created/Renamed handlers can take down the process; restart double-queues existing files (checks in-memory cache racing InitializeAsync — query the repository instead); shutdown cleanup unreachable
- [ ] (2026-08-04) docker-compose mounts input :ro so every upload fails; UI fixes: desktop Download button navigates Photino to a 404 (Worker-only endpoint), generated output filename goes stale when type/settings change after load [bug]
- [ ] (2026-08-04) Wire hardware encoding (NVENC) into output encode — detected and displayed but every pipeline hardcodes libx264 [audit]
  - Unused RifePipelineOptions/FFmpegRenderSettings are the ready-made home
  - Add raw `key=value` encoder-option passthrough instead of wrapping every ffmpeg flag
- [ ] (2026-08-04) Persist source/output video metadata — columns exist on UpscaleJob but UpscaleJobEntity never maps them, silently null after DB round-trip [bug]
- [ ] (2026-08-04) Dead-code sweep [audit]
  - IUpscaleService/IRifeService/RifePipelineOptions/InterpolateFramesAsync (zero callers), AutoStartQueue/PlayCompletionSound/MaxRetries/DarkMode/DefaultSettings section (never read), EstimatedTimeRemaining (never computed)
  - Consolidate RIFE's three parallel model tables; route RIFE through IVapourSynthEnvironment instead of its private duplicate detection; Real-CUGAN Linux crash (os.environ['APPDATA'] KeyError)
- [ ] (2026-08-04) Scene-cut detection before RIFE interpolation — cheap frame-diff; above threshold duplicate frame instead of interpolating (prevents ghosting across cuts) [plan]
- [ ] (2026-08-04) Auto tile size from queried VRAM budget instead of user-configured [plan]
- [ ] (2026-08-04) Removable drive workflow — process from/to a plugged-in USB stick or external SSD [user]
  - One large job or a big batch of small ones; output lands back on the drive
  - Overlaps with batch processing (Future) — likely built together
- [ ] (2026-08-04) Fix invalid MudBlazor parameters flagged by MUD0002 [audit]
  - FileBrowserDialog.razor: `OnDoubleClick` on MudListItem (likely non-functional), `Title` on MudIconButton
  - DependencyManager.razor / UpscaleQueue.razor: `PanelClass` on MudTabs; FileUploadDialog.razor: `ChildContent` on MudFileUpload

## Future

- [ ] (2026-08-04) Live preview of in-flight jobs — tee encoder output as fragmented MP4, stream to a video element in the UI [user]
- [ ] (2026-08-04) Preset system as ordered stage lists with composition rules (restore→upscale→interpolate) instead of monolithic setting blobs; ship GPU-tier preset tables [plan]
- [ ] (2026-08-04) Anime4K shader support via ffmpeg libplacebo `custom_shader_path` — lowest priority [plan]
  - Fast/weak-GPU/preview tier; requires Vulkan (Docker image lacks graphics capability; verify local ffmpeg has libplacebo)
  - vs-placebo route has an open bug breaking offline encode — use the ffmpeg path
- [ ] (2026-08-04) Remote worker push — desktop client sends jobs to a Worker on another machine [user]
  - Upload → enqueue → poll → download result; local vs remote is a per-job choice
  - Same Worker binary regardless of host: Docker on Linux, native win-x64 service on Windows (no WSL2 — multi-GB file I/O)
  - Roles are reversible: any machine can be client or worker; jobs run on whichever box is free (gaming on one → process on the other)
- [ ] (2026-08-04) API key authentication middleware — needed once a Worker is exposed on the LAN [plan]
  - `X-Api-Key` header check, configured via env var (Worker) or settings (Desktop)
  - `/health` excluded from auth, Blazor same-origin bypasses
- [ ] (2026-08-04) Queue control REST API — pause/resume/throttle for remote workers [plan]
  - `POST /api/queue/pause`, `POST /api/queue/resume`, `GET /api/queue/status`, `PUT /api/queue/settings`
- [ ] (2026-03-17) Batch processing (multiple files) [plan]
- [ ] (2026-03-17) Preset system (save/load processing configurations) [plan]
- [ ] (2026-03-17) Processing pipeline builder (chain multiple operations) [plan]
- [ ] (2026-03-17) Integration with CheapShotcutRandomizer (direct handoff) [plan]
- [ ] (2026-03-17) Hardware benchmark tool [plan]
- [ ] (2026-03-17) Processing history/statistics [plan]
- [ ] (2026-03-17) Preview thumbnail generation [plan]
- [ ] (2026-03-17) RIFE variant selector (if multiple installed) [plan]
- [ ] (2026-03-17) GPU selection dropdown [plan]
- [ ] (2026-03-17) Backend selector for Real-CUGAN (TensorRT/CUDA/CPU) [plan]

## Done

- [x] (2026-03-17 → 2026-08-04) Abandoned: Tautulli transcode-aware queue pausing — only made sense sharing a server GPU with Plex [plan]
- [x] (2026-03-17 → 2026-08-04) Abandoned: dedicated server GPU deployment — container removed, server GPU is Plex-only now; Docker remains supported as a deployment model [plan]
- [x] (pre-2026 → pre-2026) Core library — RIFE, Real-CUGAN, Real-ESRGAN, NonAI services [plan]
- [x] (pre-2026 → pre-2026) Blazor UI — CheapAvaloniaBlazor desktop app with MudBlazor [plan]
- [x] (pre-2026 → pre-2026) Dependency manager page — auto-detection of all tools [plan]
- [x] (pre-2026 → pre-2026) Upscale queue — Channel-based BackgroundService with persistence [plan]
- [x] (pre-2026 → pre-2026) Add job dialog — file picker, type-specific settings panels [plan]
- [x] (pre-2026 → pre-2026) Video source info via FFProbe [plan]
- [x] (pre-2026 → pre-2026) Settings page — tool paths, defaults, queue, UI settings [plan]
- [x] (pre-2026 → pre-2026) Hardware info card — GPU detection, NVENC, TensorRT, CUDA [plan]
- [x] (pre-2026 → pre-2026) EF Core SQLite persistence — jobs survive restarts [plan]
- [x] (pre-2026 → pre-2026) Ubuntu Worker Service — REST API + Blazor UI for Docker [plan]
- [x] (pre-2026 → pre-2026) Docker smoke test (Docker Desktop, CPU-mode) [plan]
- [x] (pre-2026 → pre-2026) FileWatcherService for automatic processing [plan]
- [x] (pre-2026 → pre-2026) SVP detection + RIFE path configuration fixes (PR #2) [bug]
- [x] (pre-2026 → pre-2026) TemporaryFileManager from CheapHelpers.MediaProcessing [audit]
- [x] (pre-2026 → pre-2026) blazor.server.js 404 fix — CheapAvaloniaBlazor v1.2.4 [bug]
