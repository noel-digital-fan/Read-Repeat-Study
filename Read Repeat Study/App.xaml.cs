namespace Read_Repeat_Study
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // Add debug to see if App starts
            System.Diagnostics.Debug.WriteLine("=== APP CONSTRUCTOR ===");
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}