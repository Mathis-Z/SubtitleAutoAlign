# Subtitle Auto Align

A Jellyfin plugin that automatically aligns newly downloaded subtitles to a
movie's audio using [ffsubsync](https://github.com/smacke/ffsubsync), writing
the result alongside the original with an `.autoaligned` suffix (e.g.
`Movie.en.srt` -> `Movie.en.autoaligned.srt`).

## Installation

1. In Jellyfin, go to **Dashboard -> Plugins -> Repositories**.
2. Add a repository pointing at the raw manifest URL:
   `https://raw.githubusercontent.com/Mathis-Z/subtitle-auto-align/main/manifest.json`
3. Install **Subtitle Auto Align** from **Dashboard -> Plugins -> Catalog**.

On **Linux x64**, no further setup is needed: the plugin ships with a bundled,
standalone `ffsubsync` binary (built via PyInstaller in CI) and uses it
automatically — no Python install required on the Jellyfin host. ffsubsync
needs `ffmpeg`/`ffprobe` for audio extraction; the plugin points it at the
ffmpeg Jellyfin itself uses (e.g. `/usr/lib/jellyfin-ffmpeg`), so nothing
extra has to be on `PATH`.

On other platforms (or if you'd rather use your own install), install
`ffsubsync` yourself and either put it on the `PATH` of the Jellyfin server
process, or set an absolute path in the plugin's **ffsubsync path**
configuration field — an explicit path always overrides the bundled binary.

## How it works

The plugin does **not** rely on a specific "subtitle downloaded" event from
Jellyfin's OpenSubtitles provider, because the current plugin ABI does not
expose one reliably. Instead, it watches library update events for movies and
diffs their external subtitle files against a previously observed snapshot.
Any newly-appeared, non-aligned external subtitle file is treated as eligible
for alignment — this includes subtitles from OpenSubtitles as well as other
providers or manually added files. This is a deliberate simplicity/robustness
tradeoff; see the plugin configuration if you want to disable auto-align
selectively.

For each eligible subtitle, the plugin runs:

```sh
ffsubsync <video> -i <subtitle> -o <subtitle-with-autoaligned-suffix>
```

## Configuration

- **Enable automatic alignment** — master on/off switch.
- **ffsubsync path** — leave empty (the default) to auto-detect: prefers the
  bundled binary on Linux x64, otherwise falls back to `ffsubsync` on PATH.
  Set an explicit executable name or full path to override.
- **Extra ffsubsync arguments** — appended to every invocation.
- **Timeout (seconds)** — max time allowed per alignment before it's killed.
- **Overwrite existing aligned subtitle** — re-run alignment even if an
  `.autoaligned` file already exists for the input.

## Development

```sh
dotnet build
dotnet test tests/Jellyfin.Plugin.SubtitleAutoAlign.Tests
```

### Integration tests

Integration tests exercise the real `ffsubsync` binary inside Docker against
fixture media (see
`tests/Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests/Fixtures/README.md`).
They are excluded from `dotnet test` by default via the `Integration` trait.

```sh
docker compose -f docker/integration/docker-compose.integration.yml up --build --abort-on-container-exit
```

Without Docker, point them at any ffsubsync binary (ffmpeg must be on `PATH`):

```sh
FFSUBSYNC_PATH=/path/to/ffsubsync dotnet test tests/Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests --filter Category=Integration
```

### Plugin load tests (Jellyfin 12.1 container)

These publish the plugin, install it into a throwaway `jellyfin/jellyfin:12.1`
container and check the server log. They confirm that Jellyfin actually loads
the plugin, that the subtitle watcher starts, and that no Jellyfin server
assemblies are bundled alongside the plugin. Run them on the host (Docker must
be usable by your user):

```sh
dotnet test tests/Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests --filter Category=JellyfinContainer
```

## Releasing

Releases are built via the manually-triggered **Release** GitHub Action
(`Actions -> Release -> Run workflow`). It first builds a standalone
`ffsubsync` binary for linux-x64 with PyInstaller, then publishes the plugin,
bundles that binary into the plugin's output directory (at
`bundled/linux-x64/ffsubsync`), packages everything into a zip, updates
`manifest.json` with the new version's checksum and download URL, commits
that change, and publishes a GitHub Release with the zip attached.

Only linux-x64 gets a bundled binary today — PyInstaller can't cross-compile,
so other platforms (linux-arm64, Windows, macOS) would each need their own
native build job. On those platforms the plugin transparently falls back to
`ffsubsync` resolved via PATH.
