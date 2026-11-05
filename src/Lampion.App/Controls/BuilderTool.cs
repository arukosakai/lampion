namespace Lampion.App.Controls;

/// <summary>The stage view's tools, Unity style: Hand only looks around, Move and Aim edit the rig.</summary>
internal enum BuilderTool
{
    /// <summary>Every button orbits or pans the camera; nothing is edited (Q).</summary>
    Hand,

    /// <summary>Click to select, drag to move fixtures and set pieces, Alt+drag for height (W).</summary>
    Move,

    /// <summary>Click or drag on the stage to point the selected fixtures there (A).</summary>
    Aim,
}
