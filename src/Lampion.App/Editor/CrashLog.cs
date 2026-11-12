using System.Diagnostics;

namespace Lampion.App.Editor;

/// <summary>
/// Writes anything that escapes every other handler to <c>crash.log</c> in the user data folder, so a crash can be
/// diagnosed afterwards instead of vanishing. Background-task failures nobody awaited are logged and kept from
/// ever taking the process down.
/// </summary>
internal static class CrashLog
{
    /// <summary>Where the log goes.</summary>
    public static string PathName => Path.Combine(UserData.Root, "crash.log");

    /// <summary>Hooks the process-wide handlers. Call once, first thing in Main.</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("Unhandled", e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("Unobserved task", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Appends <paramref name="error"/> with a timestamp. Never throws.</summary>
    public static void Write(string kind, object error)
    {
        try
        {
            Directory.CreateDirectory(UserData.Root);
            File.AppendAllText(PathName, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {kind}: {error}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            Trace.TraceError("Couldn't write the crash log: " + ex.Message);
        }
    }
}
