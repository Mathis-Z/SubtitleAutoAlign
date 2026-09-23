import os
import sys

# A PyInstaller one-file binary points LD_LIBRARY_PATH at its own unpacked
# libraries, and child processes inherit it. ffsubsync runs ffmpeg/ffprobe
# as children; Jellyfin's ffmpeg ships its own libraries and breaks when it
# picks up ours instead. Restore the caller's value before anything is spawned.
if getattr(sys, "frozen", False):
    original = os.environ.get("LD_LIBRARY_PATH_ORIG")
    if original is None:
        os.environ.pop("LD_LIBRARY_PATH", None)
    else:
        os.environ["LD_LIBRARY_PATH"] = original

from ffsubsync.ffsubsync import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
