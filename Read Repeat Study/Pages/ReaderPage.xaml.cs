using Read_Repeat_Study.Services;

namespace Read_Repeat_Study
{
    [QueryProperty(nameof(DocumentId), "docId")]
    public partial class ReaderPage : ContentPage
    {
        private bool isRepeating = false;
        CancellationTokenSource ttsCancel;
        private List<Locale> systemLocales = new();
        private List<Locale> filteredLocales = new();
        private Locale selectedLocale;
        private string pendingSearchText = "";
        private ImportedDocument CurrentDocument;
        private readonly DatabaseService _db;
        private bool isNewDocument;
        private int _documentId;

        public int DocumentId
        {
            get => _documentId;
            set
            {
                _documentId = value;
                if (_documentId <= 0)  // Check for -1 or any non-positive ID indicating new document
                {
                    isNewDocument = true;
                    CurrentDocument = new ImportedDocument { Name = "Untitled", Content = "", VoiceLocale = null };
                    TxtContentEditor.Text = "";
                    Title = "New Document";
                    SetEditorState(true);
                }
                else
                {
                    isNewDocument = false;
                    LoadDocument(value);
                }
            }
        }

        public ReaderPage(DatabaseService db)
        {
            InitializeComponent();
            _db = db;
        }

        private async void LoadDocument(int id)
        {
            CurrentDocument = await _db.GetDocumentByIdAsync(id);
            if (CurrentDocument != null)
            {
                TxtContentEditor.Text = CurrentDocument.Content;
                Title = CurrentDocument.Name;
                SetEditorState(false);

                // Set saved voice locale after locales are loaded
                if (!string.IsNullOrWhiteSpace(CurrentDocument.VoiceLocale) && filteredLocales.Any())
                {
                    SetSavedVoiceLocale();
                }
            }
        }

        void SetEditorState(bool isEditing)
        {
            TxtContentEditor.IsReadOnly = !isEditing;
            SaveButton.IsVisible = isEditing;
            EditButton.IsVisible = !isEditing;
        }

