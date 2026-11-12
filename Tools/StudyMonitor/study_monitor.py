#!/usr/bin/env python3
"""
Study data monitor for the Avian Voice study: watch a recording while it is being made.

Runs next to Unity, not inside it. It only READS the files StudyRecorder writes under StudyData/
(mic.wav, output.wav, events.jsonl, trials.csv, samples.csv, conditions.csv, conditions/<condition>/ with the
trial WAVs of each of the four conditions, diagnostics/tracking_*.csv), and for
sessions recorded before samples.csv existed the shared analyzer log (avian_trials.jsonl in the Unity
persistent data folder), and serves a browser page that draws them as scrolling waveforms with automatic
health checks.

    python Tools/StudyMonitor/study_monitor.py            # opens its own window on http://127.0.0.1:8765
    python Tools/StudyMonitor/study_monitor.py --tab      # a normal browser tab instead of a window
    python Tools/StudyMonitor/study_monitor.py --root D:/StudyData --port 9000 --no-browser
    python Tools/StudyMonitor/study_monitor.py --host 0.0.0.0   # also reachable from another PC / tablet

Starting it again while it is already running does not start a second server: it only brings a window back
if none is open. The Unity editor does exactly that when a recording starts (StudyMonitorLauncher.cs).

Python 3.8+, standard library only.
"""

import argparse
import array
import csv
import datetime as dt
import io
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import threading
import time
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse
from urllib.request import Request, urlopen

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent.parent
SESSION_RE = re.compile(r"^\d{8}_\d{6}_[A-Za-z0-9_\-]+$")
TRACKING_RE = re.compile(r"^tracking_(\d{8}_\d{6})\.csv$")
MAX_PCM_FRAMES = 48000 * 40          # per request
MAX_LINES = 4000                     # per request, per text file
SAMPLE_FIELDS = ("pitchHz", "loudness", "breathDepth")            # avian_trials.jsonl
SAMPLE_CSV_FIELDS = ("pitch_hz", "loudness", "breath_depth")       # samples.csv, same order
PAGE_TIMEOUT_S = 90                  # a hidden browser window may poll as rarely as once a minute
SEGMENT_RE = re.compile(r"^(conditions/[A-Za-z0-9_\-]+/)?segments/[A-Za-z0-9_\-.]+\.wav$")
CONDITION_RE = re.compile(r"^[A-Za-z0-9_\-]+$")
ENVELOPE_BINS = 40                   # points of the small waveform drawn for each trial WAV
PAGE_API = "2"                       # what monitor.html must send as v= (raise it when the page and the server change together)
VERSION = "%d" % os.path.getmtime(__file__)   # a newer study_monitor.py replaces a server that is still running


# ---- files --------------------------------------------------------------------------------------

def default_samples_log():
    """avian_trials.jsonl written by SessionRecorder into Application.persistentDataPath (Windows Editor / PC)."""
    company, product = "DefaultCompany", "Birdflow"
    try:
        text = (PROJECT / "ProjectSettings" / "ProjectSettings.asset").read_text(encoding="utf-8", errors="ignore")
        m = re.search(r"^\s*companyName:\s*(.+)$", text, re.M)
        if m:
            company = m.group(1).strip()
        m = re.search(r"^\s*productName:\s*(.+)$", text, re.M)
        if m:
            product = m.group(1).strip()
    except OSError:
        pass
    home = os.environ.get("USERPROFILE") or str(Path.home())
    return Path(home) / "AppData" / "LocalLow" / company / product / "avian_trials.jsonl"


def list_sessions(root):
    out = []
    try:
        for entry in os.scandir(root):
            if entry.is_dir() and SESSION_RE.match(entry.name):
                out.append(entry.name)
    except OSError:
        pass
    out.sort(reverse=True)  # the name starts with yyyyMMdd_HHmmss
    return out


