using Read_Repeat_Study.Pages;

namespace Read_Repeat_Study
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Add debug to see if Shell starts
            System.Diagnostics.Debug.WriteLine("=== APPSHELL CONSTRUCTOR ===");

            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(ReaderPage), typeof(ReaderPage));
            Routing.RegisterRoute(nameof(AddEditFlagPage), typeof(AddEditFlagPage));
        }
    }
}
