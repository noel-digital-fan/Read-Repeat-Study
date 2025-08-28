using Microsoft.Maui.Graphics;
using Read_Repeat_Study.Services;
using System.Windows.Input;

namespace Read_Repeat_Study.Pages
{
    [QueryProperty(nameof(FlagId), "flagId")]
    public partial class AddEditFlagPage : ContentPage
    {
        public string FlagId { get; set; } // Bound query property for flag ID
        private readonly DatabaseService _db; // Injected database service
        private Flags _flag; // The flag being added/edited

        public ICommand SetBaseColorCommand { get; private set; } // Command to set base colors

        public AddEditFlagPage(DatabaseService db) // Dependency Injection
        {
            InitializeComponent();
            _db = db;

            SetBaseColorCommand = new Command<string>(colorName =>
            {
                Color c = colorName switch
                {
                    "Red" => Colors.Red,
                    "Green" => Colors.Green,
                    "Blue" => Colors.Blue,
                    "Yellow" => Colors.Yellow,
                    "Pink" => Colors.Pink,
                    _ => Colors.Transparent
                };
                ApplyColor(c);
            });
            RedSlider.ValueChanged += OnRgbChanged;
            GreenSlider.ValueChanged += OnRgbChanged;
            BlueSlider.ValueChanged += OnRgbChanged;

            BindingContext = this;
        }

        protected override async void OnAppearing() // On loading the page
        {
            base.OnAppearing();

            if (int.TryParse(FlagId, out var id) && id > 0)
            {
                _flag = await _db.GetFlagAsync(id);
                HeaderLabel.Text = "Edit Flag";
                NameEntry.Text = _flag.Name;

                if (!string.IsNullOrWhiteSpace(_flag.Color))
                {
                    var c = Color.FromArgb(_flag.Color);
                    ApplyColor(c);
                }
            }
            else
            {
                _flag = new Flags();
                HeaderLabel.Text = "Add Flag";
                ApplyColor(Colors.Transparent);
            }
        }

        private void OnRgbChanged(object sender, ValueChangedEventArgs e) // When any RGB slider changes
        {
            var c = new Color(
                (float)(RedSlider.Value / 255.0),
                (float)(GreenSlider.Value / 255.0),
                (float)(BlueSlider.Value / 255.0));
            ApplyColor(c);
        }

        private void ApplyColor(Color c) // Apply a color to the preview and sliders
        {
            RedSlider.Value = c.Red * 255;
            GreenSlider.Value = c.Green * 255;
            BlueSlider.Value = c.Blue * 255;
            ColorPreview.BackgroundColor = c;
        }

        private async void OnSaveClicked(object sender, EventArgs e) // Save button clicked
        {
            string flagName = NameEntry.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(flagName))
            {
                await DisplayAlert("Required Field", "Please enter a name for the flag", "OK");

                NameEntry.BackgroundColor = Colors.LightPink;
                NameEntry.Focus();
                return;
            }

            NameEntry.BackgroundColor = Colors.Transparent;

            _flag.Name = flagName;
            _flag.Color = ColorPreview.BackgroundColor.ToHex();
            await _db.SaveFlagAsync(_flag);
            await Shell.Current.GoToAsync("..");
        }
    }
}
