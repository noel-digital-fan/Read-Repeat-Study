using Read_Repeat_Study.Pages;

namespace Read_Repeat_Study
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for navigation
            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(FlagsPage), typeof(FlagsPage));
            Routing.RegisterRoute(nameof(ReaderPage), typeof(ReaderPage));
            Routing.RegisterRoute(nameof(AddEditFlagPage), typeof(AddEditFlagPage));
            Routing.RegisterRoute("AddDocumentPage", typeof(ReaderPage));
        }
    }
}
