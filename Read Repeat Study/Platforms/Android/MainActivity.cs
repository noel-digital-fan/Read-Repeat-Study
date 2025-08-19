using Android.App;
using Android.Content.PM;
using Microsoft.Maui;

namespace Read_Repeat_Study
{
    [Activity(
        Label = "Read Repeat Study",
        Icon = "@mipmap/appicon",
        RoundIcon = "@mipmap/appicon_round",
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        ConfigurationChanges =
          ConfigChanges.ScreenSize |
          ConfigChanges.Orientation |
          ConfigChanges.UiMode |
          ConfigChanges.ScreenLayout |
          ConfigChanges.SmallestScreenSize)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
