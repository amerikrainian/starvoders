"""sfxr's synthesizer, ported from DrPetter's sfxr (main.cpp, 2007), standard library only.

A sound is the 22 sfxr parameters (the names of its .sfs fields, without the p_ prefix) plus the
wave type; render() returns mono floats in -1..1 at 44100 Hz, exactly as sfxr's SynthSample computes
them (8x supersampling, the same filters, envelope, phaser and repeat). Parameters left out take
sfxr's ResetParams defaults.

    uv run python tools/python/sfxr.py params.json out.wav [--seed N]

params.json holds one sound: {"wave_type": "noise", "base_freq": 0.3, ...}.
"""

from __future__ import annotations

import argparse
import array
import json
import math
import random
import wave
from pathlib import Path

RATE = 44100
WAVES = {"square": 0, "sawtooth": 1, "sine": 2, "noise": 3}

DEFAULTS = {
    "wave_type": "square",
    "base_freq": 0.3,
    "freq_limit": 0.0,
    "freq_ramp": 0.0,
    "freq_dramp": 0.0,
    "duty": 0.0,
    "duty_ramp": 0.0,
    "vib_strength": 0.0,
    "vib_speed": 0.0,
    "vib_delay": 0.0,  # in sfxr's struct, never used by its synth
    "env_attack": 0.0,
    "env_sustain": 0.3,
    "env_decay": 0.4,
    "env_punch": 0.0,
    "filter_on": False,  # likewise unused
    "lpf_resonance": 0.0,
    "lpf_freq": 1.0,
    "lpf_ramp": 0.0,
    "hpf_freq": 0.0,
    "hpf_ramp": 0.0,
    "pha_offset": 0.0,
    "pha_ramp": 0.0,
    "repeat_speed": 0.0,
    "arp_speed": 0.0,
    "arp_mod": 0.0,
    # sfxr's master_vol and sound_vol: the output is scaled by 2 * sound_vol * master_vol
    "master_vol": 0.05,
    "sound_vol": 0.5,
}


