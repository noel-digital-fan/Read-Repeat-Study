using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Read_Repeat_Study.Pages;
using Read_Repeat_Study.Services;

namespace Read_Repeat_Study
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            // Initialise the toolkit
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register services
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<ReportService>();

            // Register pages
            builder.Services.AddTransient<FlagsPage>();
            builder.Services.AddTransient<AddEditFlagPage>();
            builder.Services.AddTransient<ReaderPage>();
            builder.Services.AddTransient<HomePage>();


#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
