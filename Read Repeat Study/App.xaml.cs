namespace Read_Repeat_Study
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            
            // Restore saved theme preference
            RestoreThemePreference();
            
            // Add debug to see if App starts
            System.Diagnostics.Debug.WriteLine("=== APP CONSTRUCTOR ===");
        }

        private void RestoreThemePreference()
        {
            // Get saved theme preference, default to system theme
            var savedTheme = Preferences.Get("app_theme", "Unspecified");
            
            if (Enum.TryParse<AppTheme>(savedTheme, out var theme))
            {
                UserAppTheme = theme;
            }
            else
            {
                // Default to system theme
                UserAppTheme = AppTheme.Unspecified;
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}