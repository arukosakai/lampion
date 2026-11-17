using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Lampion.App.Controls.Icons;

/// <summary>
/// One <see cref="Lucide"/> icon, stroked in the inherited foreground, so it follows its button's hover, disabled
/// and active colours like text would.
/// </summary>
internal sealed class LucideIcon : Control
{
    /// <summary>The icon; null draws nothing.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<LucideIcon, Geometry?>(nameof(Icon));

    /// <summary>Edge length in pixels.</summary>
    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<LucideIcon, double>(nameof(Size), 14);

    /// <summary>Fills the outline too, e.g. a keyframe diamond with a key on it.</summary>
    public static readonly StyledProperty<bool> FilledProperty = AvaloniaProperty.Register<LucideIcon, bool>(nameof(Filled));

    /// <summary>The stroke (and fill) brush; inherited from the parent, like text.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LucideIcon>();

    private IPen? _pen;

    static LucideIcon()
    {
        AffectsRender<LucideIcon>(IconProperty, FilledProperty, ForegroundProperty);
        AffectsMeasure<LucideIcon>(SizeProperty);
    }

    /// <summary>An icon control showing <paramref name="icon"/> at <paramref name="size"/> pixels.</summary>
    public LucideIcon(Geometry icon, double size = 14)
    {
        Icon = icon;
        Size = size;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="FilledProperty"/>
    public bool Filled
    {
        get => GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
            _pen = null;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Icon is not { } icon)
            return;
        var brush = Foreground ?? Brushes.Gray;
        _pen ??= IconPainter.Pen(brush);
        IconPainter.Draw(context, icon, new Rect(0, 0, Size, Size), _pen, Filled ? brush : null);
    }
}
