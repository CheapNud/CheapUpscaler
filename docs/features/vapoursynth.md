# VapourSynth Setup

Required for RIFE, Real-CUGAN, and Real-ESRGAN processing.

For general installation guidance, see [Installation Guide](../installation.md).

## Installation via Dependency Manager (Recommended)

1. Navigate to **Dependency Manager** in the application
2. Check status of "VapourSynth" and "VapourSynth Source Plugin"
3. Follow the automated installation instructions
4. Click "Refresh Status" to verify installation
5. The VapourSynthEnvironment service will automatically detect the installation

## Advanced/Manual Installation

### Install VapourSynth

1. **Download & Install**
   - Download: https://github.com/vapoursynth/vapoursynth/releases
   - Run installer (adds `vspipe` to PATH)
   - Restart computer

2. **Verify**
   ```cmd
   vspipe --version
   ```

### Install Source Plugin

VapourSynth needs a plugin to load videos. Choose one:

**BestSource (Recommended)**
- Download: https://github.com/vapoursynth/bestsource/releases
- Extract `BestSource.dll` to:
  - `C:\Program Files\VapourSynth\plugins\`
  - OR `%APPDATA%\VapourSynth\plugins\`

**L-SMASH Source (Alternative)**
- Download: https://github.com/AkarinVS/L-SMASH-Works/releases
- Extract `LSMASHSource.dll` to `C:\Program Files\VapourSynth\plugins\`

**FFMS2 (Another Alternative)**
- Download: https://github.com/FFMS/ffms2/releases
- Extract `FFMS2.dll` to `C:\Program Files\VapourSynth\plugins\`

## Troubleshooting

**First Step: Use the Dependency Manager**
- Navigate to **Dependency Manager** in the application
- Click "Refresh Status" to re-check all dependencies
- Review the status and error messages for each dependency
- The DependencyChecker service provides detailed diagnostic information

### VapourSynth Errors

**"No attribute with the name bs/ffms2/lsmas exists"**
- Check Dependency Manager for "VapourSynth Source Plugin" status
- Install a source plugin (BestSource recommended) via Dependency Manager or manually
- Restart application after installing
- Check plugin is in correct folder

**"vspipe not found"**
- Check Dependency Manager for "VapourSynth" status
- Restart computer after VapourSynth installation
- Verify: `where vspipe` shows correct path
- Use Dependency Manager's "Refresh Status" to verify detection

## Links

- **VapourSynth:** https://github.com/vapoursynth/vapoursynth
- **BestSource:** https://github.com/vapoursynth/bestsource
- **L-SMASH Source:** https://github.com/AkarinVS/L-SMASH-Works
- **FFMS2:** https://github.com/FFMS/ffms2
