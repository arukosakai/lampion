# Show day checklist

Lampion's live mode is built to keep the lights going whatever happens inside the app (see the README's
*Live console* section). These are the things outside it that can still end a show, and what to do about them.

## The day before

- **Rehearse the whole set in live mode** on the show laptop, with the real interface and controller plugged in.
  Watch the engine line at the bottom of the live window: it should stay green with 0 overruns.
- **Tune Light delay** on the real PA: raise it until hits land with the kick through the speakers.
- **Pause Windows Update** (Settings → Windows Update → Pause) so the laptop can't restart itself mid-show.
- **Turn off sleep and the screen saver** in Windows. Live mode already asks Windows to stay awake, but the
  power plan wins on some laptops.
- **Copy every show and its music into one folder** next to the `.lampion` project, so nothing points at a
  USB stick that may not be there.

## At the venue

- **Plug the laptop into mains power.** A laptop on battery throttles its CPU.
- **Use a wired network for Art-Net**, not Wi-Fi.
- **Prefer interfaces that hold the last frame** if the computer stops sending: Art-Net nodes and the Enttec
  DMX USB Pro do; Open DMX / FTDI cables go dark, because the computer itself drives the DMX line.
- **Close other programs**: browsers, chat apps and cloud sync all compete for the CPU and the disk.
- **Keep a manual fallback**: a hardware console or a "house lights" scene on the DMX node, in case the
  laptop itself dies.

## If something goes wrong

- Problems show up in red at the bottom of the live window, in plain words. Faults the engine contained
  (a broken pad, a show that failed to evaluate) don't stop the lights: the pad switches off, the show holds
  its last look.
- An unplugged MIDI controller reconnects by itself when you plug it back in.
- After the show, anything that went wrong is logged in `crash.log` in Lampion's user data folder.
