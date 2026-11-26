"""Renders audio/sounds.json: each sound into audio/cues (the files the mod ships and plays, one per
cue, named as the mod's AudioCue in snake case), then demo columns into audio/samples.

A column is read bottom to top, one sound per cell (E = empty, O = occupied), each cell's slot a fixed
stagger after the one below it, the stagger shorter than the sounds so they overlap. Each sound starts
its attack time before its slot, so it is at full volume on the slot: a sound that swells in, started on
the slot, is heard late, and the column stumbles. No pitch yet: every cell's sound is the same, except
that each empty puff is fresh noise (one rendering repeated at a short stagger would comb-filter into a
flanging whoosh).

    uv run python tools/python/audio_samples.py
"""

from __future__ import annotations

import json
import math
import random
from pathlib import Path

import sfxr

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / "audio"
OUT = AUDIO / "samples"
CUES = AUDIO / "cues"

PATTERNS = ["OEEOEOE"]  # bottom to top
RANDOM_PATTERNS = 10  # more, 5 to 9 rows, from a fixed seed so a re-render keeps them
RANDOM_SEED = 7
STAGGER_MS = 80


def patterns() -> list[str]:
    rng = random.Random(RANDOM_SEED)
    out = list(PATTERNS)
    while len(out) < len(PATTERNS) + RANDOM_PATTERNS:
        p = "".join(rng.choice("OE") for _ in range(rng.randint(5, 9)))
        if p not in out:
            out.append(p)
    return out


def rms(samples: list[float]) -> float:
    return math.sqrt(sum(s * s for s in samples) / len(samples))


def scaled(samples: list[float], level: float) -> list[float]:
    """Scaled to RMS level: by peak, fresh noise would vary in loudness from puff to puff."""
    k = level / rms(samples)
    return [s * k for s in samples]


def sound(defs: dict, name: str, seed: int) -> list[float]:
    d = defs[name]
    return scaled(sfxr.render(d["sfxr"], seed), d["level"])


def attack_samples(defs: dict, name: str) -> int:
    """sfxr's attack stage length: env_attack^2 * 100000 samples."""
    return int(defs[name]["sfxr"].get("env_attack", 0.0) ** 2 * 100000)


def column(defs: dict, pattern: str, stagger_ms: int) -> list[float]:
    step = round(sfxr.RATE * stagger_ms / 1000)
    names = ["occupied" if c == "O" else "empty" for c in pattern]
    parts = [sound(defs, n, seed=100 + i) for i, n in enumerate(names)]
    leads = [attack_samples(defs, n) for n in names]
    pad = max(leads)
    starts = [pad + i * step - lead for i, lead in enumerate(leads)]
    mix = [0.0] * max(start + len(p) for start, p in zip(starts, parts))
    for start, part in zip(starts, parts):
        for j, s in enumerate(part):
            mix[start + j] += s
    top = max(abs(s) for s in mix)
    return [s / top * 0.9 for s in mix] if top > 0.9 else mix


def main() -> None:
    defs = {k: v for k, v in json.loads((AUDIO / "sounds.json").read_text(encoding="utf-8")).items() if not k.startswith("_")}
    OUT.mkdir(parents=True, exist_ok=True)
    CUES.mkdir(parents=True, exist_ok=True)
    for old in list(OUT.glob("*.wav")) + list(CUES.glob("*.wav")):
        old.unlink()
    for name in defs:
        s = sound(defs, name, seed=1)
        sfxr.write_wav(CUES / f"{name}.wav", s)
        print(f"{name}.wav: {len(s) / sfxr.RATE * 1000:.0f} ms, attack {attack_samples(defs, name) / 44.1:.0f} ms")
    for pattern in patterns():
        s = column(defs, pattern, STAGGER_MS)
        name = f"column_{STAGGER_MS}ms_{pattern}.wav"
        sfxr.write_wav(OUT / name, s)
        print(f"{name}: {len(pattern)} rows, {len(s) / sfxr.RATE * 1000:.0f} ms")


if __name__ == "__main__":
    main()
