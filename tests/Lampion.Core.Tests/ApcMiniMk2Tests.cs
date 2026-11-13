using Lampion.Core.Live;
using Lampion.Core.Midi;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Tests;

public class ApcMiniMk2Tests
{
    [Theory]
    [InlineData("APC mini mk2", true)]
    [InlineData("2- APC mini mk2", true)]
    [InlineData("MIDIIN2 (APC mini mk2)", false)]
    [InlineData("MIDIOUT2 (APC mini mk2)", false)]
    [InlineData("APC mini", false)]
    [InlineData(null, false)]
    public void MatchesOnlyTheFirstPort(string? name, bool expected) => Assert.Equal(expected, ApcMiniMk2.Matches(name));

    [Fact]
    public void FindSkipsOtherDevices() =>
        Assert.Equal(1, ApcMiniMk2.Find(["Microsoft GS Wavetable Synth", "APC mini mk2", "MIDIOUT2 (APC mini mk2)"]));

    [Fact]
    public void GridCountsUpFromTheBottomLeft()
    {
        Assert.Equal(0x38, ApcMiniMk2.PadNote(0, 0));
        Assert.Equal(0x3F, ApcMiniMk2.PadNote(0, 7));
        Assert.Equal(0x00, ApcMiniMk2.PadNote(7, 0));
        Assert.Equal(0x07, ApcMiniMk2.PadNote(7, 7));
    }

    [Theory]
    [InlineData("#FF0000", 5)]
    [InlineData("#590000", 5)]
    [InlineData("#00FF00", 21)]
    [InlineData("#0000FF", 45)]
    [InlineData("#FFFFFF", 3)]
    [InlineData("#FF7F00", 96)]
    [InlineData("#000000", 3)]
    public void NearestColourComparesAtFullBrightness(string hex, int expected) => Assert.Equal(expected, ApcMiniMk2.NearestColour(hex));

    [Fact]
    public void PadBrightnessIsTheNoteOnChannel()
    {
        Assert.Equal(new MidiMessage(0x96, 0x00, 0x05), ApcMiniMk2.PadLight(0, PadGlow.Full, 5));
        Assert.Equal(new MidiMessage(0x91, 0x00, 0x05), ApcMiniMk2.PadLight(0, PadGlow.Dim, 5));
        Assert.Equal(new MidiMessage(0x9E, 0x00, 0x05), ApcMiniMk2.PadLight(0, PadGlow.Blink, 5));
    }

    [Fact]
    public void ButtonsTakeOffOnOrBlinkOnChannelOne()
    {
        Assert.Equal(new MidiMessage(0x90, 0x64, 1), ApcMiniMk2.ButtonLight(0x64, on: true));
        Assert.Equal(new MidiMessage(0x90, 0x70, 2), ApcMiniMk2.ButtonLight(0x70, on: true, blink: true));
        Assert.Equal(new MidiMessage(0x90, 0x77, 0), ApcMiniMk2.ButtonLight(0x77, on: false));
    }

    [Fact]
    public void LayoutBindsFadersGridShowsGoAndStop()
    {
        var live = new LiveSet();
        for (var i = 0; i < 10; i++)
            live.Faders.Add(new LiveFader());
        for (var i = 0; i < 70; i++)
            live.Pads.Add(new LivePad());
        for (var i = 0; i < 9; i++)
            live.Shows.Add(new LiveShow());
        var spare = new MidiBinding(MidiBindingKind.Note, 2, 60);
        live.Pads[64].Midi = spare;

        ApcLayout.Apply(live);

        Assert.Equal(new MidiBinding(MidiBindingKind.Control, 1, 0x30), live.Faders[0].Midi);
        Assert.Equal(new MidiBinding(MidiBindingKind.Control, 1, 0x38), live.Faders[8].Midi);
        Assert.Null(live.Faders[9].Midi);
        Assert.Equal(new MidiBinding(MidiBindingKind.Note, 1, 0x38), live.Pads[0].Midi);
        Assert.Equal(new MidiBinding(MidiBindingKind.Note, 1, 0x30), live.Pads[8].Midi);
        Assert.Equal(new MidiBinding(MidiBindingKind.Note, 1, 0x07), live.Pads[63].Midi);
        Assert.Same(spare, live.Pads[64].Midi);
        Assert.Equal(new MidiBinding(MidiBindingKind.Note, 1, 0x70), live.Shows[0].Midi);
        Assert.Equal(new MidiBinding(MidiBindingKind.Note, 1, 0x76), live.Shows[6].Midi);
        Assert.Null(live.Shows[7].Midi);
        Assert.Equal(new MidiBinding(MidiBindingKind.Note, 1, 0x77), live.StopMidi);
        Assert.Equal(new MidiBinding(MidiBindingKind.Note, 1, 0x6B), live.GoMidi);
    }
}
