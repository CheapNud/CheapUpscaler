# Docker Testing - Pickup Notes

## Current Status
- Docker Desktop installed and working on Windows
- First version with Docker support complete
- Local smoke test passed

## Target Environment
- **Proxmox Host:** Tranquility
- **Target VM:** Helios-One (Ubuntu Server with GPU passthrough for Plex)

## Testing Plan

### Phase 1: Local Smoke Test (Docker Desktop) - COMPLETE
- [x] Verify container builds successfully
- [x] Test basic functionality (CPU-mode if supported)
- [x] Check dependencies resolve correctly

### Phase 2: Helios-One Deployment (Full GPU Test)
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

## Notes
- Local Hyper-V Ubuntu won't help - no GPU passthrough available
- Docker Desktop useful for build validation only
- Helios-One is the only environment for full GPU testing