        private void SetSavedVoiceLocale()
        {
            if (CurrentDocument?.VoiceLocale != null && systemLocales.Any())
            {
                // Match using language-country-name string for consistency
                var savedLocale = systemLocales.FirstOrDefault(l =>
                    $"{l.Language}-{l.Country}-{l.Name}" == CurrentDocument.VoiceLocale);

                if (savedLocale != null)
                {
                    var index = filteredLocales.FindIndex(l =>
                        $"{l.Language}-{l.Country}-{l.Name}" == CurrentDocument.VoiceLocale);

                    if (index >= 0)
                    {
                        VoicePicker.SelectedIndex = index;
                        selectedLocale = filteredLocales[index];
                    }
                    else
                    {
                        // If not found in filteredLocales, reset selection to savedLocale
                        selectedLocale = savedLocale;
                    }
                }
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Load system locales and bind picker
            systemLocales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();
            filteredLocales = systemLocales.OrderBy(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();
            VoicePicker.ItemsSource = filteredLocales.Select(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();

            // If CurrentDocument loaded before OnAppearing, set voice picker
            if (CurrentDocument != null && !string.IsNullOrWhiteSpace(CurrentDocument.VoiceLocale))
            {
                SetSavedVoiceLocale();
            }
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

                // Fire and forget saving
                _ = SaveVoiceSelectionAsync();
            }
        }

        private async Task SaveVoiceSelectionAsync()
        {
            if (CurrentDocument != null)
            {
                CurrentDocument.VoiceLocale = $"{selectedLocale.Language}-{selectedLocale.Country}-{selectedLocale.Name}";
                await _db.SaveDocumentAsync(CurrentDocument);
            }
        }



        private void OnVoiceSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            pendingSearchText = e.NewTextValue ?? "";
        }

        private void OnVoiceSearchButtonPressed(object sender, EventArgs e)
        {
            var currentSelection = selectedLocale;

            var searchText = pendingSearchText.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                filteredLocales = systemLocales.OrderBy(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();
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

            VoicePicker.ItemsSource = filteredLocales.Select(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();

            // Restore selection if still available after filtering
            if (currentSelection != null)
            {
                var index = filteredLocales.FindIndex(l =>
                    l.Language == currentSelection.Language &&
                    l.Country == currentSelection.Country &&
                    l.Name == currentSelection.Name);
                if (index >= 0)
                {
                    VoicePicker.SelectedIndex = index;
                }
            }

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
                try
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
                catch { /* handle errors if needed */ }
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

        private async Task<Flags> SelectFlagAsync()
        {
            var flags = await _db.GetAllFlagsAsync();
            if (!flags.Any())
            {
                var createFlag = await DisplayAlert("No Flags",
                    "No flags exist. Would you like to create one?", "Yes", "No");
                if (createFlag)
                {
                    await Shell.Current.GoToAsync("AddEditFlagPage?flagId=0");
                    return null;
                }
                return null;
            }

            var flagNames = flags.Select(f => f.Name).ToArray();
            var selectedFlagName = await DisplayActionSheet("Select Flag", "Cancel", null, flagNames);
            if (string.IsNullOrWhiteSpace(selectedFlagName) || selectedFlagName == "Cancel")
                return null;
            return flags.FirstOrDefault(f => f.Name == selectedFlagName);
        }

        private void OnEditDocumentClicked(object sender, EventArgs e)
        {
            TxtContentEditor.IsReadOnly = false;
            SaveButton.IsVisible = true;
            EditButton.IsVisible = false;
        }

        private async void OnSaveDocumentClicked(object sender, EventArgs e)
        {
            // Create temp document if new and null
            if (isNewDocument && CurrentDocument == null)
            {
                CurrentDocument = new ImportedDocument
                {
                    Name = "Untitled",
                    Content = TxtContentEditor.Text,
                    ImportedDate = DateTime.Now
                };
            }

            // Show error if no doc and not new doc
            if (CurrentDocument == null && !isNewDocument)
            {
                await DisplayAlert("Error", "No document loaded.", "OK");
                return;
            }

            string action;
            if (isNewDocument)
            {
                action = await DisplayActionSheet("Save Document", "Cancel", null, "Save");
                if (action == "Save")
                    action = "Create New"; // Treat as new internally
            }
            else
            {
                action = await DisplayActionSheet("Save Document", "Cancel", null, "Override Existing", "Create New");
            }

            if (action == "Override Existing" && !isNewDocument && CurrentDocument != null)
            {
                CurrentDocument.Content = TxtContentEditor.Text;
                if (selectedLocale != null)
                    CurrentDocument.VoiceLocale = $"{selectedLocale.Language}-{selectedLocale.Country}-{selectedLocale.Name}";
                await _db.SaveDocumentAsync(CurrentDocument);
                await DisplayAlert("Saved", "Changes saved to the existing document.", "OK");
                SetEditorState(false);
            }
            else if (action == "Create New" || isNewDocument)
            {
                var newName = await DisplayPromptAsync("New Document", "Enter document name:", CurrentDocument?.Name.Replace("Untitled", "New Document"));
                if (string.IsNullOrWhiteSpace(newName))
                    return;

                Flags selectedFlag = null;
                bool addFlag = await DisplayAlert("Add Flag", "Add a flag to this document?", "Yes", "No");
                if (addFlag)
                    selectedFlag = await SelectFlagAsync();

                var newDoc = new ImportedDocument
                {
                    Name = newName,
                    Content = TxtContentEditor.Text,
                    ImportedDate = DateTime.Now,
                    Flag = selectedFlag,
                    FlagId = selectedFlag?.ID,
                    VoiceLocale = selectedLocale != null ? $"{selectedLocale.Language}-{selectedLocale.Country}-{selectedLocale.Name}" : null
                };
                await _db.SaveDocumentAsync(newDoc);
                CurrentDocument = newDoc;
                isNewDocument = false;
                await DisplayAlert("Saved", "New document saved.", "OK");
                SetEditorState(false);
            }
            else
            {
                if (isNewDocument)
                {
                    CurrentDocument = null;
                    await DisplayAlert("Discarded", "Document creation cancelled.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
            }
        }
    }
}
