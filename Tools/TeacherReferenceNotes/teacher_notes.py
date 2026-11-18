"""Teacher bird guide notes: cut them from a session's output.wav, measure them, and render the same
three notes cleanly with a port of BirdSongSynth (PlayChirp path). Pure standard library."""
import array, csv, json, math, os, statistics, sys, wave

session, out_dir = sys.argv[1], sys.argv[2]
os.makedirs(out_dir, exist_ok=True)
RATE = 48000

# AvianVoiceSettings.asset (the values the scene uses) and the PlayChirp call of the guide note.
S = dict(pitch_ratio=8.0, trill_rate_min=18.0, trill_rate_max=28.0, trill_depth_st=0.6,
         glide_st=2.5, glide_s=0.04, attack_s=0.008, release_s=0.015, harmonic2=0.18,
         noise=0.06, volume=0.65, pitch_smooth_s=0.02)
GUIDE_LOUDNESS, GUIDE_S = 0.92, 0.9
LEVEL = 0.5 + 0.5 * GUIDE_LOUDNESS
TRILL_RATE = S["trill_rate_min"] + (S["trill_rate_max"] - S["trill_rate_min"]) * GUIDE_LOUDNESS
NOTE_NAMES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]


def note_name(hz):
    m = round(69 + 12 * math.log2(hz / 440.0))
    return NOTE_NAMES[m % 12] + str(m // 12 - 1)


def write_wav(path, samples):
    pcm = array.array("h", (max(-32768, min(32767, int(round(s * 32767)))) for s in samples))
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(pcm.tobytes())


def render(human_hz, seconds=GUIDE_S, tail=0.2):
    """BirdSongSynth.OnAudioFilterRead for one chirp from a fresh synth."""
    goal = min(5500.0, max(640.0, min(900.0, max(80.0, human_hz)) * S["pitch_ratio"]))
    dt = 1.0 / RATE
    attack = 1 - math.exp(-dt / S["attack_s"]); release = 1 - math.exp(-dt / S["release_s"])
    frames_left = round(seconds * RATE)
    total = frames_left + round(tail * RATE)
    env = 0.0; phase = 0.0; trill_phase = 0.0; glide_rem = S["glide_s"]
    low = band = 0.0; noise_state = 0x9E3779B9
    h2 = S["harmonic2"]; out = []
    for _ in range(total):
        is_open = frames_left > 0
        if is_open:
            frames_left -= 1
        target = LEVEL if is_open else 0.0
        env += (target - env) * (attack if target > env else release)
        if env < 0.0001 and not is_open:
            out.append(0.0); continue
        glide = 0.0
        if glide_rem > 0:
            glide = -S["glide_st"] * (glide_rem / S["glide_s"]); glide_rem -= dt
        trill_phase += TRILL_RATE * dt
        if trill_phase >= 1: trill_phase -= 1
        trill = math.sin(trill_phase * 2 * math.pi)
        hz = goal * 2 ** ((glide + trill * S["trill_depth_st"]) / 12)
        phase += hz * dt
        if phase >= 1: phase -= 1
        p = phase * 2 * math.pi
        tone = math.sin(p) + h2 * math.sin(2 * p)
        noise_state ^= (noise_state << 13) & 0xFFFFFFFF
        noise_state ^= noise_state >> 17
        noise_state ^= (noise_state << 5) & 0xFFFFFFFF
        white = noise_state / 0xFFFFFFFF * 2 - 1
        f = 2 * math.sin(math.pi * min(hz, RATE * 0.16) / RATE)
        high = white - low - 0.7 * band
        band += f * high; low += f * band
        out.append((tone / (1 + h2) + band * S["noise"] * 2) * env * (0.85 + 0.15 * trill) * S["volume"])
    return goal, out


def highpass(x, fc=1000.0):
    """2nd-order Butterworth high-pass: keeps the whistle (2-3 kHz), removes a voice under it."""
    w = 2 * math.pi * fc / RATE; al = math.sin(w) / math.sqrt(2); c = math.cos(w)
    b0, b1, b2, a0, a1, a2 = (1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + al, -2 * c, 1 - al
    y = []; x1 = x2 = y1 = y2 = 0.0
    for v in x:
        o = (b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, v, y1, o
        y.append(o)
    return y


def measure(x, ev):
    """ev = index of the guide_start event in x. Level and pitch from the steady part (0.2-0.85 s after the
    event); onset / duration at 10 % of the peak, only when nothing was sounding before the event."""
    peak = max(abs(v) for v in x[ev:])
    if peak < 0.005:
        return None
    pre = x[max(0, ev - int(0.05 * RATE)):ev]
    pre_rms = math.sqrt(sum(v * v for v in pre) / len(pre)) if pre else 0.0
    thr = 0.1 * peak
    on = next(i for i, v in enumerate(x) if abs(v) > thr)
    off = len(x) - 1 - next(i for i, v in enumerate(reversed(x)) if abs(v) > thr)
    a, b = ev + int(0.2 * RATE), min(ev + int(0.85 * RATE), len(x) - 1)
    raw = x[a:b]
    rms = math.sqrt(sum(v * v for v in raw) / len(raw))
    steady = highpass(x)[a:b]
    zc = [a + i + (-steady[i]) / (steady[i + 1] - steady[i])
          for i in range(len(steady) - 1) if steady[i] < 0 <= steady[i + 1]]
    f_mean = (len(zc) - 1) * RATE / (zc[-1] - zc[0])
    k = 4
    t = [(zc[i] + zc[i + k]) / 2 / RATE for i in range(0, len(zc) - k)]
    fr = [k * RATE / (zc[i + k] - zc[i]) for i in range(0, len(zc) - k)]
    fs = sorted(fr)
    lo, hi = fs[int(0.02 * len(fs))], fs[int(0.98 * len(fs))]
    ups = [t[i] for i in range(len(fr) - 1) if fr[i] < f_mean <= fr[i + 1]]
    gaps = [g for g in (ups[i + 1] - ups[i] for i in range(len(ups) - 1)) if g > 0.015]
    trill = 1 / statistics.median(gaps) if gaps else float("nan")
    return dict(onset=on, duration_s=(off - on) / RATE, peak=peak, rms_dbfs=20 * math.log10(rms), pre_rms=pre_rms,
                f_mean_hz=f_mean, f_low_hz=lo, f_high_hz=hi, trill_hz=trill)


# ---- 1. every guide note of the session, cut from output.wav --------------------------------------
guides = []
with open(os.path.join(session, "events.jsonl"), encoding="utf-8") as fh:
    for line in fh:
        if '"guide_start"' in line:
            e = json.loads(line)
            if e.get("task") == "singing":
                guides.append(e)

wav = wave.open(os.path.join(session, "output.wav"), "rb")
assert wav.getframerate() == RATE and wav.getnchannels() == 1 and wav.getsampwidth() == 2
rows, clips = [], {}
PRE, POST = int(0.05 * RATE), int(1.15 * RATE)
for e in guides:
    start = max(0, e["out_frame"] - PRE)
    wav.setpos(start)
    x = [v / 32768.0 for v in array.array("h", wav.readframes(PRE + POST))]
    m = measure(x, PRE)
    hz = round(e["target_hz"], 2)
    row = dict(condition=e["condition"], trial=e["trial"], run=e.get("run", 1), teacher_voice=e["teacher_voice"],
               target_human_hz=hz, note=note_name(hz), expected_bird_hz=round(hz * S["pitch_ratio"], 2),
               out_frame_guide_start=e["out_frame"])
    if m is None:
        row.update(found=0)
    else:
        quiet = m["pre_rms"] < 0.003  # nothing sounding in the 50 ms before the event
        row.update(found=1, sound_before_note=0 if quiet else 1,
                   sound_before_dbfs=round(20 * math.log10(max(m["pre_rms"], 1e-5)), 1),
                   onset_after_event_ms=round((start + m["onset"] - e["out_frame"]) / RATE * 1000, 1) if quiet else "",
                   duration_s=round(m["duration_s"], 3) if quiet else "", f_mean_hz=round(m["f_mean_hz"], 1),
                   cents_from_expected=round(1200 * math.log2(m["f_mean_hz"] / (hz * S["pitch_ratio"])), 1),
                   f_low_hz=round(m["f_low_hz"], 1), f_high_hz=round(m["f_high_hz"], 1),
                   trill_hz=round(m["trill_hz"], 1), rms_dbfs=round(m["rms_dbfs"], 2), peak=round(m["peak"], 3))
        if hz not in clips and quiet:  # first trial of this pitch: the example clip
            a = max(0, m["onset"] - int(0.05 * RATE))
            clips[hz] = (e, x[a:a + int(1.1 * RATE)])
    rows.append(row)
wav.close()

fields = ["condition", "trial", "run", "teacher_voice", "target_human_hz", "note", "expected_bird_hz", "out_frame_guide_start",
          "found", "sound_before_note", "sound_before_dbfs", "onset_after_event_ms", "duration_s", "f_mean_hz", "cents_from_expected", "f_low_hz", "f_high_hz",
          "trill_hz", "rms_dbfs", "peak"]
with open(os.path.join(out_dir, "teacher_notes_all_trials.csv"), "w", newline="", encoding="utf-8") as fh:
    w = csv.DictWriter(fh, fieldnames=fields); w.writeheader(); w.writerows(rows)

# ---- 2. the three notes: example clip as heard, clean render, summary -----------------------------
summary = []
for i, hz in enumerate(sorted(clips), 1):
    e, clip = clips[hz]
    name = note_name(hz); safe = name.replace("#", "s")
    bird_hz, clean = render(hz)
    bird_name = note_name(bird_hz).replace("#", "s")
    heard_file = f"{i}_{safe}_as_heard_{e['condition']}_trial{e['trial']:03d}.wav"
    clean_file = f"{i}_{safe}_synth_clean_{bird_name}_{round(bird_hz)}Hz.wav"
    write_wav(os.path.join(out_dir, heard_file), clip)
    write_wav(os.path.join(out_dir, clean_file), [0.0] * int(0.05 * RATE) + clean)
    mc = measure([0.0] * 480 + clean, 480)
    ok = [r for r in rows if r["target_human_hz"] == hz and r.get("found")]
    n_all = sum(1 for r in rows if r["target_human_hz"] == hz)
    col = lambda k: [r[k] for r in ok if r[k] != ""]
    summary.append(dict(
        note=name, target_human_hz=hz, pitch_ratio=S["pitch_ratio"], bird_hz=round(bird_hz, 2), bird_note=note_name(bird_hz),
        guide_seconds=GUIDE_S, trill_rate_hz=round(TRILL_RATE, 1), trill_depth_semitones=S["trill_depth_st"],
        onset_glide_semitones=-S["glide_st"], onset_glide_s=S["glide_s"], attack_tau_s=S["attack_s"], release_tau_s=S["release_s"],
        harmonic2=S["harmonic2"], noise=S["noise"], level=round(LEVEL * S["volume"], 3),
        clean_f_mean_hz=round(mc["f_mean_hz"], 1), clean_duration_s=round(mc["duration_s"], 3), clean_rms_dbfs=round(mc["rms_dbfs"], 2),
        session_notes=n_all, session_notes_found=len(ok), session_notes_sound_before=sum(r["sound_before_note"] for r in ok),
        heard_f_mean_hz=round(statistics.mean(col("f_mean_hz")), 1), heard_f_sd_hz=round(statistics.pstdev(col("f_mean_hz")), 2),
        heard_f_low_hz=round(statistics.mean(col("f_low_hz")), 1), heard_f_high_hz=round(statistics.mean(col("f_high_hz")), 1),
        heard_trill_hz=round(statistics.median(col("trill_hz")), 1),
        heard_duration_s=round(statistics.mean(col("duration_s")), 3),
        heard_duration_min_s=min(col("duration_s")), heard_duration_max_s=max(col("duration_s")),
        heard_rms_dbfs=round(statistics.mean(col("rms_dbfs")), 2),
        heard_rms_min_dbfs=min(col("rms_dbfs")), heard_rms_max_dbfs=max(col("rms_dbfs")),
        heard_peak=round(statistics.mean(col("peak")), 3),
        onset_after_event_ms_median=statistics.median(col("onset_after_event_ms")),
        as_heard_wav=heard_file, synth_clean_wav=clean_file))

with open(os.path.join(out_dir, "teacher_notes.csv"), "w", newline="", encoding="utf-8") as fh:
    w = csv.DictWriter(fh, fieldnames=list(summary[0])); w.writeheader(); w.writerows(summary)

print("guide notes in session:", len(rows), "found:", sum(r["found"] for r in rows))
for s in summary:
    print(json.dumps(s, ensure_ascii=False))
odd = [r for r in rows if not r["found"] or abs(r["cents_from_expected"]) > 10
       or (r["duration_s"] != "" and abs(r["duration_s"] - 0.93) > 0.02)]
print("rows off the expected pitch / length:", len(odd))
for r in odd[:20]:
    print("  ", r)
print("sound before the note:", [(r["condition"], r["trial"], r["sound_before_dbfs"]) for r in rows if r.get("sound_before_note")])
import collections
print("by condition:", collections.Counter((r["condition"], r["note"]) for r in rows))
