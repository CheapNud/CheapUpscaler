<!--
  TODO.md — CheapUpscaler project work tracker
  Last updated: 2026-03-17

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

_Nothing blocking._

## Planned

- [ ] (2026-03-17) API key authentication middleware (CheapUpscaler.Shared) [plan]
  - `X-Api-Key` header check, configured via env var (Worker) or settings (Desktop)
  - `/health` excluded from auth, Blazor same-origin bypasses
- [ ] (2026-03-17) Queue control REST API — expose pause/resume/throttle [plan]
  - `POST /api/queue/pause`, `POST /api/queue/resume`, `GET /api/queue/status`, `PUT /api/queue/settings`
  - Pause reason tracking for UI display
- [ ] (2026-03-17) Tautulli integration — transcode-aware queue pausing [plan]
  - Webhook: transcode start → pause, playback stop → resume
- [ ] (2026-03-17) Helios-One GPU deployment — verify NVIDIA Container Toolkit + deploy [plan]
  - `nvidia-ctk --version`, `docker run --gpus all nvidia/cuda:12.0-base nvidia-smi`

## Future

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
