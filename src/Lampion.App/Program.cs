using Avalonia;

namespace Lampion.App;

/// <summary>Entry point.</summary>
internal static class Program
{
    /// <summary>Starts the desktop app; the first argument, if any, is a show file to open.</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        Editor.CrashLog.Install();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // The 3D stage needs a real OpenGL context, so each platform is steered to its GL backend first
    // (ANGLE on Windows, desktop GL on macOS, GLX/EGL on Linux) with software as the last resort.
    /// <summary>The app's Avalonia setup. Also found by name by the designer and previewer, so keep it public and static.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software] })
            .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software] })
            .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Glx, X11RenderingMode.Egl, X11RenderingMode.Software] })
            .WithInterFont()
            .LogToTrace();
}
