using Avalonia.Controls;
using Avalonia.Layout;

namespace Lampion.App.Views;

internal sealed partial class StageQualityOptions
{
    /// <summary>A combo over a few fixed values; <see cref="Select"/> moves it without counting as a user change.</summary>
    private sealed class Knob<T>
        where T : struct
    {
        private readonly List<(T Value, string Label)> _options;
        private bool _quiet;

        public Knob(List<(T Value, string Label)> options, T current, Action<T> apply)
        {
            _options = options;
            var labels = new List<string>(options.Count);
            foreach (var (_, label) in options)
                labels.Add(label);
            Combo = new ComboBox { ItemsSource = labels, HorizontalAlignment = HorizontalAlignment.Stretch };
            Select(current);
            Combo.SelectionChanged += (_, _) =>
            {
                if (_quiet || Combo.SelectedIndex < 0)
                    return;
                apply(_options[Combo.SelectedIndex].Value);
                Changed?.Invoke();
            };
        }

        public ComboBox Combo { get; }

        public event Action? Changed;

        public void Select(T value)
        {
            _quiet = true;
            // A value tuned outside these options (an edited settings file) shows as blank rather than lying.
            Combo.SelectedIndex = _options.FindIndex(o => EqualityComparer<T>.Default.Equals(o.Value, value));
            _quiet = false;
        }
    }
}
