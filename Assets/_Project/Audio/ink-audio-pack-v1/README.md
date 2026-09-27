# Ink audio pack — first synthesized pass

39 requested assets: 36 sound effects and 3 music files. These are original procedural sound designs and compositions, not acoustic foley recordings. Paper, ink and metal descriptions indicate the intended timbre; audition in-game before final approval.

## Format

- 48,000 Hz, signed 24-bit PCM WAV.
- SFX are mono for engine positioning. Music is stereo.
- Exact durations, frame counts, peaks and music loop boundaries are in manifest.json.
- No external recordings or sample libraries were used.

## Priority sounds

parry.wav combines a fast paper snap, supporting body transient and bright inharmonic metallic modes with a short shimmer. It peaks at -2.5 dBFS. Lower its engine gain if repeated successful parries dominate the mix.

boss_swipe_tell is a bright shing, boss_cap_tell is a hollow pop, and boss_sweep_tell is a sustained friction squeak. boss_dash_tell is a rough rising rev; boss_drip_tell is an irregular dark three-drop pattern. Those two deliberately avoid the bright metal onset used by the parry family. The dash and sweep both reference marker friction as specified, so test their distinction under boss music with real players.

## Music and synchronization

music_world1: 80 BPM, 4/4, 40 bars, 120 seconds, 5,760,000 stereo frames.

music_boss and music_boss_layer: 128 BPM, 4/4, 48 bars, exactly 90 seconds and 4,320,000 stereo frames EACH. Both start at bar 1, beat 1, use the same harmonic schedule, and use identical loop boundaries. The layer is an additive stem, not a second full boss track.

Start both boss files on the same audio/DSP clock and keep the layer playing at zero gain during phase one. Fade its gain up for phase two. Starting it from the beginning only when phase two starts will put it out of musical phase. A one-bar fade is 1.875 seconds; two bars is 3.75 seconds. At unity gain, the combined boss mix has at least 3 dB peak headroom. Avoid independently normalizing or time-stretching either stem.

Releases and room-delay tails wrap into the loop start. Loop the complete sample range; do not append silence or crossfade the two stems independently. Exact import and scheduling settings depend on the game's audio system.

## Audition

Open index.html for all assets. previews/sfx_audition.wav contains all effects in order with short gaps; sfx_cues.json identifies their start times. world1_preview.wav is a 20-second excerpt. boss_phase2_preview.wav fades the layer over a 20-second excerpt and is not a loop asset.

This is a first sound-design pass. No claim of recorded-paper realism or gameplay-tested readability is made. Evaluate the parry, warning contrast and repetition fatigue in the actual mix.
