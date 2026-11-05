# Lampion

A cross-platform (Windows / macOS / Linux) light show editor in C#. You patch DMX fixtures, build the stage in metres, lay out lights on a Premiere-style timeline next to your music and video, preview everything in real-time 3D, and export the show to QLC+.

## Build & run

```sh
dotnet run --project src/Lampion.App            # opens a demo show
dotnet run --project src/Lampion.App -- show.lampion
dotnet test                                     # core engine tests
```

Requires the .NET 10 SDK. Audio/video playback uses LibVLC: Windows and macOS get native binaries from NuGet, and on Linux you install `vlc` / `libvlc-dev` from the distro. The 3D view needs OpenGL 3.3, or GLES 3.0 through ANGLE on Windows.

## Layout

| Panel | What it does |
| --- | --- |
| **Project** (left) | Patch fixtures (auto-addressing, overlap warnings), groups, the fixture profile editor, media import, and the effects library (drag effects onto the timeline) |
| **Program** (centre) | **Stage 3D**: GPU preview and stage builder. **Video**: program monitor. **DMX output**: live 512-channel view per universe |
| **Effect Controls** (right) | Properties of whatever is selected: clip, effect parameters, transition, automation keyframes, track targets, fixture placement, set pieces |
| **Timeline** (bottom) | Light/audio/video tracks, beat grid from BPM + offset, markers, snapping, fades, transitions, keyframe lanes, waveforms |

## How lights become DMX

- A **fixture profile** lists its channels, and each channel has a role: `Dimmer, Red, Green, Blue, White, Amber, UV, Strobe, Pan(+Fine), Tilt(+Fine), Zoom, Gobo, Effect, Generic…` plus a default value.
- Effects and colour automation produce *light* (colour × intensity). Lampion maps that onto each fixture's own channels:
  - with a dimmer: full-saturation RGB, and the dimmer carries the level
  - with a white channel: the shared part of R/G/B moves to white (per-fixture toggle)
  - with no colour channels: the dimmer follows brightness and the preview uses the fixture's gel
- **Automation lanes** can target semantic values (Hue shift, Saturation, Intensity, Opacity, White, R/G/B) that are mapped per fixture, or write channels directly by **role** (e.g. Pan on every head) or by **channel number**.
- Tracks stack top-over-bottom with Normal / HTP / Add blending. Clips have fades and transitions (crossfade, dip to black, wipe across the fixtures).

The 3D preview is drawn from the computed DMX, decoded back to light, so what you see is what gets sent. **Output → Send Art-Net** broadcasts it live.

## Stage builder

Everything is in metres: X across (audience right is +), Y up, Z toward the audience.

- Set pieces: truss (real lattice geometry), platforms, walls, LED screens (these play the video track)
- Drag on the floor plane, Alt+drag for height, or drag a gizmo arrow to lock an axis. Snaps to 0.05–1 m
- Aim tool: click the stage to point the selected fixtures there. The readout shows throw distance and spot diameter
- Top / Front / Side views are orthographic, for exact placement
- Inspector helpers: aim at an XYZ point, distribute fixtures evenly along a truss
- **Stage → Export / Import stage layout** saves the venue as a `.lampstage` file: set pieces and stage look, plus the rig (fixtures with patch and positions, groups, the profiles they use). On import you choose set only or set + rig, and Ctrl+Z undoes it. **Use this layout for new projects** makes File → New start from it, with one light track per group ready to go.

Rendering covers per-pixel spotlights with hot-spots on the set, ray-marched volumetric beams through animated haze, lens flares, ACES tone mapping and 4× MSAA. Optional *Bounce* lets the lit rig tint the room's ambient light.

## QLC+ export

**File → Export to QLC+** writes a QLC+ 4 workspace (`.qxw`). It can build a new one or merge into an existing project:

- Each fixture is patched at its address: RGB/RGBW profiles become QLC+ RGB panels, everything else Generic.
- Each group (or loose fixture) gets a Sequence bound to a hidden Scene. The steps hold the exact DMX Lampion renders, sampled and reduced to linear fades within a DMX tolerance.
- A single Show plays the sequences next to Audio/Video functions at their timeline positions, and fixtures are placed in the fixture monitor.
- When merging, fixtures at the same address are reused, partial overlaps are skipped with a warning, and IDs and show names never collide.
