# webrtcvad is ffsubsync's default speech detector, so it must be bundled.
# The module ships in the "webrtcvad-wheels" distribution, but PyInstaller's
# stock hook copies metadata for "webrtcvad" and crashes. Hooks passed via
# --additional-hooks-dir take precedence over the stock one.
from PyInstaller.utils.hooks import copy_metadata

datas = copy_metadata("webrtcvad-wheels")
