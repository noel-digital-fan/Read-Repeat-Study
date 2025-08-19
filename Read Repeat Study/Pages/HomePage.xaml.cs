using Read_Repeat_Study.Services;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Read_Repeat_Study.Pages
{
    public partial class HomePage : ContentPage
    {
        private readonly DatabaseService _db;

        public Command ItemLongPressedCommand { get; set; }

        public HomePage(DatabaseService db)
        {
            InitializeComponent();

            try
            {
                _db = db;

                ItemLongPressedCommand = new Command<int>(async id =>
                {
                    try
                    {
                        var flag = await _db.GetFlagAsync(id);
                        if (flag == null)
                            return;

                        var action = await DisplayActionSheet("Options", "Cancel", null, "Edit", "Delete");
                        if (action == "Edit")
                        {
                            await Shell.Current.GoToAsync($"AddEditFlagPage?flagId={flag.ID}");
                        }
                        else if (action == "Delete")
                        {
                            await _db.DeleteFlagAsync(flag);
                            FlagsCollection.ItemsSource = await _db.GetAllFlagsAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Error", $"Issue processing flag: {ex.Message}", "OK");
                    }
                });
                BindingContext = this;
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("ERROR", $"Constructor failed: {ex.Message}", "OK");
                });
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            var flags = await _db.GetAllFlagsAsync();
            FlagsCollection.ItemsSource = flags;
        }

        private async void OnAddFlagClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("AddEditFlagPage?flagId=0");
        }

        // This method converts the string color name to a Maui Color
        Color ColorFromName(string colorName)
        {
            return colorName?.ToLower() switch
            {
                "red" => Colors.Red,
                "green" => Colors.Green,
                "blue" => Colors.Blue,
                "yellow" => Colors.Yellow,
                "purple" => Colors.Purple,
                _ => Colors.Transparent,
            };
        }

        // Called whenever a Frame loads inside the CollectionView
        private void Frame_Loaded(object sender, EventArgs e)
        {
            if (sender is Frame frame)
            {
                var flag = frame.BindingContext as Flags;
                if (flag == null)
                    return;

                if (frame.Content is HorizontalStackLayout layout)
                {
                    if (layout.Children[0] is BoxView colorBox)
                    {
                        if (!string.IsNullOrWhiteSpace(flag.Color))
                        {
                            try
                            {
                                colorBox.Color = Color.FromArgb(flag.Color);
                            }
                            catch
                            {
                                // fallback to transparent or a default color
                                colorBox.Color = Colors.Transparent;
                            }
                        }
                        else
                        {
                            colorBox.Color = Colors.Transparent;
                        }
                    }
                }
            }
        }

    }
}
