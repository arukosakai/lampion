namespace Lampion.Core.Model.Effects;

/// <summary>
/// Text keyed as International Morse code, laid out in dot-length units: a dot is 1 unit on, a dash 3, with 1 off
/// between the elements of a letter, 3 between letters and 7 between words. Immutable, so it's safe to share.
/// </summary>
internal sealed class MorseCode
{
    /// <summary>Units of silence after the message before it repeats, the same as a word gap.</summary>
    public const int WordGap = 7;

    private const int LetterGap = 3;
    private const int ElementGap = 1;
    private const int Dash = 3;

    private static readonly Dictionary<char, string> Table = new()
    {
        ['A'] = ".-", ['B'] = "-...", ['C'] = "-.-.", ['D'] = "-..", ['E'] = ".", ['F'] = "..-.", ['G'] = "--.",
        ['H'] = "....", ['I'] = "..", ['J'] = ".---", ['K'] = "-.-", ['L'] = ".-..", ['M'] = "--", ['N'] = "-.",
        ['O'] = "---", ['P'] = ".--.", ['Q'] = "--.-", ['R'] = ".-.", ['S'] = "...", ['T'] = "-", ['U'] = "..-",
        ['V'] = "...-", ['W'] = ".--", ['X'] = "-..-", ['Y'] = "-.--", ['Z'] = "--..",
        ['0'] = "-----", ['1'] = ".----", ['2'] = "..---", ['3'] = "...--", ['4'] = "....-", ['5'] = ".....",
        ['6'] = "-....", ['7'] = "--...", ['8'] = "---..", ['9'] = "----.",
        ['.'] = ".-.-.-", [','] = "--..--", ['?'] = "..--..", ['\''] = ".----.", ['!'] = "-.-.--", ['/'] = "-..-.",
        ['('] = "-.--.", [')'] = "-.--.-", ['&'] = ".-...", [':'] = "---...", [';'] = "-.-.-.", ['='] = "-...-",
        ['+'] = ".-.-.", ['-'] = "-....-", ['_'] = "..--.-", ['"'] = ".-..-.", ['$'] = "...-..-", ['@'] = ".--.-.",
    };

    // Start unit of each element, in order, and how many units it stays on.
    private readonly int[] _starts;
    private readonly int[] _lengths;

    private MorseCode(int[] starts, int[] lengths, int length)
    {
        _starts = starts;
        _lengths = lengths;
        Length = length;
    }

    /// <summary>Units from the first element to the end of the last; 0 when nothing in the text could be keyed.</summary>
    public int Length { get; }

    /// <summary>
    /// Encodes <paramref name="text"/>, case-insensitively. Characters Morse has no code for are skipped, and any
    /// run of whitespace is one word gap.
    /// </summary>
    public static MorseCode Encode(string? text)
    {
        var starts = new List<int>();
        var lengths = new List<int>();
        var at = 0;
        var gap = 0;
        foreach (var c in text ?? "")
        {
            if (char.IsWhiteSpace(c))
            {
                if (starts.Count > 0)
                    gap = WordGap;
                continue;
            }
            if (!Table.TryGetValue(char.ToUpperInvariant(c), out var code))
                continue;
            at += gap;
            for (var i = 0; i < code.Length; i++)
            {
                if (i > 0)
                    at += ElementGap;
                var length = code[i] == '-' ? Dash : 1;
                starts.Add(at);
                lengths.Add(length);
                at += length;
            }
            gap = LetterGap;
        }
        return new MorseCode([.. starts], [.. lengths], at);
    }

    /// <summary>Whether the light is on <paramref name="unit"/> units into the message. Allocation-free.</summary>
    public bool IsOn(double unit)
    {
        // The last element starting at or before the unit is the only one that can cover it.
        var lo = 0;
        var hi = _starts.Length - 1;
        var found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (_starts[mid] <= unit)
            {
                found = mid;
                lo = mid + 1;
            }
            else
                hi = mid - 1;
        }
        return found >= 0 && unit < _starts[found] + _lengths[found];
    }
}