class _Synth:
    def __init__(self, params: dict, rng: random.Random):
        unknown = set(params) - set(DEFAULTS)
        if unknown:
            raise ValueError(f"unknown sfxr parameters: {sorted(unknown)}")
        self.p = {**DEFAULTS, **params}
        wt = self.p["wave_type"]
        self.wave = WAVES[wt] if isinstance(wt, str) else int(wt)
        self.rng = rng
        self.phase = 0
        self.reset(False)

    def frnd(self, r: float) -> float:
        return self.rng.random() * r

    def reset(self, restart: bool) -> None:
        p = self.p
        if not restart:
            self.phase = 0
        self.fperiod = 100.0 / (p["base_freq"] ** 2 + 0.001)
        self.period = int(self.fperiod)
        self.fmaxperiod = 100.0 / (p["freq_limit"] ** 2 + 0.001)
        self.fslide = 1.0 - p["freq_ramp"] ** 3 * 0.01
        self.fdslide = -(p["freq_dramp"] ** 3) * 0.000001
        self.square_duty = 0.5 - p["duty"] * 0.5
        self.square_slide = -p["duty_ramp"] * 0.00005
        if p["arp_mod"] >= 0.0:
            self.arp_mod = 1.0 - p["arp_mod"] ** 2 * 0.9
        else:
            self.arp_mod = 1.0 + p["arp_mod"] ** 2 * 10.0
        self.arp_time = 0
        self.arp_limit = int((1.0 - p["arp_speed"]) ** 2 * 20000 + 32)
        if p["arp_speed"] == 1.0:
            self.arp_limit = 0
        if restart:
            return
        self.fltp = 0.0
        self.fltdp = 0.0
        self.fltw = p["lpf_freq"] ** 3 * 0.1
        self.fltw_d = 1.0 + p["lpf_ramp"] * 0.0001
        self.fltdmp = min(0.8, 5.0 / (1.0 + p["lpf_resonance"] ** 2 * 20.0) * (0.01 + self.fltw))
        self.fltphp = 0.0
        self.flthp = p["hpf_freq"] ** 2 * 0.1
        self.flthp_d = 1.0 + p["hpf_ramp"] * 0.0003
        self.vib_phase = 0.0
        self.vib_speed = p["vib_speed"] ** 2 * 0.01
        self.vib_amp = p["vib_strength"] * 0.5
        self.env_vol = 0.0
        self.env_stage = 0
        self.env_time = 0
        self.env_length = [
            int(p["env_attack"] ** 2 * 100000.0),
            int(p["env_sustain"] ** 2 * 100000.0),
            int(p["env_decay"] ** 2 * 100000.0),
        ]
        self.fphase = p["pha_offset"] ** 2 * 1020.0
        if p["pha_offset"] < 0.0:
            self.fphase = -self.fphase
        self.fdphase = p["pha_ramp"] ** 2 * 1.0
        if p["pha_ramp"] < 0.0:
            self.fdphase = -self.fdphase
        self.iphase = abs(int(self.fphase))
        self.ipp = 0
        self.phaser = [0.0] * 1024
        self.noise = [self.frnd(2.0) - 1.0 for _ in range(32)]
        self.rep_time = 0
        self.rep_limit = int((1.0 - p["repeat_speed"]) ** 2 * 20000 + 32)
        if p["repeat_speed"] == 0.0:
            self.rep_limit = 0

    def render(self) -> list[float]:
        p = self.p
        out: list[float] = []
        gain = 2.0 * p["sound_vol"] * p["master_vol"]
        lpf_on = p["lpf_freq"] != 1.0
        while True:
            self.rep_time += 1
            if self.rep_limit != 0 and self.rep_time >= self.rep_limit:
                self.rep_time = 0
                self.reset(True)
            self.arp_time += 1
            if self.arp_limit != 0 and self.arp_time >= self.arp_limit:
                self.arp_limit = 0
                self.fperiod *= self.arp_mod
            self.fslide += self.fdslide
            self.fperiod *= self.fslide
            if self.fperiod > self.fmaxperiod:
                self.fperiod = self.fmaxperiod
                if p["freq_limit"] > 0.0:
                    break
            rfperiod = self.fperiod
            if self.vib_amp > 0.0:
                self.vib_phase += self.vib_speed
                rfperiod = self.fperiod * (1.0 + math.sin(self.vib_phase) * self.vib_amp)
            self.period = max(8, int(rfperiod))
            self.square_duty = min(0.5, max(0.0, self.square_duty + self.square_slide))

            self.env_time += 1
            if self.env_time > self.env_length[self.env_stage]:
                self.env_time = 0
                self.env_stage += 1
                if self.env_stage == 3:
                    break
            length = self.env_length[self.env_stage]
            if self.env_stage == 0:
                self.env_vol = self.env_time / length
            elif self.env_stage == 1:
                self.env_vol = 1.0 + (1.0 - self.env_time / length) * 2.0 * p["env_punch"]
            else:
                self.env_vol = 1.0 - self.env_time / length

            self.fphase += self.fdphase
            self.iphase = min(1023, abs(int(self.fphase)))
            if self.flthp_d != 0.0:
                self.flthp = min(0.1, max(0.00001, self.flthp * self.flthp_d))

            ssample = 0.0
            for _ in range(8):
                self.phase += 1
                if self.phase >= self.period:
                    self.phase %= self.period
                    if self.wave == 3:
                        self.noise = [self.frnd(2.0) - 1.0 for _ in range(32)]
                fp = self.phase / self.period
                if self.wave == 0:
                    sample = 0.5 if fp < self.square_duty else -0.5
                elif self.wave == 1:
                    sample = 1.0 - fp * 2
                elif self.wave == 2:
                    sample = math.sin(fp * 2 * math.pi)
                else:
                    sample = self.noise[self.phase * 32 // self.period]
                pp = self.fltp
                self.fltw = min(0.1, max(0.0, self.fltw * self.fltw_d))
                if lpf_on:
                    self.fltdp += (sample - self.fltp) * self.fltw
                    self.fltdp -= self.fltdp * self.fltdmp
                else:
                    self.fltp = sample
                    self.fltdp = 0.0
                self.fltp += self.fltdp
                self.fltphp += self.fltp - pp
                self.fltphp -= self.fltphp * self.flthp
                sample = self.fltphp
                self.phaser[self.ipp & 1023] = sample
                sample += self.phaser[(self.ipp - self.iphase + 1024) & 1023]
                self.ipp = (self.ipp + 1) & 1023
                ssample += sample * self.env_vol
            ssample = ssample / 8 * gain
            out.append(min(1.0, max(-1.0, ssample)))
        return out


def render(params: dict, seed: int | None = None) -> list[float]:
    """The sound as sfxr plays it: mono floats, -1..1, at RATE."""
    return _Synth(params, random.Random(seed)).render()


def write_wav(path: Path, samples: list[float], peak: float | None = None) -> None:
    """16-bit mono WAV. With peak, the samples are scaled so the loudest is that (0..1)."""
    if peak is not None:
        top = max((abs(s) for s in samples), default=0.0)
        if top > 0:
            samples = [s * peak / top for s in samples]
    data = array.array("h", (int(round(max(-1.0, min(1.0, s)) * 32767)) for s in samples))
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("params", type=Path)
    ap.add_argument("out", type=Path)
    ap.add_argument("--seed", type=int, default=None)
    ap.add_argument("--peak", type=float, default=None, help="normalize to this peak (0..1)")
    args = ap.parse_args()
    samples = render(json.loads(args.params.read_text(encoding="utf-8")), args.seed)
    write_wav(args.out, samples, args.peak)
    print(f"{args.out}: {len(samples) / RATE * 1000:.0f} ms")


if __name__ == "__main__":
    main()