def wav_info(path):
    """Sample rate / channels / frame count of a 16-bit PCM WAV that may still be open for writing
    (StudyRecorder patches the RIFF sizes only on close, so the length comes from the file size)."""
    try:
        size = os.path.getsize(path)
        with open(path, "rb") as f:
            head = f.read(44)
        mtime = os.path.getmtime(path)
    except OSError:
        return None
    if len(head) < 44 or head[0:4] != b"RIFF" or head[8:12] != b"WAVE":
        return None
    channels, rate = struct.unpack_from("<HI", head, 22)
    bits = struct.unpack_from("<H", head, 34)[0]
    if bits != 16 or channels < 1 or rate < 1:
        return None
    frames = max(0, size - 44) // (2 * channels)
    # "finalized": the length in the header is the length of the file, i.e. Unity closed it properly.
    header_frames = struct.unpack_from("<I", head, 40)[0] // (2 * channels)
    return {"rate": rate, "channels": channels, "frames": frames, "mtime": mtime, "size": size,
            "finalized": header_frames == frames}


def wav_header(rate, frames):
    n = frames * 2
    return b"RIFF" + struct.pack("<I", 36 + n) + b"WAVEfmt " + struct.pack("<IHHIIHH", 16, 1, 1, rate, rate * 2, 2, 16) + b"data" + struct.pack("<I", n)


_wav_levels = {}   # path -> ((size, mtime), {"peak", "rms", "env"})


