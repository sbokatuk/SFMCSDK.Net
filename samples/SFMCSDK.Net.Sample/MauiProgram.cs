using Microsoft.Extensions.Logging;

namespace SFMCSDK.Net.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // The documented pattern: one client per process, registered as a singleton and
        // injected as ISfmcSdkClient. The whole page programs against the interface - which is
        // also what makes the page unit-testable against a fake on a plain target framework.
        builder.Services.AddSingleton<ISfmcSdkClient, SfmcSdkClient>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
