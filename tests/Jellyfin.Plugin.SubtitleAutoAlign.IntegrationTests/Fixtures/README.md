# Integration test fixtures

- `us-now-5min.mp4` — the first 5 minutes of the documentary *Us Now* (2009),
  shrunk for the repo (160px wide, 5 fps, mono 48 kbit/s AAC); ffsubsync only
  uses the audio.
- `us-now-5min.en.srt` — the film's English subtitle, trimmed to the same
  5 minutes (109 cues). It is in sync with the film (ffsubsync measures about
  -0.45 s), so tests shift it by a known amount and check that alignment
  restores the original timings.

*Us Now* was released by its makers under a Creative Commons licence; check
the exact terms before redistributing these files outside this repository.

To regenerate from the full film (`Us Now.mp4`, `Us_Now_eng.srt`):

```sh
ffmpeg -i "Us Now.mp4" -t 300 -vf "scale=160:-2,fps=5" -c:v libopenh264 -b:v 40k \
  -c:a aac -b:a 48k -ac 1 -ar 24000 -movflags +faststart us-now-5min.mp4
```

and keep only the subtitle cues that start before 00:05:00, clamping their end
times to 00:05:00.