def wav_level(path, info):
    """Peak, RMS (both 0..1) and a short envelope (peak per bin, 0..1) of a finished 16-bit WAV: is there sound
    in it? Computed once per file and kept (a trial WAV is written in one go and never changes)."""
    key = (info["size"], info["mtime"])
    hit = _wav_levels.get(path)
    if hit and hit[0] == key:
        return hit[1]
    try:
        with open(path, "rb") as f:
            f.seek(44)
            raw = f.read()
    except OSError:
        return None
    a = array.array("h")
    a.frombytes(raw[: len(raw) // 2 * 2])
    if sys.byteorder != "little":
        a.byteswap()
    ch = max(1, info["channels"])
    if ch > 1:
        a = a[::ch]
    n = len(a)
    level = {"peak": 0.0, "rms": 0.0, "env": []}
    if n:
        step = max(1, -(-n // ENVELOPE_BINS))
        env = []
        for i in range(0, n, step):
            part = a[i:i + step]
            env.append(max(max(part), -min(part)) / 32768.0)
        thin = a[::8] if n > 80000 else a          # the RMS of every 8th sample is close enough for a health check
        level = {"peak": round(max(env), 5), "rms": round((sum(x * x for x in thin) / len(thin)) ** 0.5 / 32768.0, 6),
                 "env": [round(v, 4) for v in env]}
    _wav_levels[path] = (key, level)
    return level


def scan_segments(folder):
    """Every trial WAV of a session, keyed by its path relative to the session folder (what trials.csv calls
    segment_file): conditions/<condition>/segments/*.wav, and segments/*.wav of older recordings."""
    found = {}
    places = [("segments", os.path.join(folder, "segments"))]
    try:
        for entry in os.scandir(os.path.join(folder, "conditions")):
            if entry.is_dir() and CONDITION_RE.match(entry.name):
                places.append(("conditions/%s/segments" % entry.name, os.path.join(entry.path, "segments")))
    except OSError:
        pass
    for rel, place in places:
        try:
            entries = list(os.scandir(place))
        except OSError:
            continue
        for entry in entries:
            if not entry.name.lower().endswith(".wav"):
                continue
            info = wav_info(entry.path)
            if not info:
                found[rel + "/" + entry.name] = {"size": os.path.getsize(entry.path), "frames": 0, "rate": 0, "finalized": False}
                continue
            level = wav_level(entry.path, info) if info["finalized"] else None
            if level:
                info = dict(info, peak=level["peak"], rms=level["rms"])
            found[rel + "/" + entry.name] = info
    return found


def scan_conditions(folder):
    """The condition folders of a session: what is saved in each (None for recordings made before they existed)."""
    base = os.path.join(folder, "conditions")
    if not os.path.isdir(base):
        return None
    out = {}
    for entry in os.scandir(base):
        if not entry.is_dir() or not CONDITION_RE.match(entry.name):
            continue
        table = read_csv(os.path.join(entry.path, "trials.csv"))
        out[entry.name] = {"trial_rows": len(table["rows"]) if table else 0,
                           "files": {name: file_entry(os.path.join(entry.path, name)) for name in ("events.jsonl", "trials.csv", "samples.csv")}}
    return out


def file_entry(path):
    try:
        st = os.stat(path)
    except OSError:
        return None
    return {"size": st.st_size, "mtime": st.st_mtime * 1000.0}


def read_csv(path):
    """trials.csv exactly as saved: header + complete rows (a row still being written is left out)."""
    try:
        with open(path, "r", encoding="utf-8", newline="") as f:
            text = f.read()
    except OSError:
        return None
    if text and not text.endswith("\n"):
        text = text[: text.rfind("\n") + 1]
    rows = [r for r in csv.reader(io.StringIO(text)) if r]
    return {"header": rows[0] if rows else [], "rows": rows[1:]}


def read_pcm(path, start, count, limit=MAX_PCM_FRAMES):
    """Mono int16 little-endian frames [start, start+count) of a WAV written by WavWriter (44-byte header)."""
    info = wav_info(path)
    if info is None:
        return None, b""
    ch = info["channels"]
    start = max(0, min(start, info["frames"]))
    count = max(0, min(count, info["frames"] - start, limit))
    with open(path, "rb") as f:
        f.seek(44 + start * 2 * ch)
        data = f.read(count * 2 * ch)
    data = data[: len(data) - (len(data) % (2 * ch))]
    if ch > 1:  # not produced by StudyRecorder, but keep the page's mono assumption true
        n = len(data) // (2 * ch)
        samples = struct.unpack("<%dh" % (n * ch), data)
        data = struct.pack("<%dh" % n, *[sum(samples[i * ch:(i + 1) * ch]) // ch for i in range(n)])
    info["from"] = start
    info["count"] = len(data) // 2
    return info, data


def read_lines(path, offset):
    """Complete lines appended after byte `offset`. Returns (lines, next_offset, size)."""
    try:
        size = os.path.getsize(path)
    except OSError:
        return [], 0, 0
    if offset < 0 or offset > size:
        offset = 0
    lines = []
    with open(path, "rb") as f:
        f.seek(offset)
        pos = offset
        while len(lines) < MAX_LINES:
            raw = f.readline()
            if not raw or not raw.endswith(b"\n"):
                break  # nothing more, or a line still being written
            pos += len(raw)
            text = raw.decode("utf-8", errors="replace").strip()
            if text:
                lines.append(text)
    return lines, pos, size


def parse_iso_ms(text):
    """ISO-8601 with 7 fractional digits and Z or an offset (what .NET "o" writes) -> epoch milliseconds."""
    m = re.match(r"^(\d{4})-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:\.(\d+))?(Z|[+-]\d\d:\d\d)?$", text or "")
    if not m:
        return None
    y, mo, d, h, mi, s = (int(m.group(i)) for i in range(1, 7))
    frac = float("0." + m.group(7)) if m.group(7) else 0.0
    zone = m.group(8)
    if zone in (None, "Z"):
        tz = dt.timezone.utc
    else:
        sign = 1 if zone[0] == "+" else -1
        tz = dt.timezone(sign * dt.timedelta(hours=int(zone[1:3]), minutes=int(zone[4:6])))
    return (dt.datetime(y, mo, d, h, mi, s, tzinfo=tz).timestamp() + frac) * 1000.0


def local_stamp_to_epoch(stamp):
    """yyyyMMdd_HHmmss in this PC's local time -> epoch seconds."""
    try:
        return time.mktime(time.strptime(stamp, "%Y%m%d_%H%M%S"))
    except ValueError:
        return None


def find_tracking_file(root, session_id):
    """The tracking health log of the Play session this recording belongs to: the newest log that started
    before the recording did and was still being written after it started."""
    start = local_stamp_to_epoch(session_id[:15])
    if start is None:
        return None, None
    best = None
    folder = Path(root) / "diagnostics"
    try:
        for entry in os.scandir(folder):
            m = TRACKING_RE.match(entry.name)
            if not m:
                continue
            t0 = local_stamp_to_epoch(m.group(1))
            if t0 is None or t0 > start + 5:
                continue
            # os.stat, not entry.stat(): Windows keeps the directory entry of a file that is still open stale.
            if os.stat(entry.path).st_mtime < start - 5:
                continue
            if best is None or t0 > best[1]:
                best = (entry.path, t0)
    except OSError:
        return None, None
    return best if best else (None, None)


def _line_time_at(f, offset, size):
    """Timestamp (epoch ms) of the first complete line starting at or after `offset`, and where it starts."""
    f.seek(offset)
    if offset > 0:
        f.readline()  # skip the partial line
    while True:
        pos = f.tell()
        if pos >= size:
            return None, size
        raw = f.readline()
        if not raw:
            return None, size
        # jsonl: {"timestamp":"...", ...}   csv: the row starts with t_utc
        m = re.search(rb'(?:^|"timestamp":")(\d{4}-\d\d-\d\dT[0-9:.]+(?:Z|[+-]\d\d:\d\d)?)', raw)
        if m:
            t = parse_iso_ms(m.group(1).decode("ascii", errors="ignore"))
            if t is not None:
                return t, pos


def seek_samples(path, t_ms):
    """Byte offset of the first analyzer sample at or after t_ms (the log is appended in time order)."""
    try:
        size = os.path.getsize(path)
    except OSError:
        return 0
    with open(path, "rb") as f:
        lo, hi = 0, size
        while hi - lo > 4096:
            mid = (lo + hi) // 2
            t, _ = _line_time_at(f, mid, size)
            if t is None or t >= t_ms:
                hi = mid
            else:
                lo = mid
        _, pos = _line_time_at(f, lo, size) if lo > 0 else (None, 0)
        return pos


def read_samples(path, offset, t0_ms, is_csv):
    """Analyzer samples as [epoch_ms, pitchHz, loudness, breathDepth] rows, from a byte offset, or from the
    first sample at or after t0_ms when the page has nothing yet / jumped somewhere else. `is_csv`: the
    session's own samples.csv (StudyRecorder), otherwise the shared avian_trials.jsonl (SessionRecorder)."""
    if path is None or not os.path.isfile(path):
        return {"available": False, "rows": [], "next": 0}
    if t0_ms is not None:
        offset = seek_samples(path, t0_ms)
    lines, nxt, _ = read_lines(path, offset)
    rows = []
    if is_csv:
        try:
            with open(path, "r", encoding="utf-8") as f:
                header = f.readline().strip().split(",")
            cols = [header.index(k) for k in SAMPLE_CSV_FIELDS]
        except (OSError, ValueError):
            return {"available": False, "rows": [], "next": 0}
        for r in csv.reader(lines):
            t = parse_iso_ms(r[0]) if r else None
            if t is None or len(r) <= max(cols):
                continue
            try:
                rows.append([t] + [float(r[i]) if r[i] else 0.0 for i in cols])
            except ValueError:
                continue
        return {"available": True, "rows": rows, "next": nxt, "from": offset}
    for line in lines:
        try:
            o = json.loads(line)
        except ValueError:
            continue
        t = parse_iso_ms(o.get("timestamp"))
        if t is None:
            continue
        rows.append([t] + [o.get(k) or 0 for k in SAMPLE_FIELDS])
    return {"available": True, "rows": rows, "next": nxt, "from": offset}


# ---- HTTP ---------------------------------------------------------------------------------------

class Handler(BaseHTTPRequestHandler):
    server_version = "StudyMonitor/1.0"
    protocol_version = "HTTP/1.1"
    root = ""
    samples_log = None
    pages = {}          # page id -> time of its last poll

    @classmethod
    def page_open(cls):
        now = time.time()
        return any(now - t < PAGE_TIMEOUT_S for t in list(cls.pages.values()))

    def log_message(self, fmt, *args):  # quiet: the page polls several times a second
        pass

    def _send(self, code, body, ctype, extra=None):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        for k, v in (extra or {}).items():
            self.send_header(k, str(v))
        self.end_headers()
        self.wfile.write(body)

    def _json(self, obj, code=200):
        self._send(code, json.dumps(obj, ensure_ascii=False, allow_nan=False).encode("utf-8"),
                   "application/json; charset=utf-8")

    def _session_dir(self, q):
        """Resolve ?session= to a folder that really is a session under the root (never a free path)."""
        sessions = list_sessions(self.root)
        sid = (q.get("session") or ["latest"])[0]
        if sid == "latest":
            sid = sessions[0] if sessions else ""
        if sid not in sessions:
            return None, sessions
        return sid, sessions

    def do_GET(self):
        try:
            url = urlparse(self.path)
            q = parse_qs(url.query)
            if url.path in ("/", "/index.html"):
                self._send(200, (HERE / "monitor.html").read_bytes(), "text/html; charset=utf-8")
            elif url.path == "/api/state":
                if (q.get("v") or [""])[0] != PAGE_API:
                    # A window left open by an older monitor.html: it cannot show what this server sends.
                    self._json({"error": "This window shows an older version of the monitor. Press F5 to update it (or close it: a new window opens with the next recording)."})
                    return
                Handler.pages[(q.get("page") or ["?"])[0]] = time.time()
                self._state(q)
            elif url.path == "/api/ping":
                self._json({"app": "study_monitor", "root": self.root, "page_open": self.page_open(), "version": VERSION})
            elif url.path == "/api/segments":
                self._segments(q)
            elif url.path == "/api/pcm":
                self._pcm(q)
            elif url.path == "/api/audio":
                self._audio(q)
            else:
                self._send(404, b"not found", "text/plain")
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass
        except Exception as e:  # keep serving whatever one request ran into
            try:
                self._json({"error": "%s: %s" % (type(e).__name__, e)}, 500)
            except OSError:
                pass

    def do_POST(self):
        """The page says goodbye when its window closes, so the next recording opens a new one at once."""
        try:
            url = urlparse(self.path)
            length = int(self.headers.get("Content-Length") or 0)
            if length:
                self.rfile.read(length)
            if url.path == "/api/bye":
                Handler.pages.pop((parse_qs(url.query).get("page") or ["?"])[0], None)
            self._send(204, b"", "text/plain")
            if url.path == "/api/quit":
                # A newer study_monitor.py takes over (its pages reconnect to it on the same port).
                threading.Timer(0.2, lambda: os._exit(0)).start()
        except (OSError, ValueError):
            pass

    def _pcm(self, q):
        sid, _ = self._session_dir(q)
        name = (q.get("file") or ["mic"])[0]
        if sid is None or name not in ("mic", "output"):
            self._send(404, b"", "application/octet-stream")
            return
        start = int((q.get("from") or ["0"])[0])
        count = int((q.get("count") or [str(MAX_PCM_FRAMES)])[0])
        info, data = read_pcm(os.path.join(self.root, sid, name + ".wav"), start, count)
        if info is None:
            self._send(404, b"", "application/octet-stream")
            return
        self._send(200, data, "application/octet-stream",
                   {"X-Rate": info["rate"], "X-Total": info["frames"], "X-From": info["from"], "X-Count": info["count"]})

    def _segments(self, q):
        """The small waveform of every finished trial WAV: {segment_file: [peak per bin, 0..1]}."""
        sid, _ = self._session_dir(q)
        if sid is None:
            self._json({})
            return
        folder = os.path.join(self.root, sid)
        out = {}
        for rel, info in scan_segments(folder).items():
            if info.get("finalized"):
                level = wav_level(os.path.join(folder, *rel.split("/")), info)
                if level:
                    out[rel] = level["env"]
        self._json(out)

    def _audio(self, q):
        """A playable WAV made from what is on disk: one saved trial WAV (?seg=<segment_file of trials.csv>), or a
        stretch of mic.wav / output.wav (?file=mic&from=&count=), which have no valid header while Unity writes."""
        sid, _ = self._session_dir(q)
        if sid is None:
            self._send(404, b"", "audio/wav")
            return
        seg = (q.get("seg") or [""])[0]
        if seg:
            if "/" not in seg:
                seg = "segments/" + seg
            if not SEGMENT_RE.match(seg) or ".." in seg:
                self._send(404, b"", "audio/wav")
                return
            path = os.path.join(self.root, sid, *seg.split("/"))
            name = None
        else:
            name = (q.get("file") or ["mic"])[0]
            if name not in ("mic", "output"):
                self._send(404, b"", "audio/wav")
                return
            path = os.path.join(self.root, sid, name + ".wav")
        start = int(float((q.get("from") or ["0"])[0]))
        count = int(float((q.get("count") or [str(MAX_PCM_FRAMES * 4)])[0]))
        info, data = read_pcm(path, start, count, MAX_PCM_FRAMES * 4)
        if info is None:
            self._send(404, b"", "audio/wav")
            return
        self._send(200, wav_header(info["rate"], len(data) // 2) + data, "audio/wav")

    def _state(self, q):
        def num(key, default):
            try:
                return int(float((q.get(key) or [default])[0]))
            except ValueError:
                return default

        sid, sessions = self._session_dir(q)
        out = {"server_time_ms": time.time() * 1000.0, "root": self.root, "sessions": sessions[:200], "session": sid,
               "page_version": "%d" % os.path.getmtime(HERE / "monitor.html")}
        if sid is None:
            self._json(out)
            return

        folder = os.path.join(self.root, sid)
        same = (q.get("cur") or [""])[0] == sid  # offsets only mean something for the session they came from
        try:
            with open(os.path.join(folder, "session.json"), "r", encoding="utf-8") as f:
                out["info"] = json.load(f)
        except (OSError, ValueError):
            out["info"] = None

        out["mic"] = wav_info(os.path.join(folder, "mic.wav"))
        out["output"] = wav_info(os.path.join(folder, "output.wav"))

        lines, nxt, _ = read_lines(os.path.join(folder, "events.jsonl"), num("ev", 0) if same else 0)
        events = []
        for line in lines:
            try:
                events.append(json.loads(line))
            except ValueError:
                pass
        out["events"] = {"rows": events, "next": nxt, "reset": not same}

        table = read_csv(os.path.join(folder, "trials.csv"))
        out["trials_csv"] = table
        out["trial_rows"] = len(table["rows"]) if table else 0
        out["segments"] = scan_segments(folder)
        out["conditions"] = scan_conditions(folder)
        out["conditions_csv"] = read_csv(os.path.join(folder, "conditions.csv"))

        track_path, track_t0 = find_tracking_file(self.root, sid)
        files = {"folder": folder}
        for name in ("session.json", "events.jsonl", "trials.csv", "samples.csv", "conditions.csv", "mic.wav", "output.wav"):
            files[name] = file_entry(os.path.join(folder, name))
        files["tracking"] = dict(file_entry(track_path) or {}, path=track_path) if track_path else None
        entry = file_entry(self.samples_log) if self.samples_log else None
        files["samples"] = dict(entry, path=self.samples_log) if entry else None
        out["files"] = files
        try:
            usage = shutil.disk_usage(self.root)
            out["disk"] = {"free": usage.free, "total": usage.total}
        except OSError:
            out["disk"] = None
        # Earlier recordings with the same participant ID (the folder name is <date>_<time>_<participant>).
        out["same_participant"] = [s for s in sessions if s != sid and s[16:] == sid[16:] and s < sid]
        if track_path:
            name = os.path.basename(track_path)
            keep = same and (q.get("trkf") or [""])[0] == name
            offset = num("trk", 0) if keep else 0
            lines, nxt, _ = read_lines(track_path, offset)
            header = None
            if offset == 0 and lines:
                header, lines = lines[0].split(","), lines[1:]
            out["tracking"] = {"file": name, "start_ms": track_t0 * 1000.0, "header": header,
                               "rows": [l.split(",") for l in lines], "next": nxt, "reset": not keep}
        else:
            out["tracking"] = None

        # Samples: the session's own samples.csv when it has one, the log shared by all sessions otherwise.
        own = os.path.join(folder, "samples.csv")
        source, spath = ("session", own) if os.path.isfile(own) else ("shared", self.samples_log)
        smp_t0 = q.get("smp_t0")
        usable = (q.get("smpsrc") or [""])[0] == source      # an offset belongs to the file it came from
        if usable and (smp_t0 or (same and "smp" in q)):
            out["samples"] = read_samples(spath, num("smp", 0), float(smp_t0[0]) if smp_t0 else None, source == "session")
        else:
            out["samples"] = {"available": bool(spath and os.path.isfile(spath)), "rows": [], "next": -1}
        out["samples"]["source"] = source
        self._json(out)


class Server(ThreadingHTTPServer):
    # The default (True) lets several servers listen on the same port on Windows: a second monitor would start
    # on a port that is taken and the browser would reach either of them. False makes the bind fail as it should.
    allow_reuse_address = False
    daemon_threads = True


def open_window(url, tab):
    """Show the page in a window of its own (Edge / Chrome app window), or in a browser tab."""
    if not tab:
        candidates = []
        for base in (os.environ.get("ProgramFiles(x86)"), os.environ.get("ProgramFiles"), os.environ.get("LOCALAPPDATA")):
            if base:
                candidates.append(os.path.join(base, "Microsoft", "Edge", "Application", "msedge.exe"))
                candidates.append(os.path.join(base, "Google", "Chrome", "Application", "chrome.exe"))
        for exe in candidates:
            if os.path.isfile(exe):
                try:
                    subprocess.Popen([exe, "--app=" + url, "--window-size=1500,1000"],
                                     stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                    return
                except OSError:
                    pass
    webbrowser.open(url)


def find_running(port, root):
    """A monitor that already serves this StudyData folder: (url, page_open), or None. One started from an older
    study_monitor.py is told to quit, so the caller starts the current one in its place."""
    found = {}
    old = []

    def probe(p):
        url = "http://127.0.0.1:%d/" % p
        try:
            with urlopen(url + "api/ping", timeout=1.0) as r:
                info = json.loads(r.read().decode("utf-8"))
        except (OSError, ValueError):
            return
        if info.get("app") == "study_monitor" and os.path.normcase(info.get("root", "")) == os.path.normcase(root):
            if info.get("version") == VERSION:
                found[p] = (url, bool(info.get("page_open")))
            else:
                old.append(url)

    # All ports at once: on Windows a closed local port only fails after the timeout, not at once.
    threads = [threading.Thread(target=probe, args=(p,), daemon=True) for p in range(port, port + 20)]
    for t in threads:
        t.start()
    for t in threads:
        t.join(1.5)
    for url in old:
        try:
            urlopen(Request(url + "api/quit", data=b"", method="POST"), timeout=1.0).close()
        except (OSError, ValueError):
            pass
    if old:
        time.sleep(0.6)   # its port is free again
    return found[min(found)] if found else None


def main():
    ap = argparse.ArgumentParser(description="Live monitor for Avian Voice study recordings (reads StudyData, independent of Unity).")
    ap.add_argument("--root", default=str(PROJECT / "StudyData"), help="StudyData folder (default: <project>/StudyData)")
    ap.add_argument("--samples-log", default=None, help="avian_trials.jsonl (default: the Unity persistent data folder of this project)")
    ap.add_argument("--host", default="127.0.0.1", help="127.0.0.1 = this PC only (default); 0.0.0.0 = also other devices on the network")
    ap.add_argument("--port", type=int, default=8765)
    ap.add_argument("--tab", action="store_true", help="open a browser tab instead of a window of its own")
    ap.add_argument("--no-browser", action="store_true", help="do not open the page automatically")
    ap.add_argument("--exit-when-idle", type=float, default=0, metavar="SECONDS",
                    help="stop the server after its window has been closed for this long (0 = keep running)")
    args = ap.parse_args()

    Handler.root = os.path.abspath(args.root)
    Handler.samples_log = os.path.abspath(args.samples_log) if args.samples_log else str(default_samples_log())

    running = find_running(args.port, Handler.root)
    if running:
        url, page_open = running
        if page_open or args.no_browser:
            print("Study monitor already running: " + url)
        else:
            print("Study monitor already running, opening its window: " + url)
            open_window(url, args.tab)
        return

    server = None
    for port in range(args.port, args.port + 20):
        try:
            server = Server((args.host, port), Handler)
            break
        except OSError:
            continue
    if server is None:
        sys.exit("No free port in %d-%d." % (args.port, args.port + 19))
    server.daemon_threads = True

    url = "http://%s:%d/" % ("127.0.0.1" if args.host in ("0.0.0.0", "") else args.host, server.server_address[1])
    print("Study monitor: " + url)
    print("  StudyData   : " + Handler.root + ("" if os.path.isdir(Handler.root) else "   (does not exist yet)"))
    print("  sample log  : " + Handler.samples_log + ("" if os.path.isfile(Handler.samples_log) else "   (not found: breath / analyzer strips stay empty)"))
    print("  Ctrl+C to stop. Unity is never touched; the files are only read.")
    sys.stdout.flush()
    if not args.no_browser:
        # A window left open by an earlier run reconnects within a moment; only open one if none did.
        threading.Timer(1.5, lambda: None if Handler.page_open() else open_window(url, args.tab)).start()

    if args.exit_when_idle > 0:
        def watchdog():
            idle_since = time.time()
            while True:
                time.sleep(5)
                if Handler.page_open():
                    idle_since = time.time()
                elif time.time() - idle_since > args.exit_when_idle:
                    os._exit(0)
        threading.Thread(target=watchdog, daemon=True).start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nstopped")


if __name__ == "__main__":
    main()
