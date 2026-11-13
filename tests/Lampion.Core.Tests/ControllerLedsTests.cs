using Lampion.Core.Live;
using Lampion.Core.Midi;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Tests;

public class ControllerLedsTests
{
    private static LivePad Pad(int note, int velocity = 21) =>
        new() { Effect = new SolidEffect { Color = "#FF0000" }, Midi = new MidiBinding(MidiBindingKind.Note, 1, note), LedVelocity = velocity };

    [Fact]
    public void GenericControllerGetsTheVelocityWhileOnAndZeroWhenOff()
    {
        var live = new LiveSet { Pads = { Pad(36, velocity: 45) } };
        var controls = new LiveControls();
        var output = new RecordingMidiOutput("Launchpad");
        var leds = new ControllerLeds();

        leds.Update(output, live, controls, null, null, null);
        Assert.Equal([MidiMessage.NoteOn(1, 36, 0)], output.Sent);

        controls.SetPad(0, true);
        output.Sent.Clear();
        leds.Update(output, live, controls, null, null, null);
        Assert.Equal([MidiMessage.NoteOn(1, 36, 45)], output.Sent);
    }

    [Fact]
    public void OnlyChangesAreSentUntilForgotten()
    {
        var live = new LiveSet { Pads = { Pad(36) } };
        var controls = new LiveControls();
        var output = new RecordingMidiOutput("Launchpad");
        var leds = new ControllerLeds();
        leds.Update(output, live, controls, null, null, null);
        output.Sent.Clear();

        leds.Update(output, live, controls, null, null, null);
        Assert.Empty(output.Sent);

        leds.Forget();
        leds.Update(output, live, controls, null, null, null);
        Assert.Single(output.Sent);
    }

    [Fact]
    public void ApcPadsShowTheirEffectColourDimWhileOff()
    {
        var live = new LiveSet { Pads = { Pad(0x38) } };
        var controls = new LiveControls();
        var output = new RecordingMidiOutput("APC mini mk2");
        var leds = new ControllerLeds();

        leds.Update(output, live, controls, null, null, null);
        Assert.Contains(ApcMiniMk2.PadLight(0x38, PadGlow.Dim, 5), output.Sent);
        // The rest of the surface is cleared once.
        Assert.Contains(ApcMiniMk2.Off(0x00), output.Sent);
        Assert.Contains(ApcMiniMk2.Off(0x77), output.Sent);

        controls.SetPad(0, true);
        output.Sent.Clear();
        leds.Update(output, live, controls, null, null, null);
        Assert.Equal([ApcMiniMk2.PadLight(0x38, PadGlow.Full, 5)], output.Sent);
    }

    [Fact]
    public void ShowButtonsBlinkWhileLoadingAndLightWhilePlaying()
    {
        var show = new LiveShow { Midi = new MidiBinding(MidiBindingKind.Note, 1, 0x70) };
        var live = new LiveSet { Shows = { show }, StopMidi = new MidiBinding(MidiBindingKind.Note, 1, 0x77) };
        var output = new RecordingMidiOutput("APC mini mk2");
        var leds = new ControllerLeds();

        leds.Update(output, live, new LiveControls(), null, show, null);
        Assert.Contains(ApcMiniMk2.ButtonLight(0x70, on: true, blink: true), output.Sent);
        Assert.Contains(ApcMiniMk2.ButtonLight(0x77, on: false), output.Sent);

        output.Sent.Clear();
        leds.Update(output, live, new LiveControls(), show, null, null);
        Assert.Equal([ApcMiniMk2.ButtonLight(0x70, on: true), ApcMiniMk2.ButtonLight(0x77, on: true)], output.Sent);
    }

    [Fact]
    public void ALightWhoseBindingWentAwayIsSwitchedOff()
    {
        var pad = Pad(40);
        var live = new LiveSet { Pads = { pad } };
        var controls = new LiveControls();
        controls.SetPad(0, true);
        var output = new RecordingMidiOutput("Launchpad");
        var leds = new ControllerLeds();
        leds.Update(output, live, controls, null, null, null);

        pad.Midi = null;
        output.Sent.Clear();
        leds.Update(output, live, controls, null, null, null);
        Assert.Equal([MidiMessage.NoteOn(1, 40, 0)], output.Sent);
    }

    [Fact]
    public void BlankSwitchesOffEverythingLit()
    {
        var live = new LiveSet { Pads = { Pad(36), Pad(37) } };
        var controls = new LiveControls();
        controls.SetPad(1, true);
        var output = new RecordingMidiOutput("Launchpad");
        var leds = new ControllerLeds();
        leds.Update(output, live, controls, null, null, null);
        output.Sent.Clear();

        leds.Blank(output);
        Assert.Equal([MidiMessage.NoteOn(1, 36, 0), MidiMessage.NoteOn(1, 37, 0)], output.Sent);
    }
}
