# CheapUpscaler Worker Docker Image
# Multi-stage build for optimized image size

# ============================================
# Stage 1: Build Stage
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy solution and project files
COPY *.sln ./
COPY CheapUpscaler.Shared/*.csproj ./CheapUpscaler.Shared/
COPY CheapUpscaler.Core/*.csproj ./CheapUpscaler.Core/
COPY CheapUpscaler.Worker/*.csproj ./CheapUpscaler.Worker/

# Restore dependencies
RUN dotnet restore CheapUpscaler.Worker/CheapUpscaler.Worker.csproj -r linux-x64

# Copy source files
COPY CheapUpscaler.Shared/ ./CheapUpscaler.Shared/
COPY CheapUpscaler.Core/ ./CheapUpscaler.Core/
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
FROM nvidia/cuda:12.3.2-cudnn9-runtime-ubuntu22.04 AS runtime

# Avoid prompts during package installation
ENV DEBIAN_FRONTEND=noninteractive

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Python
    python3.11 \
    python3.11-venv \
    python3-pip \
    # FFmpeg
    ffmpeg \
    # Build tools for VapourSynth plugins
    build-essential \
    pkg-config \
    libffi-dev \
    # Video libraries
    libavcodec-dev \
    libavformat-dev \
    libavutil-dev \
    libswscale-dev \
    # Additional utilities
    wget \
    curl \
    git \
    # .NET runtime dependencies
    libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Install .NET 10 ASP.NET Core Runtime
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm dotnet-install.sh

# Set up Python
RUN ln -sf /usr/bin/python3.11 /usr/bin/python3 \
    && ln -sf /usr/bin/python3.11 /usr/bin/python

# Install VapourSynth
RUN pip3 install --no-cache-dir vapoursynth

# Install VapourSynth plugins for AI upscaling
RUN pip3 install --no-cache-dir \
    vsmlrt \
    vsrealesrgan \
    vspyplugin \
    numpy

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

# Expose API port
EXPOSE 5080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "CheapUpscaler.Worker.dll"]
