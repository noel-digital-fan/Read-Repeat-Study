using Android.App;
using Android.Runtime;
using System.Text;

namespace Read_Repeat_Study
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();
            try
            {
                // Register global handlers so we can capture the actual crash in Release
                AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
                {
                    try { LogCrash("AndroidEnvironment", e.Exception); } catch { }
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try { LogCrash("AppDomain", e.ExceptionObject as Exception); } catch { }
                };
                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    try { LogCrash("TaskScheduler", e.Exception); } catch { }
                };
            }
            catch { /* swallow – never block app start */ }
        }

        static void LogCrash(string source, Exception? ex)
        {
            if (ex == null) return;
            var sb = new StringBuilder();
            sb.AppendLine(DateTime.UtcNow.ToString("u"));
            sb.AppendLine($"SOURCE: {source}");
            while (ex != null)
            {
                sb.AppendLine(ex.GetType().FullName);
                sb.AppendLine(ex.Message);
                sb.AppendLine(ex.StackTrace);
                ex = ex.InnerException;
                if (ex != null) sb.AppendLine("-- Inner Exception --");
            }
            sb.AppendLine(new string('-', 60));
            try
            {
                var path = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
                File.AppendAllText(path, sb.ToString());
            }
            catch { }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
