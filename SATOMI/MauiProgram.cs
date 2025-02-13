// This code sets up the MauiProgram class to configure and build a Microsoft MAUI application.
// It registers the app's main entry point (App), configures fonts, adds a custom handler for ProgressBar,
// and enables logging in debug mode.
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui;

namespace SATOMI
{
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
                })
                .ConfigureMauiHandlers(handlers =>
                {
                    // ProgressBar のハンドラを追加
                    handlers.AddHandler<epj.ProgressBar.Maui.ProgressBar, epj.ProgressBar.Maui.ProgressBarHandler>();
                });
            
#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
