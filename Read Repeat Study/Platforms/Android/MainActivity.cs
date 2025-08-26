using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Microsoft.Maui;
using System.Text;

namespace Read_Repeat_Study
{
    [Activity(
        Label = "Read Repeat Study",
        Icon = "@mipmap/appicon",
        RoundIcon = "@mipmap/appicon_round",
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        Exported = true,
        ConfigurationChanges =
          ConfigChanges.ScreenSize |
          ConfigChanges.Orientation |
          ConfigChanges.UiMode |
          ConfigChanges.ScreenLayout |
          ConfigChanges.SmallestScreenSize)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            try
            {
                base.OnCreate(savedInstanceState);
                Log.Debug("RRS", "MainActivity OnCreate OK (release) ");
                AppendDiag("MainActivity OnCreate reached.\n");
            }
            catch (System.Exception ex)
            {
                Log.Error("RRS", "MainActivity crash: " + ex);
                AppendDiag("MainActivity crash: " + ex + "\n");
                throw;
            }
        }

        void AppendDiag(string text)
        {
            try
            {
                var path = System.IO.Path.Combine(FileSystem.AppDataDirectory, "crash.log");
                System.IO.File.AppendAllText(path, System.DateTime.UtcNow.ToString("u") + " " + text);
            }
            catch { }
        }
    }
}
