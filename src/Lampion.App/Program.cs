using Avalonia;

namespace Lampion.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // The 3D stage needs a real OpenGL context, so each platform is steered to its GL backend first
    // (ANGLE on Windows, desktop GL on macOS, GLX/EGL on Linux) with software as the last resort.
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
