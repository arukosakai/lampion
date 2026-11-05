using System.Runtime.CompilerServices;
using System.Text;

namespace Lampion.Core.Tests.Golden;

/// <summary>
/// Reads golden files next to this source file. Set LAMPION_WRITE_GOLDEN=1 to (re)write them, which is only
/// legitimate when the output is meant to change; a refactor must never need it.
/// </summary>
internal static class GoldenFile
{
    private static string Directory([CallerFilePath] string source = "") => Path.GetDirectoryName(source)!;

    public static void AssertMatches(string name, string actual)
    {
        var path = Path.Combine(Directory(), name);
        actual = Normalize(actual);
        if (Environment.GetEnvironmentVariable("LAMPION_WRITE_GOLDEN") == "1")
        {
            File.WriteAllText(path, actual, new UTF8Encoding(false));
            return;
        }
        Assert.True(File.Exists(path), $"Golden file {name} is missing.");
        var expected = Normalize(File.ReadAllText(path));
        if (expected == actual)
            return;
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            var e = i < expectedLines.Length ? expectedLines[i] : "<missing>";
            var a = i < actualLines.Length ? actualLines[i] : "<missing>";
            if (e != a)
                Assert.Fail($"{name} differs at line {i + 1}:\nexpected: {Clip(e)}\nactual:   {Clip(a)}");
        }
    }

    public static string Read(string name) => Normalize(File.ReadAllText(Path.Combine(Directory(), name)));

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string Clip(string line) => line.Length > 300 ? line[..300] + "…" : line;
}
