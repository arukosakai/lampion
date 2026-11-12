using Lampion.Core.Live;
using Lampion.Core.Midi;
using Lampion.Core.Model;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Tests;

public class MidiRouterTests
{
    private static MidiRouter Router(Project project)
    {
        var router = new MidiRouter(new LiveControls());
        router.Patch = LivePatch.Build(project);
        return router;
    }

    [Fact]
    public void ControlChangeMovesItsFader()
    {
        var project = new Project();
        project.Live.Faders.Add(new LiveFader { Midi = new MidiBinding(MidiBindingKind.Control, 2, 7) });
        var router = Router(project);

        router.Handle(MidiMessage.ControlChange(2, 7, 127));
        Assert.Equal(1, router.Controls.Fader(0));

        router.Handle(MidiMessage.ControlChange(1, 7, 0));
        Assert.Equal(1, router.Controls.Fader(0));
    }

    [Fact]
    public void NoteTogglesAToggledPadAndIgnoresNoteOff()
    {
        var project = new Project();
        project.Live.Pads.Add(new LivePad { Mode = PadMode.Toggle, Midi = new MidiBinding(MidiBindingKind.Note, 1, 36) });
        var router = Router(project);

        router.Handle(MidiMessage.NoteOn(1, 36, 100));
        router.Handle(MidiMessage.NoteOn(1, 36, 0));
        Assert.True(router.Controls.IsPadOn(0));

        router.Handle(MidiMessage.NoteOn(1, 36, 100));
        Assert.False(router.Controls.IsPadOn(0));
    }

    [Fact]
    public void FlashPadFollowsNoteOnAndOff()
    {
        var project = new Project();
        project.Live.Pads.Add(new LivePad { Mode = PadMode.Flash, Midi = new MidiBinding(MidiBindingKind.Note, 1, 40) });
        var router = Router(project);

        router.Handle(MidiMessage.NoteOn(1, 40, 90));
        Assert.True(router.Controls.IsPadOn(0));

        router.Handle(new MidiMessage(0x80, 40, 0));
        Assert.False(router.Controls.IsPadOn(0));
    }

    [Fact]
    public void ControlChangeButtonPressesAPad()
    {
        var project = new Project();
        project.Live.Pads.Add(new LivePad { Mode = PadMode.Flash, Midi = new MidiBinding(MidiBindingKind.Control, 1, 64) });
        var router = Router(project);

        router.Handle(MidiMessage.ControlChange(1, 64, 127));
        Assert.True(router.Controls.IsPadOn(0));
        router.Handle(MidiMessage.ControlChange(1, 64, 0));
        Assert.False(router.Controls.IsPadOn(0));
    }

    [Fact]
    public void LatestMessageIsReportedOnce()
    {
        var router = Router(new Project());
        long seen = 0;
        Assert.False(router.TryGetLatest(ref seen, out _));

        router.Handle(MidiMessage.ControlChange(3, 21, 64));
        Assert.True(router.TryGetLatest(ref seen, out var message));
        Assert.Equal(MidiMessage.ControlChange(3, 21, 64), message);
        Assert.False(router.TryGetLatest(ref seen, out _));
    }

    [Fact]
    public void QuickOffOnRetriggersThePad()
    {
        var controls = new LiveControls();
        controls.PressPad(0, PadMode.Toggle);
        var first = controls.PadGeneration(0);
        controls.PressPad(0, PadMode.Toggle);
        controls.PressPad(0, PadMode.Toggle);

        Assert.True(controls.IsPadOn(0));
        Assert.NotEqual(first, controls.PadGeneration(0));
    }
}
