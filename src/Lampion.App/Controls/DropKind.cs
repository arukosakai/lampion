namespace Lampion.App.Controls;

/// <summary>What is being dragged onto the timeline from another panel; decides where <see cref="TimelineControl.PreviewDrop"/> shows it landing.</summary>
public enum DropKind
{
    /// <summary>An effect or preset from the Effects panel; lands as a clip on a light track.</summary>
    Effect,

    /// <summary>Fixtures or groups from the Project panel; join a light track or make a new one.</summary>
    Targets,

    /// <summary>A transition from the Effects panel; lands on a light clip's left edge.</summary>
    Transition,
}
