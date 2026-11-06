# Lampion

Cross-platform (.NET 10, Avalonia 12) light-show designer: patch DMX fixtures, lay effects out on a timeline
synced to audio/video, preview in real-time OpenGL 3D, send DMX over Art-Net or a USB interface, export QLC+ workspaces.

**Priorities, in order:** user-friendly, developer-friendly, then fast. None of them is optional.

```sh
dotnet build Lampion.slnx                  # must finish with 0 warnings
dotnet test tests/Lampion.Core.Tests       # engine, exporter and allocation tests
dotnet run --project src/Lampion.App       # start screen
```

## Layout

| Project | What lives there |
| --- | --- |
| `src/Lampion.Core` | Model, show evaluator, DMX mapping, QLC+ export, Art-Net and USB DMX output, persistence. **No UI references, ever.** |
| `src/Lampion.App` | Avalonia shell, panels, custom controls, OpenGL stage renderer, LibVLC media |
| `tests/Lampion.Core.Tests` | xUnit tests for everything in Core |

Namespaces follow folders (`src/Lampion.Core/Engine/DmxMapper.cs` → `Lampion.Core.Engine`).

The evaluator is **pure**: same project + same time → same DMX. The 3D preview, DMX output and QLC+ export
all read that one result, which is why they agree. Never add a second path that computes light on its own.

## The UI is After Effects

We model the UI on Adobe After Effects, so AE users already know where everything is. Keep using its names and
idioms instead of inventing new ones:

| After Effects | Lampion |
| --- | --- |
| Project panel | fixture patch, groups, profiles, media bin |
| Composition viewer | Program monitor: Stage 3D / Video / DMX output tabs |
| Effect Controls | inspector for the selection, with twirl-down sections |
| Effects & Presets | effects library + user presets, dragged onto the timeline |
| Timeline layers + twirl-downs | tracks, clips and keyframe lanes |
| Stopwatch, blue scrubbable values | keyframing at the playhead, `ScrubValue` |

New features follow the same conventions: scrub to change a number, stopwatch to animate it, twirl-down to
reveal detail, drag from a library onto the timeline.

## Being user-friendly

- Every action is reachable from a menu, has a tooltip, and gets a shortcut if it's frequent. Shortcuts follow AE.
- Every edit is undoable. If it changes the project, it goes through `EditorState` so undo and dirty-tracking work.
- No silent failures: say what went wrong in plain language in the status bar or a dialog, and what to do about it.
- Always show units (m, s, °, %, DMX 0–255). Defaults should produce something that looks good straight away.
- Don't block the UI thread with I/O or long work.

## Being developer-friendly

### Components

- **One responsibility per type.** A panel composes smaller controls; it doesn't draw, hit-test, lay out and edit
  all in one class. Big controls hand work to collaborators: a layout calculator, renderers per element,
  a hit tester, one class per drag interaction.
- `partial` is **not** componentization. Splitting a 1000-line class across files changes nothing; extract types.
- A type nearing ~300 lines is a signal to split it. Windows and panels wire components together, they don't
  implement them.
- Reuse the shared building blocks (`Ui`, `ScrubValue`, `TwirlSection`, `PropertyRow`, ...) instead of
  hand-rolling another one.

### One type per file

- Every class, struct, record, interface, enum and delegate gets its own file, named after the type.
- Nested types that are private implementation details get their own file too, via `partial`:
  `TimelineControl.ClipDrag.cs`. If a nested type is used outside its owner, un-nest it.
- Generic types drop the arity: `Knob<T>` lives in `Knob.cs`.

### Encapsulation

- Smallest visibility that works. App types default to `internal sealed`; Core types are `public` only when the
  App or tests need them (tests see internals via `InternalsVisibleTo`).
- No public mutable fields. Use properties; prefer `init` or `private set`, and `readonly` fields.
- Collections are get-only properties (`List<T> Items { get; } = [];`). Expose `IReadOnlyList<T>` when callers
  don't need to mutate. JSON fills get-only collections because the serializer uses
  `JsonObjectCreationHandling.Populate`.
- Classes are `sealed` unless they are designed for inheritance.
- Don't leak internal state: return copies or read-only views, don't hand out a buffer you'll reuse.

### XML documentation

- Every non-private type and member has an XML doc comment. `src/Directory.Build.props` generates doc files
  and makes CS1591 (missing XML comment) a build error.
- Write what the code can't tell you: units, valid ranges, what `null` means, what's thrown, threading and
  ownership (e.g. "the returned span is only valid until the next call"). `/// <summary>Gets the name.</summary>`
  is noise. Use `<see cref>`, `<param>`, `<returns>`, `<exception>` where they add information.
- Inline comments follow the global rules: explain *why*, 2–3 lines max, keep them true, no ticket IDs.

### Persistence compatibility

`.lampion` projects, `.lampfx` presets and stage layouts are user files. Renaming a serialized property, enum
value or type discriminator breaks people's shows. If the format has to change, bump `Project.FormatVersion`
and migrate the old shape on load, with a test that loads the old one.

## Performance

### The hot path

Everything that runs per frame (~60 Hz) or per fixture per frame:

- `EditorState.Tick` → `ShowEvaluator` → `FixtureState` blending → `DmxMapper` → `ShowRenderer`/`DmxFrame`
- `StageView.UpdateScene` / scene snapshot building and `StageRenderer` draw passes
- `TimelineControl`, `DmxMonitor` and playhead rendering while playing
- DMX output (`DmxOutput.Tick` and its sender thread) and `MediaEngine.Sync`

Rules for that code:

- **Zero allocations per frame in steady state.** No LINQ, no lambdas that capture, no `params` arrays,
  no string formatting or interpolation (cache text and formatted labels), no boxing, no `foreach` over
  interfaces that box an enumerator.
- Reuse buffers owned by the component; use `Span<T>`, `stackalloc` for small fixed sizes, `ArrayPool<T>` for
  temporary large ones.
- Work out what only changes when the project changes (target lists, profile lookups, channel indices, meshes)
  once per edit, not per frame. Invalidate on `EditorState.Changed`, and resolve string IDs to indices before
  the inner loop.
- Hoist invariants out of loops, avoid repeated `Math.Pow`/trig where a cached value works, use `float` in GPU
  and render paths.
- Allocation-free behaviour of the Core hot path is covered by tests using
  `GC.GetAllocatedBytesForCurrentThread()`. If you touch the evaluator, keep them green; if you add a hot path,
  add one.

### Structs

Use a struct when the type is small (≲ 32 bytes), behaves as a value, and there are many of them or they're
created often (`Rgb`, `Vec3`, `EffectContext`, `EffectSample`, interaction/draw-time records).

- Make it `readonly struct` / `readonly record struct` unless mutation is the point (e.g. an accumulator
  reused in place, which is passed by `ref`).
- Pass larger ones by `in`; don't box them (no casting to `object`/interfaces in hot code).
- **Don't** make something a struct if it's an entity that's edited by reference (fixtures, clips, keyframes
  bound to the inspector) — copying would silently break edits.

Don't micro-optimize cold code (dialogs, export, file I/O) at the cost of readability. Readable first there.

## Workflow

- Behaviour changes come with Core tests where the logic lives in Core.
- Conventional Commits (`feat:`, `fix:`, `refactor:`, `perf:`, `docs:`, `chore:`, ...).
- Keep `README.md` in step with user-visible changes (features, shortcuts).
