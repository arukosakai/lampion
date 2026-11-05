using System.Globalization;

namespace Lampion.App.Controls.Stage3D;

/// <summary>
/// A label rebuilt from live values every frame without allocating: the text is composed into a reused
/// buffer, and a new string is made only when it reads differently from last time.
/// Usage: <see cref="Clear"/>, a run of <c>Append</c>s, then <see cref="ToText"/>. Not thread-safe.
/// </summary>
internal sealed class CachedText
{
    private char[] _buffer = new char[128];
    private int _length;
    private string _text = "";

    /// <summary>Whether nothing has been appended since <see cref="Clear"/>.</summary>
    public bool IsEmpty => _length == 0;

    /// <summary>Starts composing a new version of the text.</summary>
    public CachedText Clear()
    {
        _length = 0;
        return this;
    }

    /// <summary>Appends literal text.</summary>
    public CachedText Append(ReadOnlySpan<char> text)
    {
        Reserve(text.Length);
        text.CopyTo(_buffer.AsSpan(_length));
        _length += text.Length;
        return this;
    }

    /// <summary>Appends a number formatted as <c>value.ToString(format, provider)</c> would (current culture when <paramref name="provider"/> is null).</summary>
    public CachedText Append<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : struct, ISpanFormattable
    {
        provider ??= CultureInfo.CurrentCulture;
        int written;
        while (!value.TryFormat(_buffer.AsSpan(_length), out written, format, provider))
            Reserve(_buffer.Length);
        _length += written;
        return this;
    }

    /// <summary>Appends metres the way the stage readouts show them: <c>1.25 m</c>, always with a full stop.</summary>
    public CachedText AppendMetres(double metres) => Append(metres, "0.00", CultureInfo.InvariantCulture).Append(" m");

    /// <summary>The composed text; the same string instance as last time when nothing changed.</summary>
    public string ToText()
    {
        var composed = _buffer.AsSpan(0, _length);
        if (!composed.SequenceEqual(_text))
            _text = new string(composed);
        return _text;
    }

    private void Reserve(int extra)
    {
        if (_length + extra > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(_length + extra, _buffer.Length * 2));
    }
}
