"""Writes the placeholder sound effects to Assets/Resources/Audio/Sfx (needs numpy).

Synthesised and seeded, so a rerun gives identical files. These hold each AudioCue's place until
real recordings replace them: keep the file name and the cue picks up the new sound. The target
sound is paper, stamps, card and coin (production/next-phase-plan.md, Track A).
"""
import pathlib
import struct
import sys
import wave

import numpy as np

RATE = 44100
PEAK = 10 ** (-6 / 20)  # -6 dBFS: headroom for voices stacking
OUT = pathlib.Path(__file__).resolve().parents[2] / "Assets/Resources/Audio/Sfx"
rng = np.random.default_rng(1701)


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def noise(length):
    return rng.standard_normal(int(length * RATE))


def low_pass(signal, cutoff):
    alpha = 1 - np.exp(-2 * np.pi * cutoff / RATE)
    out = np.empty_like(signal)
    state = 0.0
    for i, sample in enumerate(signal):
        state += alpha * (sample - state)
        out[i] = state
    return out


def high_pass(signal, cutoff):
    return signal - low_pass(signal, cutoff)


def burst(length, decay, low=None, high=None):
    """Filtered noise with an exponential tail: paper, card and impact bodies."""
    signal = noise(length)
    if high:
        signal = high_pass(signal, high)
    if low:
        signal = low_pass(signal, low)
    return signal / (np.abs(signal).max() + 1e-9) * np.exp(-seconds(length) / decay)


def thump(length, start, end, decay):
    """A falling sine: the weight under a stamp or a hit."""
    t = seconds(length)
    freq = end + (start - end) * np.exp(-t / .03)
    return np.sin(2 * np.pi * np.cumsum(freq) / RATE) * np.exp(-t / decay)


def tone(length, partials, decay):
    """Decaying sine partials as (frequency, level): coins and chimes."""
    t = seconds(length)
    out = sum(level * np.sin(2 * np.pi * freq * t) for freq, level in partials)
    return out * np.exp(-t / decay)


def mix(*parts):
    """Adds (delay seconds, signal, level) parts."""
    length = max(int(delay * RATE) + len(signal) for delay, signal, _ in parts)
    out = np.zeros(length)
    for delay, signal, level in parts:
        start = int(delay * RATE)
        out[start:start + len(signal)] += signal * level
    return out


def coin(freq):
    return tone(.3, [(freq, 1), (freq * 1.51, .6), (freq * 2.27, .4), (freq * 3.13, .2)], .07)


SOUNDS = {
    "ui_press": lambda: mix((0, burst(.05, .008, high=1800), 1), (0, thump(.05, 420, 240, .012), .5)),
    "card_throw": lambda: burst(.14, .05, low=5000, high=900),
    "block_impact": lambda: mix((0, burst(.16, .03, low=2600), .9), (0, thump(.16, 210, 110, .05), .9)),
    "enemy_hit": lambda: mix((0, burst(.2, .04, low=3200, high=120), 1), (0, thump(.2, 170, 70, .07), 1)),
    "enemy_defeat": lambda: mix((0, thump(.5, 150, 42, .16), 1), (0, burst(.5, .12, low=1400), .7),
                                (.08, burst(.3, .08, high=900), .35)),
    "perfect_block": lambda: mix((0, thump(.2, 260, 150, .05), .7), (0, burst(.1, .02, high=1200), .5),
                                 (.03, tone(.5, [(880, 1), (1320, .6), (1760, .3)], .14), .55)),
    "player_hurt": lambda: mix((0, thump(.32, 130, 48, .1), 1), (0, burst(.3, .06, low=900), .9)),
    "shield_hit": lambda: mix((0, burst(.18, .03, high=1400), .7), (0, tone(.3, [(520, 1), (780, .7), (1170, .4)], .06), .7)),
    "trigger_hop": lambda: mix((0, tone(.12, [(660, 1), (1320, .3)], .03), 1), (0, burst(.02, .005, high=2500), .3)),
    "coin_land": lambda: coin(2300),
    "heal_land": lambda: tone(.26, [(990, 1), (1485, .5)], .07),
    "item_pick_up": lambda: burst(.09, .03, low=6000, high=1500),
    "item_slap": lambda: mix((0, burst(.12, .018, low=3800), 1), (0, thump(.12, 230, 130, .03), .7)),
    "reward_card": lambda: mix((0, burst(.28, .1, low=6000, high=1200), .55), (.03, tone(.6, [(660, 1), (990, .6)], .16), .4),
                               (.13, tone(.6, [(880, 1), (1320, .6)], .18), .45)),
    "run_victory": lambda: mix(*[(i * .13, tone(.9, [(f, 1), (f * 1.5, .4), (f * 2, .25)], .24), .5)
                                 for i, f in enumerate((523.3, 659.3, 784, 1046.5))]),
    "run_defeat": lambda: mix(*[(i * .24, tone(1.1, [(f, 1), (f * 2, .2)], .34), .55)
                                for i, f in enumerate((293.7, 261.6, 220))]),
}


def finish(signal):
    """Removes DC, normalises to PEAK and fades out so no clip ends on a click."""
    signal = signal - signal.mean()
    signal = signal / np.abs(signal).max() * PEAK
    fade = min(len(signal), int(.01 * RATE))
    signal[-fade:] *= np.linspace(1, 0, fade)
    attack = int(.0005 * RATE)
    signal[:attack] *= np.linspace(0, 1, attack)
    return signal


def write(name, signal):
    samples = np.round(signal * 32767).astype(np.int16)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(struct.pack(f"<{len(samples)}h", *samples.tolist()))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, make in SOUNDS.items():
        write(name, finish(make()))
    print(f"wrote {len(SOUNDS)} clips to {OUT}")


if __name__ == "__main__":
    sys.exit(main())
