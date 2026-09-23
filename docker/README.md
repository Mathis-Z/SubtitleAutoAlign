# Docker integration tests

Runs the integration test project inside a container with a real `ffsubsync` and
`ffmpeg` installed, exercising `SubtitleAlignmentService` against real fixture
media (see `tests/Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests/Fixtures/README.md`
for what to add before this will pass). This does not start a Jellyfin server —
it only verifies the plugin's core alignment pipeline against real ffsubsync.
A fuller test that loads the plugin into a live Jellyfin server and drives an
actual OpenSubtitles download is a possible future/manual exercise, not part of
this automated setup.

Run from the repo root:

```sh
docker compose -f docker/integration/docker-compose.integration.yml up --build --abort-on-container-exit
```
