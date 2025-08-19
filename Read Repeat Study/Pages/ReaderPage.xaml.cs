namespace Read_Repeat_Study
{
    public partial class ReaderPage : ContentPage
    {
        private bool isRepeating = false;
        CancellationTokenSource ttsCancel;

        private List<Locale> systemLocales = new();
        private List<Locale> filteredLocales = new();
        private Locale selectedLocale;

        private string pendingSearchText = "";

        public ReaderPage()
        {
            InitializeComponent();
        }

        // ReaderPage.xaml.cs

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            systemLocales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();

            // Always keep the full sorted list here
            filteredLocales = systemLocales
                .OrderBy(l => $"{l.Language} - {l.Name} ({l.Country})")
                .ToList();

            VoicePicker.ItemsSource = filteredLocales
                .Select(l => $"{l.Language} - {l.Name} ({l.Country})")
                .ToList();
        }

        private void OnVoicePickerFocused(object sender, FocusEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(pendingSearchText))
            {
                // Reset to full, sorted list
                filteredLocales = systemLocales
                    .OrderBy(l => $"{l.Language} - {l.Name} ({l.Country})")
                    .ToList();

                VoicePicker.ItemsSource = filteredLocales
                    .Select(l => $"{l.Language} - {l.Name} ({l.Country})")
                    .ToList();
            }
        }



        private void OnVoiceChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            if (picker.SelectedIndex >= 0)
            {
                selectedLocale = filteredLocales[picker.SelectedIndex];
            }
        }


        // Only capture the latest search text here; do not update the picker or open it yet
        private void OnVoiceSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            pendingSearchText = e.NewTextValue ?? "";
        }


        // When the user presses enter (search), then filter and open the picker
        private void OnVoiceSearchButtonPressed(object sender, EventArgs e)
        {
            var searchText = pendingSearchText.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show full list
                filteredLocales = systemLocales
                    .OrderBy(l => $"{l.Language} - {l.Name} ({l.Country})")
                    .ToList();
            }
            else
            {
                // Filter based on search
                filteredLocales = systemLocales
                    .Where(l =>
                        l.Language.ToLowerInvariant().Contains(searchText) ||
                        l.Name.ToLowerInvariant().Contains(searchText) ||
                        l.Country.ToLowerInvariant().Contains(searchText))
                    .OrderBy(l => $"{l.Language} - {l.Name} ({l.Country})")
                    .ToList();
            }

            VoicePicker.ItemsSource = filteredLocales
                .Select(l => $"{l.Language} - {l.Name} ({l.Country})")
                .ToList();

            VoicePicker.Focus();
        }

        private async void OnImportDocumentClicked(object sender, EventArgs e)
        {
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a TXT file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new[] { "text/plain" } },
            { DevicePlatform.iOS, new[] { "public.plain-text" } },
            { DevicePlatform.WinUI, new[] { ".txt" } }
        })
            });

            if (fileResult != null)
            {
                var extension = Path.GetExtension(fileResult.FullPath).ToLowerInvariant();
                if (extension == ".txt")
                {
                    using var stream = await fileResult.OpenReadAsync();
                    using StreamReader reader = new(stream);
                    string textContent = await reader.ReadToEndAsync();

                    // Display imported text in the Editor
                    TxtContentEditor.Text = textContent;
                }
                else
                {
                    await DisplayAlert("Invalid File", "Please select a TXT file.", "OK");
                }
            }
        }

        private async void PlayText()
        {
            string textToRead = TxtContentEditor.Text;
            if (!string.IsNullOrWhiteSpace(textToRead))
            {
                ttsCancel = new CancellationTokenSource();
                var options = new SpeechOptions
                {
                    Locale = selectedLocale,
                    // Optional: Pitch and Volume
                    Pitch = 1.0f,
                    Volume = 1.0f
                };
                await TextToSpeech.Default.SpeakAsync(textToRead, options, ttsCancel.Token);
            }
        }


        private async void OnPlayClicked(object sender, EventArgs e)
        {
            ttsCancel?.Cancel();
            string textToRead = TxtContentEditor.Text;
            if (isRepeating)
            {
                StartRepeatingSpeech();
            }
            else
            {
                PlayText();
            }
        }

        private void OnPauseClicked(object sender, EventArgs e)
        {
            ttsCancel?.Cancel();
        }

        private void OnRepeatClicked(object sender, EventArgs e)
        {
            isRepeating = !isRepeating;

            // Change button color
            RepeatButton.BackgroundColor = isRepeating ? Colors.Green : Colors.Red;
        }

        private async void StartRepeatingSpeech()
        {
            string textToRead = TxtContentEditor.Text;
            if (string.IsNullOrWhiteSpace(textToRead))
                return;

            ttsCancel = new CancellationTokenSource();

            try
            {
                while (isRepeating && !ttsCancel.Token.IsCancellationRequested)
                {
                    await TextToSpeech.Default.SpeakAsync(textToRead, cancelToken: ttsCancel.Token);

                    // Optional small delay before repeating
                    await Task.Delay(500, ttsCancel.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Speech was cancelled by user or toggle off
            }
        }



    }
}
