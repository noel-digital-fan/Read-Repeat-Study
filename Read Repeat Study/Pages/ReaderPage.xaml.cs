using Read_Repeat_Study.Services;
using System.Text.RegularExpressions;

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

        // Pagination properties
        private List<string> _pages = new();
        private int _currentPageIndex = 0;
        private string _fullText = "";
        private double _availablePageHeight = 0;
        private double _availablePageWidth = 0;
        private bool _isEditingPage = false;
        private Label _measurementLabel;

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
                    _fullText = "";
                    Title = "New Document";
                    SetEditorState(true);
                    _ = Task.Run(async () => await PaginateTextAsync(""));
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
            
            // Create a hidden measurement label with the same properties as the display label
            _measurementLabel = new Label
            {
                FontSize = 18, // Match PageContentLabel
                LineBreakMode = LineBreakMode.WordWrap,
                TextColor = Colors.Transparent,
                IsVisible = false,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Start
            };
        }

        private async void LoadDocument(int id)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Loading document with ID: {id}");
#endif
            CurrentDocument = await _db.GetDocumentByIdAsync(id);
            if (CurrentDocument != null)
            {
                _fullText = CurrentDocument.Content ?? "";
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Document loaded: {CurrentDocument.Name}, Content length: {_fullText.Length}");
#endif
                Title = CurrentDocument.Name;
                SetEditorState(false);
                
                await PaginateTextAsync(_fullText);

                // Set saved voice locale after locales are loaded
                if (!string.IsNullOrWhiteSpace(CurrentDocument.VoiceLocale) && filteredLocales.Any())
                {
                    SetSavedVoiceLocale();
                }
            }
            else
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"No document found with ID: {id}");
#endif
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
            
            // Force a size recalculation after appearing
            await Task.Delay(200);
            await RecalculatePageSizeAsync();
        }
        
        protected override async void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            
            // Wait for layout to complete
            await Task.Delay(100);
            await RecalculatePageSizeAsync();
        }
        
        private async Task RecalculatePageSizeAsync()
        {
            // Wait for layout to stabilize
            await Task.Delay(100);
            
            if (PageContainer != null && PageContainer.Height > 0 && PageContainer.Width > 0)
            {
                var oldHeight = _availablePageHeight;
                var oldWidth = _availablePageWidth;
                
                // Calculate new dimensions accounting for:
                // - Frame padding/margin (12px margin + border)
                // - Label padding (16px on each side = 32px total)
                _availablePageHeight = PageContainer.Height - 32; // Account for Label padding
                _availablePageWidth = PageContainer.Width - 32;   // Account for Label padding
                
                // Ensure minimum dimensions
                _availablePageHeight = Math.Max(_availablePageHeight, 100);
                _availablePageWidth = Math.Max(_availablePageWidth, 100);
                
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Container size: {PageContainer.Height}x{PageContainer.Width}, Available: {_availablePageHeight}x{_availablePageWidth}");
#endif
                
                // Only re-paginate if dimensions changed significantly (more than 10px)
                if (Math.Abs(oldHeight - _availablePageHeight) > 10 || 
                    Math.Abs(oldWidth - _availablePageWidth) > 10)
                {
                    _measurementLabel.WidthRequest = _availablePageWidth;
                    
                    if (!string.IsNullOrEmpty(_fullText))
                    {
                        await PaginateTextAsync(_fullText);
                    }
                }
            }
            else
            {
#if DEBUG
                var containerInfo = PageContainer != null ? $"Height: {PageContainer.Height}, Width: {PageContainer.Width}" : "PageContainer is null";
                System.Diagnostics.Debug.WriteLine($"Container not ready for pagination: {containerInfo}");
#endif
            }
        }
        
        // Add this method to manually trigger re-pagination when needed
        public async Task RefreshPaginationAsync()
        {
            if (!string.IsNullOrEmpty(_fullText))
            {
                await RecalculatePageSizeAsync();
            }
        }
        
        private async Task PaginateTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _pages = new List<string> { "" };
                _currentPageIndex = 0;
                UpdatePageDisplay();
                return;
            }

            // Don't paginate if we don't have proper dimensions yet
            if (_availablePageHeight <= 0 || _availablePageWidth <= 0)
            {
                _pages = new List<string> { text };
                _currentPageIndex = 0;
                UpdatePageDisplay();
                return;
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Paginating with height: {_availablePageHeight}, width: {_availablePageWidth}");
#endif

            _pages.Clear();
            
            // Use a much more efficient algorithm: estimate characters per page first
            var estimatedCharsPerPage = await EstimateCharactersPerPageAsync();
            
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Estimated chars per page: {estimatedCharsPerPage}");
#endif

            // Split text into words to work with manageable chunks
            var words = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var currentPageText = "";
            var currentWordIndex = 0;

            while (currentWordIndex < words.Length)
            {
                // Build a page starting with estimated character count
                var testPageText = "";
                var wordsInPage = 0;
                
                // Add words until we approach the estimated limit
                while (currentWordIndex < words.Length && 
                       testPageText.Length < estimatedCharsPerPage * 0.8) // Use 80% of estimate as safety margin
                {
                    if (string.IsNullOrEmpty(testPageText))
                        testPageText = words[currentWordIndex];
                    else
                        testPageText += " " + words[currentWordIndex];
                    
                    currentWordIndex++;
                    wordsInPage++;
                }

                // Now fine-tune by measuring actual height
                var actualHeight = await MeasureTextHeightAsync(testPageText);
                
                // If too big, remove words until it fits
                while (actualHeight > (_availablePageHeight - 20) && wordsInPage > 1)
                {
                    currentWordIndex--; // Put the last word back
                    wordsInPage--;
                    
                    // Rebuild test text without the last word
                    testPageText = string.Join(" ", words.Skip(currentWordIndex - wordsInPage).Take(wordsInPage));
                    actualHeight = await MeasureTextHeightAsync(testPageText);
                }

                // If still too big and only one word, split the word
                if (actualHeight > (_availablePageHeight - 20) && wordsInPage == 1)
                {
                    var longWord = words[currentWordIndex - 1];
                    var partialWord = "";
                    
                    // Add characters until it doesn't fit
                    for (int i = 0; i < longWord.Length; i++)
                    {
                        var testChar = partialWord + longWord[i];
                        var charHeight = await MeasureTextHeightAsync(testChar);
                        
                        if (charHeight > (_availablePageHeight - 20) && !string.IsNullOrEmpty(partialWord))
                        {
                            break;
                        }
                        partialWord = testChar;
                    }
                    
                    testPageText = partialWord;
                    // Put the rest of the word back for next page
                    words[currentWordIndex - 1] = longWord.Substring(partialWord.Length);
                    currentWordIndex--;
                }

                _pages.Add(testPageText.Trim());
            }

            // Ensure we have at least one page
            if (!_pages.Any())
            {
                _pages.Add("");
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Pagination complete: {_pages.Count} pages created");
#endif

            _currentPageIndex = Math.Min(_currentPageIndex, _pages.Count - 1);
            await MainThread.InvokeOnMainThreadAsync(() => UpdatePageDisplay());
        }

        private async Task<int> EstimateCharactersPerPageAsync()
        {
            // Create a sample text with reasonable line length
            var sampleText = new string('X', 50); // 50 characters per line
            var lineHeight = await MeasureTextHeightAsync(sampleText);
            
            if (lineHeight <= 0) return 2000; // Fallback estimate
            
            var availableLines = Math.Max(1, (int)((_availablePageHeight - 40) / lineHeight)); // 40px buffer
            var estimatedChars = availableLines * 45; // Slightly less than 50 chars per line for safety
            
            return Math.Max(500, estimatedChars); // Minimum 500 characters per page
        }

        private async Task<double> MeasureTextHeightAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var tcs = new TaskCompletionSource<double>();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    // Ensure measurement label matches display label properties exactly
                    _measurementLabel.Text = text;
                    _measurementLabel.WidthRequest = _availablePageWidth;
                    _measurementLabel.FontSize = PageContentLabel.FontSize;
                    _measurementLabel.LineBreakMode = PageContentLabel.LineBreakMode;
                    _measurementLabel.FontFamily = PageContentLabel.FontFamily;
                    
                    // Force measure with exact constraints
                    var size = _measurementLabel.Measure(_availablePageWidth, double.PositiveInfinity);
                    
                    // Add a small buffer to account for line spacing and rendering differences
                    var measuredHeight = size.Height + 5;
                    tcs.SetResult(measuredHeight);
                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Text measurement error: {ex.Message}");
#endif
                    // Improved fallback estimation
                    var lines = Math.Max(1, text.Split('\n').Length);
                    var estimatedLinesFromWidth = Math.Max(1, (int)Math.Ceiling((double)text.Length / 50)); 
                    var totalEstimatedLines = Math.Max(lines, estimatedLinesFromWidth);
                    var estimatedHeight = totalEstimatedLines * 24; // Standard line height
                    tcs.SetResult(estimatedHeight);
                }
            });

            return await tcs.Task;
        }

        private List<string> SplitIntoPhrases(string text)
        {
            var phrases = new List<string>();
            
            // First split by paragraphs to preserve document structure
            var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var paragraph in paragraphs)
            {
                // Split paragraph into sentences
                var sentences = SplitIntoSentences(paragraph);
                
                foreach (var sentence in sentences)
                {
                    var trimmed = sentence.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        phrases.Add(trimmed);
                    }
                }
                
                // Add paragraph break as a separate "phrase"
                if (paragraphs.Length > 1) // Only if there are multiple paragraphs
                {
                    phrases.Add("\n\n");
                }
            }
            
            return phrases;
        }

        private List<string> SplitIntoSentences(string text)
        {
            var sentences = new List<string>();
            
            // Split by sentence endings, including various punctuation
            var pattern = @"(?<=[.!?])\s+|(?<=:)\s+(?=[A-Z])|(?<=;)\s+";
            var parts = Regex.Split(text, pattern);
            
            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    sentences.Add(part);
                }
            }
            
            // If no sentence breaks found, try splitting by commas (for very long sentences)
            if (sentences.Count == 1 && sentences[0].Length > 200)
            {
                var commaParts = sentences[0].Split(',');
                sentences.Clear();
                var currentChunk = "";
                
                foreach (var part in commaParts)
                {
                    if (string.IsNullOrEmpty(currentChunk))
                    {
                        currentChunk = part.Trim();
                    }
                    else if ((currentChunk + ", " + part.Trim()).Length > 150) // Reasonable chunk size
                    {
                        sentences.Add(currentChunk + ",");
                        currentChunk = part.Trim();
                    }
                    else
                    {
                        currentChunk += ", " + part.Trim();
                    }
                }
                
                if (!string.IsNullOrEmpty(currentChunk))
                {
                    sentences.Add(currentChunk);
                }
            }
            
            return sentences;
        }

        private void UpdatePageDisplay()
        {
            if (_pages.Any() && _currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
            {
                PageContentLabel.Text = _pages[_currentPageIndex];
                PageInfoLabel.Text = $"Page {_currentPageIndex + 1} of {_pages.Count}";
                
                PreviousPageButton.IsEnabled = _currentPageIndex > 0;
                NextPageButton.IsEnabled = _currentPageIndex < _pages.Count - 1;
                
                PreviousPageButton.Opacity = PreviousPageButton.IsEnabled ? 1.0 : 0.5;
                NextPageButton.Opacity = NextPageButton.IsEnabled ? 1.0 : 0.5;
            }
            else
            {
                PageContentLabel.Text = "";
                PageInfoLabel.Text = "Page 0 of 0";
                PreviousPageButton.IsEnabled = false;
                NextPageButton.IsEnabled = false;
                PreviousPageButton.Opacity = 0.5;
                NextPageButton.Opacity = 0.5;
            }
        }

        private void OnPreviousPageClicked(object sender, EventArgs e)
        {
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                UpdatePageDisplay();
            }
        }

        private void OnNextPageClicked(object sender, EventArgs e)
        {
            if (_currentPageIndex < _pages.Count - 1)
            {
                _currentPageIndex++;
                UpdatePageDisplay();
            }
        }

        private async void OnJumpToPageClicked(object sender, EventArgs e)
        {
            if (_pages.Count <= 1) return;

            var pageNumber = await DisplayPromptAsync("Jump to Page", 
                $"Enter page number (1-{_pages.Count}):", 
                "Go", "Cancel", 
                keyboard: Keyboard.Numeric);

            if (int.TryParse(pageNumber, out int targetPage) && targetPage >= 1 && targetPage <= _pages.Count)
            {
                _currentPageIndex = targetPage - 1;
                UpdatePageDisplay();
            }
        }

        private async void OnEditPageClicked(object sender, EventArgs e)
        {
            if (_isEditingPage) return;

            var currentPageText = _pages.Any() && _currentPageIndex < _pages.Count ? _pages[_currentPageIndex] : "";
            
            var editedText = await DisplayPromptAsync("Edit Page", 
                "Edit current page content:", 
                initialValue: currentPageText,
                maxLength: 5000,
                keyboard: Keyboard.Text);
            
            if (editedText != null) // Not cancelled
            {
                _pages[_currentPageIndex] = editedText;
                _fullText = string.Join("\n\n", _pages);
                UpdatePageDisplay();
                SetEditorState(true); // Show save button
            }
        }

        void SetEditorState(bool isEditing)
        {
            _isEditingPage = isEditing;
            SaveButton.IsVisible = isEditing;
            EditButton.IsVisible = !isEditing;
            EditPageButton.IsVisible = !isEditing && _pages.Any();
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

                    _fullText = textContent;
                    await PaginateTextAsync(_fullText);
                }
                else
                {
                    await DisplayAlert("Invalid File", "Please select a TXT file.", "OK");
                }
            }
        }

        private async void PlayText()
        {
            string textToRead;
            
            // Read the current page or full text based on user preference
            var choice = await DisplayActionSheet("Read Text", "Cancel", null, "Current Page Only", "Full Document");
            
            if (choice == "Current Page Only")
            {
                textToRead = _pages.Any() && _currentPageIndex < _pages.Count ? _pages[_currentPageIndex] : "";
            }
            else if (choice == "Full Document")
            {
                textToRead = _fullText;
            }
            else
            {
                return; // Cancelled
            }
            
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
            string textToRead = _fullText;
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

        private async void OnEditDocumentClicked(object sender, EventArgs e)
        {
            var editText = await DisplayPromptAsync("Edit Document", 
                "Edit full document content:", 
                initialValue: _fullText,
                maxLength: 50000,
                keyboard: Keyboard.Text);
            
            if (editText != null) // Not cancelled
            {
                _fullText = editText;
                await PaginateTextAsync(_fullText);
                SetEditorState(true);
            }
        }

        private async void OnSaveDocumentClicked(object sender, EventArgs e)
        {
            // Create temp document if new and null
            if (isNewDocument && CurrentDocument == null)
            {
                CurrentDocument = new ImportedDocument
                {
                    Name = "Untitled",
                    Content = _fullText,
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
                CurrentDocument.Content = _fullText;
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
                    Content = _fullText,
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
