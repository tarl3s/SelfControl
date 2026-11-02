using Avalonia;
using Avalonia.Headless;
using SelfControl.UiTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace SelfControl.UiTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SelfControl.App.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
