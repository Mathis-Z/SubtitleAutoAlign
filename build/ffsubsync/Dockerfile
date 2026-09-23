# Builds the standalone ffsubsync binary bundled with the plugin (linux-x64).
# Built on bookworm so the binary runs on glibc 2.36+ (Debian 12 and newer,
# including the jellyfin/jellyfin images). Output: /build/dist/ffsubsync
FROM python:3.11-slim-bookworm

# PyInstaller needs objdump from binutils to analyse shared libraries.
RUN apt-get update \
    && apt-get install -y --no-install-recommends binutils \
    && rm -rf /var/lib/apt/lists/*

RUN pip install --no-cache-dir \
    ffsubsync==0.5.1 \
    webrtcvad-wheels==2.0.14 \
    pyinstaller==6.22.3 \
    pyinstaller-hooks-contrib==2026.7

WORKDIR /build
COPY ffsubsync_entry.py .
COPY hooks/ hooks/

RUN pyinstaller --onefile --name ffsubsync \
        --additional-hooks-dir hooks \
        --collect-all ffsubsync \
        --collect-all auditok \
        --hidden-import webrtcvad \
        ffsubsync_entry.py \
    && ./dist/ffsubsync --help > /dev/null
