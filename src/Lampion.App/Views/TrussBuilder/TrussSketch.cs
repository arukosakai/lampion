using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.TrussBuilder;

/// <summary>
/// A to-scale front and top drawing of what the truss builder will put up, with the lights where
/// <see cref="StageMath.DistributeAlong"/> will hang them. Call <see cref="Show"/> after every change.
/// </summary>
internal sealed class TrussSketch : Control
{
    private const double Gap = 24;
    private const double Pad = 18;
    private const double LightRadius = 4;
    private const double LightDrop = 0.35;

    private static readonly IBrush Truss = new SolidColorBrush(Color.Parse("#B8BCC4"));
    private static readonly IBrush Light = new SolidColorBrush(Color.Parse("#FFC24A"));
    private static readonly IBrush Caption = Brushes.Gray;
    private static readonly IPen Floor = new Pen(new SolidColorBrush(Color.Parse("#55555C")), 1);
    private static readonly Typeface Face = new("Segoe UI, Inter, sans-serif");

    private TrussBuildSettings? _settings;
    private TrussStructure? _structure;

    /// <summary>Redraws for <paramref name="settings"/>.</summary>
    public void Show(TrussBuildSettings settings)
    {
        // Drawn unturned and at the origin: the sketch shows the shape, not where it stands.
        var unturned = new TrussBuildSettings
        {
            Shape = settings.Shape,
            Width = settings.Width,
            Depth = settings.Depth,
            Height = settings.Height,
            Profile = settings.Profile,
            LightsPerSpan = settings.LightsPerSpan,
            Spans = settings.Spans,
        };
        _structure = unturned.Build();
        _settings = unturned;
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (_settings is not { } settings || _structure is not { } structure)
            return;
        var lit = settings.LitSpans(structure).ToHashSet();
        var halfWidth = (Bounds.Width - Gap) / 2;
        var front = new Rect(0, 0, halfWidth, Bounds.Height);
        var top = new Rect(halfWidth + Gap, 0, halfWidth, Bounds.Height);
        DrawFront(context, front, settings, structure, lit);
        DrawTop(context, top, settings, structure, lit);
    }

    private static void DrawFront(DrawingContext context, Rect area, TrussBuildSettings settings, TrussStructure structure, HashSet<StageObject> lit)
    {
        var view = Fit(area, settings.Width, settings.Height + 0.5, out var origin);
        Point At(double x, double y) => new(origin.X + x * view, origin.Y - y * view);
        context.DrawLine(Floor, At(-settings.Width / 2 - 0.5, 0), At(settings.Width / 2 + 0.5, 0));
        foreach (var piece in structure.Pieces)
        {
            // Pieces running toward the audience show only their end in a front view.
            if (Math.Abs(piece.Yaw - 90) < 1)
                continue;
            var half = new Vec3(Math.Abs(piece.Roll) > 1 ? piece.Size.Y / 2 : piece.Size.X / 2, StageMath.HalfHeight(piece), 0);
            context.FillRectangle(Truss, new Rect(At(piece.Position.X - half.X, piece.Position.Y + half.Y), At(piece.Position.X + half.X, piece.Position.Y - half.Y)));
        }
        var drawnFront = structure.Spans[0];
        if (lit.Contains(drawnFront))
            foreach (var x in LightOffsets(drawnFront.Size.X, settings.LightsPerSpan))
                context.DrawEllipse(Light, null, At(x, drawnFront.Position.Y - drawnFront.Size.Y / 2 - LightDrop), LightRadius, LightRadius);
        Label(context, $"Front · {Metres(settings.Width)} wide, {Metres(settings.Height)} high", area);
    }

    private static void DrawTop(DrawingContext context, Rect area, TrussBuildSettings settings, TrussStructure structure, HashSet<StageObject> lit)
    {
        var depth = settings.Shape == TrussShape.Arch ? TrussStructures.DefaultSection : settings.Depth;
        var view = Fit(area, settings.Width, depth + 1, out var origin);
        var centreY = origin.Y - (depth + 1) / 2 * view;
        // Audience (+Z) at the bottom of the drawing, like looking down from above the stage.
        Point At(double x, double z) => new(origin.X + x * view, centreY + z * view);
        foreach (var piece in structure.Pieces)
        {
            var along = Math.Abs(piece.Yaw - 90) < 1;
            var upright = Math.Abs(piece.Roll) > 1;
            var halfX = upright || along ? piece.Size.Z / 2 : piece.Size.X / 2;
            var halfZ = along ? piece.Size.X / 2 : piece.Size.Z / 2;
            context.FillRectangle(Truss, new Rect(At(piece.Position.X - halfX, piece.Position.Z - halfZ), At(piece.Position.X + halfX, piece.Position.Z + halfZ)));
        }
        foreach (var span in structure.Spans)
        {
            if (!lit.Contains(span))
                continue;
            var along = Math.Abs(span.Yaw - 90) < 1;
            foreach (var offset in LightOffsets(span.Size.X, settings.LightsPerSpan))
            {
                // Yaw 90 turns a span's +X toward the back (-Z).
                var p = along ? At(span.Position.X, span.Position.Z - offset) : At(span.Position.X + offset, span.Position.Z);
                context.DrawEllipse(Light, null, p, LightRadius, LightRadius);
            }
        }
        var caption = settings.Shape == TrussShape.Arch ? "Top" : $"Top · {Metres(settings.Depth)} deep";
        Label(context, caption + " · audience ↓", area);
    }

    /// <summary>Pixels per metre fitting <paramref name="width"/> × <paramref name="height"/> metres in <paramref name="area"/>, and the bottom-centre point.</summary>
    private static double Fit(Rect area, double width, double height, out Point bottomCentre)
    {
        var usable = area.Deflate(new Thickness(Pad, Pad + 14, Pad, Pad));
        var scale = Math.Min(usable.Width / Math.Max(width, 0.1), usable.Height / Math.Max(height, 0.1));
        bottomCentre = new Point(usable.Center.X, usable.Center.Y + height * scale / 2);
        return scale;
    }

    /// <summary>Where <see cref="StageMath.DistributeAlong"/> puts <paramref name="count"/> lights, metres from the span's centre.</summary>
    private static IEnumerable<double> LightOffsets(double length, int count)
    {
        for (var i = 0; i < count; i++)
            yield return -length / 2 + length / count * (i + 0.5);
    }

    private static string Metres(double value) => value.ToString("0.0#", CultureInfo.InvariantCulture) + " m";

    private static void Label(DrawingContext context, string text, Rect area)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 11, Caption);
        context.DrawText(formatted, new Point(area.X + Pad, area.Y + 2));
    }
}
