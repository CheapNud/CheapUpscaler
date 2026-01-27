# CheapUpscaler Worker Docker Image
# Multi-stage build for optimized image size

# ============================================
# Stage 1: Build Stage
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files for restore optimization
COPY CheapUpscaler.Shared/*.csproj ./CheapUpscaler.Shared/
COPY CheapUpscaler.Core/*.csproj ./CheapUpscaler.Core/
COPY CheapUpscaler.Components/*.csproj ./CheapUpscaler.Components/
COPY CheapUpscaler.Worker/*.csproj ./CheapUpscaler.Worker/

# Restore dependencies (targets net10.0 for Linux)
RUN dotnet restore CheapUpscaler.Worker/CheapUpscaler.Worker.csproj -r linux-x64

# Copy source files
COPY CheapUpscaler.Shared/ ./CheapUpscaler.Shared/
COPY CheapUpscaler.Core/ ./CheapUpscaler.Core/
COPY CheapUpscaler.Components/ ./CheapUpscaler.Components/
COPY CheapUpscaler.Worker/ ./CheapUpscaler.Worker/

# Build and publish
RUN dotnet publish CheapUpscaler.Worker/CheapUpscaler.Worker.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish

# ============================================
# Stage 2: Runtime Stage with GPU Support
# ============================================
FROM nvidia/cuda:12.6.3-cudnn-runtime-ubuntu24.04 AS runtime

# Avoid prompts during package installation
ENV DEBIAN_FRONTEND=noninteractive

# Install base dependencies and add FFmpeg 7 PPA
RUN apt-get update && apt-get install -y --no-install-recommends \
    software-properties-common \
    && add-apt-repository -y ppa:ubuntuhandbook1/ffmpeg7 \
    && apt-get update && apt-get install -y --no-install-recommends \
    # Python (including dev headers for VapourSynth compilation)
    python3 \
    python3-dev \
    python3-venv \
    python3-pip \
    # FFmpeg 7 from PPA
    ffmpeg \
    # Build tools for VapourSynth
    build-essential \
    pkg-config \
    libffi-dev \
    autoconf \
    automake \
    libtool \
    nasm \
    # Video libraries (FFmpeg 7 dev packages)
    libavcodec-dev \
    libavformat-dev \
    libavutil-dev \
    libswscale-dev \
    libswresample-dev \
    # Additional utilities
    wget \
    curl \
    git \
    # .NET runtime dependencies
    libicu-dev \
    && apt-get remove -y software-properties-common \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

# Install .NET 10 ASP.NET Core Runtime
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm dotnet-install.sh

# Set up Python and install Cython (needed for VapourSynth build)
RUN ln -sf /usr/bin/python3 /usr/bin/python \
    && pip3 install --no-cache-dir --break-system-packages cython

# Install zimg (VapourSynth dependency)
RUN git clone --depth 1 --branch release-3.0.5 https://github.com/sekrit-twc/zimg.git /tmp/zimg \
    && cd /tmp/zimg \
    && ./autogen.sh \
    && ./configure --prefix=/usr/local \
    && make -j$(nproc) \
    && make install \
    && ldconfig \
    && rm -rf /tmp/zimg

# Install VapourSynth from source (R73 to match pip bindings)
RUN git clone --depth 1 --branch R73 https://github.com/vapoursynth/vapoursynth.git /tmp/vapoursynth \
    && cd /tmp/vapoursynth \
    && ./autogen.sh \
    && ./configure --prefix=/usr/local \
    && make -j$(nproc) \
    && make install \
    && ldconfig \
    && rm -rf /tmp/vapoursynth

# Install VapourSynth Python bindings (must match core version R73)
RUN pip3 install --no-cache-dir --break-system-packages vapoursynth==73

# Install ffms2 VapourSynth plugin (latest version for FFmpeg 7.x)
RUN git clone --depth 1 https://github.com/FFMS/ffms2.git /tmp/ffms2 \
    && cd /tmp/ffms2 \
    && ./autogen.sh \
    && ./configure --prefix=/usr/local --enable-shared \
    && make -j$(nproc) \
    && make install \
    && ldconfig \
    && mkdir -p /usr/local/lib/vapoursynth \
    && ln -sf /usr/local/lib/libffms2.so /usr/local/lib/vapoursynth/libffms2.so \
    && rm -rf /tmp/ffms2

# Install Python packages for video processing and AI upscaling
RUN pip3 install --no-cache-dir --break-system-packages \
    numpy \
    vsrealesrgan \
    vsrife \
    vskernels \
    vstools

# Pre-download Real-ESRGAN models (as root, before switching to appuser)
# This avoids permission issues when the container runs as non-root
# Model names use underscores (e.g., realesr_animevideov3, not realesr-animevideov3)
RUN python3 -c "from vsrealesrgan import download_model; \
    download_model('https://github.com/HolyWu/vs-realesrgan/releases/download/model/RealESRGAN_x4plus.pth'); \
    download_model('https://github.com/HolyWu/vs-realesrgan/releases/download/model/RealESRGAN_x4plus_anime_6B.pth'); \
    download_model('https://github.com/HolyWu/vs-realesrgan/releases/download/model/RealESRGAN_x2plus.pth'); \
    download_model('https://github.com/HolyWu/vs-realesrgan/releases/download/model/realesr_animevideov3.pth'); \
    download_model('https://github.com/HolyWu/vs-realesrgan/releases/download/model/realesr_general_x4v3.pth'); \
    print('All Real-ESRGAN models downloaded successfully')"

# Set VapourSynth plugin path
ENV VAPOURSYNTH_PLUGIN_PATH=/usr/local/lib/vapoursynth
RUN mkdir -p /usr/local/lib/vapoursynth

# Download and install vs-mlrt (TensorRT/NCNN backend for AI models)
# Install zstd from universe repository, then download and extract vs-mlrt
RUN apt-get update \
    && apt-get install -y --no-install-recommends software-properties-common \
    && add-apt-repository universe \
    && apt-get update \
    && apt-get install -y --no-install-recommends zstd \
    && wget -q https://github.com/AmusementClub/vs-mlrt/releases/download/v14.test3/vsmlrt-linux-x64-cuda.v14.test3.tar.zst -O /tmp/vsmlrt.tar.zst \
    && cd /tmp && tar --zstd -xf vsmlrt.tar.zst \
    && cp /tmp/vsmlrt-linux-x64-cuda/vsmlrt.so /usr/local/lib/vapoursynth/ \
    && cp -r /tmp/vsmlrt-linux-x64-cuda/vsmlrt-cuda/* /usr/local/lib/ 2>/dev/null || true \
    && ldconfig \
    && rm -rf /tmp/vsmlrt* \
    && apt-get remove -y software-properties-common zstd \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

# Create models directory for ONNX/TRT models
RUN mkdir -p /data/models

# Create application user
RUN useradd -m -s /bin/bash appuser

# Create directory structure
RUN mkdir -p /app /data/input /data/output /data/models /data/db /app/logs \
    && chown -R appuser:appuser /app /data

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV NVIDIA_VISIBLE_DEVICES=all
ENV NVIDIA_DRIVER_CAPABILITIES=compute,utility,video

# Application environment
ENV Worker__DataPath=/data/db
ENV Worker__InputPath=/data/input
ENV Worker__OutputPath=/data/output
ENV Worker__WatchFolderEnabled=true
ENV Worker__MaxConcurrentJobs=1
ENV Tools__FFmpegPath=/usr/bin/ffmpeg
ENV Tools__VspipePath=/usr/local/bin/vspipe
ENV Tools__PythonPath=/usr/bin/python3

WORKDIR /app

# Copy published application
COPY --from=build --chown=appuser:appuser /app/publish .

# Switch to non-root user
USER appuser

# Expose port (serves both REST API and Blazor Server UI)
EXPOSE 5080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "CheapUpscaler.Worker.dll"]
