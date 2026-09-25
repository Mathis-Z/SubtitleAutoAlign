# Subtitle Auto Align

A Jellyfin plugin that automatically aligns newly downloaded subtitles to a
movie's audio using [ffsubsync](https://github.com/smacke/ffsubsync), writing
the result alongside the original with an `.autoaligned` suffix (e.g.
`Movie.en.srt` -> `Movie.en.autoaligned.srt`).
Note: This plugin is completely AI-written so use this at your own risk.

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

### Aligning existing subtitles

Subtitles that were already in your library before the plugin was installed
(or that appeared while automatic alignment was off) can be aligned in one go
with the **Align all subtitles** scheduled task. Start it with the **Align all
subtitles now** button on the plugin's settings page, or under **Dashboard ->
Scheduled Tasks**, where you can also follow its progress, cancel it, or give it
a schedule.

The task searches every library folder recursively for subtitle files
(`.srt`, `.ass`, `.ssa`, `.sub`, `.vtt`) and matches each to the video in the
same folder whose name it starts with, following Jellyfin's naming
(`Movie.en.forced.srt` belongs to `Movie.mkv`). This also covers TV episodes
and subtitles Jellyfin hasn't indexed yet. Subtitles Jellyfin has indexed for
movies are included too, even if they're stored outside the library folders.
Anything that already has an `.autoaligned` copy is skipped unless
**Overwrite existing aligned subtitle** is on; subtitles with no matching video
are listed in the log.

## Configuration

- **Enable automatic alignment** — align new subtitles as they appear. Doesn't
  affect the **Align all subtitles** task.
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
assemblies are bundled alongside the plugin. An end-to-end test also builds
the bundled ffsubsync (`build/ffsubsync/Dockerfile`, same as releases), sets
Jellyfin up with a Movies library containing the *Us Now* clip, adds a
subtitle shifted by 6 s and checks that the plugin writes a correctly aligned
`.autoaligned.srt`. Run them on the host (Docker must be usable by your user):

```sh
dotnet test tests/Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests --filter Category=JellyfinContainer
```

## Releasing

Releases are built via the manually-triggered **Release** GitHub Action
(`Actions -> Release -> Run workflow`). It first runs the full test suite
(the **Tests** workflow, which also runs on every push and pull request) and
stops if anything fails. It builds a standalone `ffsubsync` binary for
linux-x64 with PyInstaller (`build/ffsubsync/Dockerfile`), then publishes the plugin,
bundles that binary into the plugin's output directory (at
`bundled/linux-x64/ffsubsync`), packages everything into a zip, updates
`manifest.json` with the new version's checksum and download URL, commits
that change, and publishes a GitHub Release with the zip attached.

Only linux-x64 gets a bundled binary today — PyInstaller can't cross-compile,
so other platforms (linux-arm64, Windows, macOS) would each need their own
native build job. On those platforms the plugin transparently falls back to
`ffsubsync` resolved via PATH.
