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

- [ ] (2026-08-04) Audio track lost on ALL AI-upscaled output (RIFE/RealCUGAN/RealESRGAN) [bug]
  - vspipe→ffmpeg pipelines never mux the source back in; add second input + `-map 0:v -map 1:a? -c:a copy`, same for subtitles
  - Also copy color metadata (range/primaries/trc) from source — currently lost
- [ ] (2026-08-04) Pause doesn't pause — status flips to Paused but vspipe/ffmpeg keep running; completion handler then never updates a Paused job, stuck forever [bug]
  - Resume equally broken (flips to Pending, nothing re-queues). Fix properly or remove the buttons
- [ ] (2026-08-04) RealCUGAN cancellation never kills child processes — RIFE/ESRGAN register graceful shutdown, CUGAN doesn't [bug]

## Planned

- [ ] (2026-08-04) Wire hardware encoding (NVENC) into output encode — detected and displayed but every pipeline hardcodes libx264 [audit]
  - Unused RifePipelineOptions/FFmpegRenderSettings are the ready-made home
  - Add raw `key=value` encoder-option passthrough instead of wrapping every ffmpeg flag
- [ ] (2026-08-04) Persist source/output video metadata — columns exist on UpscaleJob but UpscaleJobEntity never maps them, silently null after DB round-trip [bug]
- [ ] (2026-08-04) Dead-code sweep: IUpscaleService/IRifeService (zero implementations), AutoStartQueue/PlayCompletionSound/MaxRetries (never read), EstimatedTimeRemaining (never computed) [audit]
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
