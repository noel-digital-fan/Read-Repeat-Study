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

        // Helper static properties to receive content from HomePage navigation
        private bool _isLoadingImportedContent = false;

        public ReaderPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Load imported content if any (set by HomePage)
            if (!string.IsNullOrEmpty(ReaderPageHelper.DocumentContent))
            {
                _isLoadingImportedContent = true;
                TxtContentEditor.Text = ReaderPageHelper.DocumentContent;

                if (!string.IsNullOrEmpty(ReaderPageHelper.DocumentName))
                    Title = $"Reader - {ReaderPageHelper.DocumentName}";

                // Clear after loading
                ReaderPageHelper.DocumentContent = null;
                ReaderPageHelper.DocumentName = null;
                _isLoadingImportedContent = false;
            }

            // Load available locales for TTS voice selection
            systemLocales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();
            filteredLocales = systemLocales
                .OrderBy(l => $"{l.Language} - {l.Name} ({l.Country})")
                .ToList();
            VoicePicker.ItemsSource = filteredLocales
                .Select(l => $"{l.Language} - {l.Name} ({l.Country})")
                .ToList();
        }

        protected override void OnDisappearing()
        {
            ttsCancel?.Cancel();
            Title = "Test"; // Reset original page title
            base.OnDisappearing();
        }

        private void OnVoicePickerFocused(object sender, FocusEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(pendingSearchText))
            {
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

        private void OnVoiceSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            pendingSearchText = e.NewTextValue ?? "";
        }

        private void OnVoiceSearchButtonPressed(object sender, EventArgs e)
        {
            var searchText = pendingSearchText.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                filteredLocales = systemLocales
                    .OrderBy(l => $"{l.Language} - {l.Name} ({l.Country})")
                    .ToList();
            }
            else
            {
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
                    Pitch = 1.0f,
                    Volume = 1.0f
                };
                await TextToSpeech.Default.SpeakAsync(textToRead, options, ttsCancel.Token);
            }
        }

        private async void OnPlayClicked(object sender, EventArgs e)
        {
            ttsCancel?.Cancel();
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
                    await Task.Delay(500, ttsCancel.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Speech cancelled
            }
        }
    }

    /// <summary>
    /// Helper static class for passing document content and name from HomePage
    /// </summary>
    public static class ReaderPageHelper
    {
        public static string DocumentContent { get; set; }
        public static string DocumentName { get; set; }
    }
}
