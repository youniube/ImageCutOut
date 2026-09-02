using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(ImageCutOut.Core.Tests.TestAppBuilder))]

namespace ImageCutOut.Core.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .UseSkia();
}
