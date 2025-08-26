using Read_Repeat_Study.Services;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Read_Repeat_Study;

public class PhraseViewModel : INotifyPropertyChanged
{
    private Color _textColor = Colors.Black; // default text color
    private Color _backgroundColor = Colors.Transparent; // default background color
    public string Text { get; set; } = string.Empty; // phrase text
    public Color TextColor { get => _textColor; set { if (_textColor != value) { _textColor = value; OnPropertyChanged(); } } } // text color with notification
    public Color BackgroundColor { get => _backgroundColor; set { if (_backgroundColor != value) { _backgroundColor = value; OnPropertyChanged(); } } } // background color with notification
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); // notify property change
}

[QueryProperty(nameof(DocumentId), "docId")]
[QueryProperty(nameof(StartInEdit), "edit")]
public partial class ReaderPage : ContentPage
{
    private readonly DatabaseService _db; // database service
    private ImportedDocument? CurrentDocument; // current document
    private int _documentId; // current document ID
    private bool _isNewDocument; // tracks if document is new
    private readonly List<string> _pages = new(); // all pages
    private readonly List<List<PhraseViewModel>> _pagePhrases = new(); // phrases per page
    private int _currentPageIndex; // current page index
    private string _fullText = string.Empty; // entire document text
    private bool _isPlaying; // play mode tracking
    private bool _isPaused; // pause mode tracking
    private bool _isRepeating = false; // Add repeat mode tracking
    private int _resumePageIndex; // indices to resume from when paused
    private int _resumePhraseIndex; // indices to resume from when paused
    private int _currentPhraseIndex; // currently spoken phrase index
    private CancellationTokenSource? ttsCancel; // cancellation for TTS
    private bool _isEditing; // edit mode flag
    private List<Locale> systemLocales = new(); // all available voices
    private List<Locale> filteredLocales = new(); // filtered voices for picker
    private Locale? selectedLocale; // currently selected voice
    private string pendingSearchText = string.Empty; // text in search box
    public bool StartInEdit { get; set; } = false; // whether to start in edit mode
    public ObservableCollection<PhraseViewModel> CurrentPagePhrases { get; } = new(); // phrases for current page

    private bool _completedDocument; // end-of-document playback finished
    private int _lastPhrasePage; // page index of last spoken phrase
    private int _lastPhraseIndex; // phrase index of last spoken phrase
    private bool _userSelected; // user explicitly tapped a phrase
    private int _selectedStartPage; // page index to start from on user tap
    private int _selectedStartPhrase; // phrase index to start from on user tap

    private Task? _loadDocumentTask; // track async load
    private string? _pendingVoiceLocale; // voice locale to apply after locales load

    private bool _suppressPageSave; // prevents save recursion during initial load

    public int DocumentId // Document ID property
    {
        get => _documentId;
        set
        {
            _documentId = value;
            if (_documentId <= 0)
            {
                _isNewDocument = true;
                CurrentDocument = new ImportedDocument 
                { 
                    Name = "Untitled", 
                    Content = string.Empty,
                    FilePath = null, // Internal storage only
                    ImportedDate = DateTime.Now,
                    LastPageIndex = 0
                };
                _fullText = string.Empty;
                Title = "New Document";
                SetEditMode(true);
                _ = PaginateAsync(_fullText);
            }
            else
            {
                _isNewDocument = false;
                _loadDocumentTask = LoadDocumentAsync(_documentId);
            }
        }
    }

    public ReaderPage(DatabaseService db) // Database service injected
    {
        InitializeComponent();
        _db = db;
        BindingContext = this;
    }

    private async Task LoadDocumentAsync(int id) // Load document from database
    {
        CurrentDocument = await _db.GetDocumentByIdAsync(id);
        if (CurrentDocument != null)
        {
            _fullText = CurrentDocument.Content ?? string.Empty;
            Title = CurrentDocument.Name;
            SetEditMode(false);
            await PaginateAsync(_fullText);
            _pendingVoiceLocale = CurrentDocument.VoiceLocale;
            
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Loaded document: {CurrentDocument.Name}, LastPageIndex: {CurrentDocument.LastPageIndex}, FilePath: {CurrentDocument.FilePath ?? "Database only"}");
#endif
            
            if (StartInEdit) SetEditMode(true);
        }
    }

    protected override async void OnAppearing() // On entering page
    {
        base.OnAppearing();
        systemLocales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();
        filteredLocales = systemLocales.OrderBy(l => l.Language + l.Name + l.Country).ToList();
        VoicePicker.ItemsSource = filteredLocales.Select(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();
        if (_loadDocumentTask != null)
        {
            try { await _loadDocumentTask; } catch { /* Nothing happens */ }
        }
        if (_pendingVoiceLocale != null)
        {
            ApplySavedVoiceLocale();
        }
        else if (CurrentDocument?.VoiceLocale != null)
        {
            ApplySavedVoiceLocale();
        }

        await Task.Delay(50);
        ScrollToTop();
        if (StartInEdit && !_isEditing) SetEditMode(true);
    }

    protected override async void OnDisappearing() // On leaving page
    {
        base.OnDisappearing();
        ttsCancel?.Cancel();
        _isPlaying = false;
        _isPaused = false;
        _isRepeating = false;
        
        if (CurrentDocument != null && !_isNewDocument)
        {
            await SaveCurrentPageAsync();
        }
    }

    private void ApplySavedVoiceLocale() // Select saved voice in picker
    {
        string? target = _pendingVoiceLocale ?? CurrentDocument?.VoiceLocale;
        if (string.IsNullOrWhiteSpace(target)) return;
        var idx = filteredLocales.FindIndex(l => $"{l.Language}-{l.Country}-{l.Name}" == target);
        if (idx >= 0)
        {
            VoicePicker.SelectedIndex = idx;
            selectedLocale = filteredLocales[idx];
            _pendingVoiceLocale = null;
        }
    }

    private Task PaginateAsync(string text) // Split text into pages and phrases
    {
        _pages.Clear();
        _pagePhrases.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            _pages.Add(string.Empty);
            _pagePhrases.Add(new());
            _currentPageIndex = 0;
            UpdatePageDisplay();
            return Task.CompletedTask;
        }
        foreach (var block in text.Split(new[] { "\n\n" }, StringSplitOptions.None).Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            _pages.Add(block);
            _pagePhrases.Add(CreatePhrasesFromText(block));
        }
        if (_pages.Count == 0)
        {
            _pages.Add(string.Empty);
            _pagePhrases.Add(new());
        }
        _currentPageIndex = Math.Min(_currentPageIndex, _pages.Count - 1);
        
        RestoreSavedPageIfAny();
        
        UpdatePageDisplay();
        return Task.CompletedTask;
    }

    private List<PhraseViewModel> CreatePhrasesFromText(string text) // Split text into phrases
    {
        var list = new List<PhraseViewModel>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        var pattern = @"(?<=[.!?:])\s+|(?<=;)\s+|(?:\r?\n){1,}";
        foreach (var part in Regex.Split(text, pattern))
        {
            var t = part.Trim();
            if (!string.IsNullOrEmpty(t)) 
            {
                var phrase = new PhraseViewModel { Text = t };
                phrase.TextColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black;
                list.Add(phrase);
            }
        }
        if (list.Count == 0) 
        {
            var phrase = new PhraseViewModel { Text = text };
            phrase.TextColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black;
            list.Add(phrase);
        }
        return list;
    }

    private void UpdatePageDisplay() // Update UI for current page
    {
        if (_currentPageIndex < 0 || _currentPageIndex >= _pages.Count) return;
        CurrentPagePhrases.Clear();
        foreach (var p in _pagePhrases[_currentPageIndex]) CurrentPagePhrases.Add(p);
        if (!_isEditing) RenderPhrases();
        PageInfoLabel.Text = $"Page {_currentPageIndex + 1} of {_pages.Count}";
        PreviousPageButton.IsEnabled = _currentPageIndex > 0 && !_isPlaying;
        NextPageButton.IsEnabled = _currentPageIndex < _pages.Count - 1 && !_isPlaying;
        ScrollToTop();

        if (!_suppressPageSave)
            _ = SaveCurrentPageAsync();
    }

    private void RenderPhrases() // Render phrases in the UI
    {
        PhraseStack.Children.Clear();
        for (int i = 0; i < CurrentPagePhrases.Count; i++)
        {
            var vm = CurrentPagePhrases[i];
            var lbl = new Label
            {
                Text = vm.Text,
                FontSize = 15,
                TextColor = vm.TextColor,
                BackgroundColor = vm.BackgroundColor,
                LineBreakMode = LineBreakMode.WordWrap
            };
            
            lbl.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            
            int capture = i;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                _userSelected = true;
                _isPaused = false; 
                _selectedStartPage = _currentPageIndex;
                _selectedStartPhrase = capture;
                await PlayFromAsync(_currentPageIndex, capture);
            };
            lbl.GestureRecognizers.Add(tap);
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PhraseViewModel.TextColor)) lbl.TextColor = vm.TextColor;
                if (e.PropertyName == nameof(PhraseViewModel.BackgroundColor)) lbl.BackgroundColor = vm.BackgroundColor;
            };
            PhraseStack.Children.Add(lbl);
        }
    }

    private void ScrollToTop() => PhraseScrollView.ScrollToAsync(0, 0, false); // Scroll to top of phrases
    private void ScrollToPhrase(int phraseIndex) // Scroll to make phrase visible
    {
        if (phraseIndex < 0 || phraseIndex >= PhraseStack.Children.Count) return;
        if (PhraseStack.Children[phraseIndex] is VisualElement ve)
            PhraseScrollView.ScrollToAsync(ve, ScrollToPosition.Center, true);
    }

    private async Task PlayFromAsync(int startPage, int startPhrase) // Play from specified page and phrase
    {
        if (selectedLocale == null)
        {
            await DisplayAlert("Voice Required", "Select a voice first.", "OK");
            return;
        }
        if (_pages.Count == 0) return;
        if (_isPaused && !_userSelected)
        {
            startPage = _resumePageIndex;
            startPhrase = _resumePhraseIndex;
            _isPaused = false;
        }
        ttsCancel?.Cancel();
        ttsCancel = new CancellationTokenSource();
        var token = ttsCancel.Token;
        _isPlaying = true;
        _completedDocument = false;
        try
        {
            do
            {
                for (int p = startPage; p < _pages.Count; p++)
                {
                    _currentPageIndex = p;
                    UpdatePageDisplay();
                    int phraseStart = (p == startPage ? startPhrase : 0);
                    for (int i = phraseStart; i < CurrentPagePhrases.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        _currentPhraseIndex = i;
                        HighlightPhrase(i);
                        ScrollToPhrase(i);
                        await TextToSpeech.Default.SpeakAsync(CurrentPagePhrases[i].Text, new SpeechOptions { Locale = selectedLocale }, token);
                        DimPhrase(i);
                    }
                }
                if (!_isPaused && !_userSelected)
                {
                    _completedDocument = true;
                    _lastPhrasePage = _currentPageIndex;
                    _lastPhraseIndex = _currentPhraseIndex;
                }
                
                if (_isRepeating && !token.IsCancellationRequested)
                {
                    startPage = 0;
                    startPhrase = 0;
                    await Task.Delay(500, token);
                }
            } 
            while (_isRepeating && !token.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            if (_isPaused)
            {
                _resumePageIndex = _currentPageIndex;
                _resumePhraseIndex = _currentPhraseIndex;
            }
            else
            {
                ResetHighlighting();
            }
            _completedDocument = false;
        }
        finally
        {
            _isPlaying = false;
            if (!_isPaused)
            {
                _resumePageIndex = 0;
                _resumePhraseIndex = 0;
            }
            PreviousPageButton.IsEnabled = _currentPageIndex > 0;
            NextPageButton.IsEnabled = _currentPageIndex < _pages.Count - 1;
            _userSelected = false;
        }
    }

    private void HighlightPhrase(int index) // Highlight currently spoken phrase
    {
        for (int i = 0; i < CurrentPagePhrases.Count; i++)
        {
            var vm = CurrentPagePhrases[i];
            if (i == index)
            {
                vm.BackgroundColor = Colors.Yellow;
                vm.TextColor = Colors.Black;
            }
            else if (vm.BackgroundColor == Colors.Transparent)
            {
                vm.TextColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black;
            }
        }
    }
    
    private void DimPhrase(int index) // Dim phrase after spoken
    {
        if (index >= 0 && index < CurrentPagePhrases.Count)
        {
            var vm = CurrentPagePhrases[index];
            vm.BackgroundColor = Colors.Transparent;
            vm.TextColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.Gray;
        }
    }
    
    private void ResetHighlighting() // Clear all highlights and restore normal text color
    {
        foreach (var vm in CurrentPagePhrases)
        {
            vm.BackgroundColor = Colors.Transparent;
            vm.TextColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black;
        }
    }

    private void OnPreviousPageClicked(object sender, EventArgs e) // Previous page
    {
        if (_isPlaying) return;
        if (_currentPageIndex > 0)
        {
            _currentPageIndex--;
            UpdatePageDisplay();
        }
    }
    
    private void OnNextPageClicked(object sender, EventArgs e) // Next page
    {
        if (_isPlaying) return;
        if (_currentPageIndex < _pages.Count - 1)
        {
            _currentPageIndex++;
            UpdatePageDisplay();
        }
    }

    private void SetEditMode(bool editing) // Switch between edit and read modes
    {
        if (_isEditing && !editing)
        {
            _fullText = PageContentEditor.Text ?? string.Empty;
            _ = PaginateAsync(_fullText);
        }
        _isEditing = editing;
        PageContentEditor.IsVisible = editing;
        PhraseScrollView.IsVisible = !editing;
        EditButton.IsVisible = !editing;
        SaveButton.IsVisible = editing;
        if (editing)
        {
            PageContentEditor.Text = _fullText;
            PageContentEditor.Focus();
        }
        else if (CurrentPagePhrases.Any())
        {
            RenderPhrases();
            ScrollToTop();
        }
    }
    
    private void OnEditDocumentClicked(object sender, EventArgs e) => SetEditMode(true); // Switch to edit mode
    private async void OnSaveDocumentClicked(object sender, EventArgs e) // Save and exit edit mode
    {
        _fullText = PageContentEditor.Text ?? string.Empty;
        await PaginateAsync(_fullText);
        
        await SaveEditedContentAsync();
        SetEditMode(false); 
    }

    private async Task SaveEditedContentAsync() // Save edited content 
    {
        if (CurrentDocument == null) return;

        try
        {
            if (_isNewDocument)
            {
                CurrentDocument.Content = _fullText;
                CurrentDocument.Name = CurrentDocument.Name ?? "Untitled";
                await _db.SaveDocumentAsync(CurrentDocument);
                _isNewDocument = false;
                Title = CurrentDocument.Name;
                await DisplayAlert("Success", "Document saved!", "OK");
                return;
            }

            var choice = await DisplayActionSheet(
                "Save Options", 
                "Cancel", 
                null, 
                "Override Changes", 
                "Save as New Document");

            switch (choice)
            {
                case "Override Changes":
                    CurrentDocument.Content = _fullText;
                    CurrentDocument.Name = CurrentDocument.Name ?? "Untitled";
                    await _db.SaveDocumentAsync(CurrentDocument);
                    await DisplayAlert("Success", "Document overridden!", "OK");
                    return;

                case "Save as New Document":
                    await SaveAsNewDocumentAsync();
                    return;

                case "Cancel":
                default:
                    return; 
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    private async Task SaveAsNewDocumentAsync() // Save content as a new document
    {
        try
        {
            var newName = await DisplayPromptAsync(
                "Save as New Document", 
                "Enter a name for the new document:", 
                "Save", 
                "Cancel", 
                placeholder: "Enter document name here...");

            if (string.IsNullOrWhiteSpace(newName))
                return; 

            var newDocument = new ImportedDocument
            {
                Name = newName.Trim(),
                Content = _fullText, 
                ImportedDate = DateTime.Now,
                FilePath = null, 
                FlagId = CurrentDocument?.FlagId,
                Flag = CurrentDocument?.Flag,
                VoiceLocale = CurrentDocument?.VoiceLocale,
                LastPageIndex = 0 
            };

            await _db.SaveDocumentAsync(newDocument);
            
            CurrentDocument = newDocument;
            _documentId = newDocument.ID;
            _isNewDocument = false;
            Title = newDocument.Name;
            
            _currentPageIndex = 0;
            await PaginateAsync(_fullText);
            UpdatePageDisplay();
            
            await DisplayAlert("Success", "Document saved!", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save new document: {ex.Message}", "OK");
        }
    }

    private async Task SaveCurrentPageAsync() // Save current page index to document
    {
        if (CurrentDocument == null || _suppressPageSave || _isNewDocument) return;
        
        try
        {
            // Avoid frequent disk writes if unchanged
            if (CurrentDocument.LastPageIndex != _currentPageIndex)
            {
                CurrentDocument.LastPageIndex = _currentPageIndex;
                await _db.SaveDocumentAsync(CurrentDocument);
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Saved current page: {_currentPageIndex + 1} for document: {CurrentDocument.Name}");
#endif
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Error saving current page: {ex.Message}");
#endif
        }
    }

    private void RestoreSavedPageIfAny() // Call before UpdatePageDisplay
    {
        if (CurrentDocument?.LastPageIndex is int idx &&
            idx >= 0 && idx < _pages.Count)
        {
            _suppressPageSave = true;
            _currentPageIndex = idx;
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Restored last page: {idx + 1} for document: {CurrentDocument.Name}");
#endif
            _suppressPageSave = false;
        }
    }

    private async void OnJumpToPageClicked(object sender, EventArgs e) // Prompt for page number
    {
        if (_pages.Count <= 1) return;
        var input = await DisplayPromptAsync("Jump To Page", $"Enter page number (1 - {_pages.Count})", "Go", "Cancel", keyboard: Keyboard.Numeric);
        if (int.TryParse(input, out int p) && p >= 1 && p <= _pages.Count)
        {
            _currentPageIndex = p - 1;
            UpdatePageDisplay();
        }
    }

    private void OnVoicePickerFocused(object sender, FocusEventArgs e) // Reset filter when focusing
    {
        if (string.IsNullOrWhiteSpace(pendingSearchText))
        {
            filteredLocales = systemLocales.OrderBy(l => l.Language + l.Name + l.Country).ToList();
            VoicePicker.ItemsSource = filteredLocales.Select(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();
        }
    }
    
    private void OnVoiceChanged(object sender, EventArgs e) // Save selected voice
    {
        if (VoicePicker.SelectedIndex >= 0)
        {
            selectedLocale = filteredLocales[VoicePicker.SelectedIndex];
            _ = SaveVoiceSelectionAsync();
        }
    }
    
    private async Task SaveVoiceSelectionAsync() // Save selected voice to document
    {
        if (CurrentDocument != null && selectedLocale != null)
        {
            CurrentDocument.VoiceLocale = $"{selectedLocale.Language}-{selectedLocale.Country}-{selectedLocale.Name}";
            await _db.SaveDocumentAsync(CurrentDocument);
        }
    }
    
    private void OnVoiceSearchTextChanged(object sender, TextChangedEventArgs e) => pendingSearchText = e.NewTextValue ?? string.Empty; // Update pending search text

    private void OnVoiceSearchButtonPressed(object sender, EventArgs e) // Apply search filter
    {
        var search = pendingSearchText.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(search))
            filteredLocales = systemLocales.OrderBy(l => l.Language + l.Name + l.Country).ToList();
        else
            filteredLocales = systemLocales.Where(l => l.Language.ToLowerInvariant().Contains(search) || l.Name.ToLowerInvariant().Contains(search) || l.Country.ToLowerInvariant().Contains(search)).OrderBy(l => l.Language + l.Name + l.Country).ToList();
        VoicePicker.ItemsSource = filteredLocales.Select(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();
    }

    private async void OnPlayClicked(object sender, EventArgs e) // Start or resume playback
    {
        if (_isPlaying) return;
        if (_isPaused)
        {
            await PlayFromAsync(_resumePageIndex, _resumePhraseIndex);
            return;
        }
        if (_userSelected)
        {
            await PlayFromAsync(_selectedStartPage, _selectedStartPhrase);
            return;
        }
        if (_completedDocument)
        {
            _lastPhrasePage = Math.Clamp(_lastPhrasePage, 0, _pages.Count - 1);
            var phraseList = _pagePhrases[_lastPhrasePage];
            if (phraseList.Count == 0) return;
            _lastPhraseIndex = Math.Clamp(_lastPhraseIndex, 0, phraseList.Count - 1);
            if (selectedLocale == null)
            {
                await DisplayAlert("Voice Required", "Select a voice first.", "OK");
                return;
            }
            ttsCancel?.Cancel();
            ttsCancel = new CancellationTokenSource();
            _isPlaying = true;
            try
            {
                _currentPageIndex = _lastPhrasePage;
                UpdatePageDisplay();
                HighlightPhrase(_lastPhraseIndex);
                ScrollToPhrase(_lastPhraseIndex);
                await TextToSpeech.Default.SpeakAsync(phraseList[_lastPhraseIndex].Text, new SpeechOptions { Locale = selectedLocale }, ttsCancel.Token);
                DimPhrase(_lastPhraseIndex);
            }
            catch (OperationCanceledException) { }
            finally { _isPlaying = false; }
            return;
        }
        await PlayFromAsync(_currentPageIndex, 0);
    }

    private void OnPauseClicked(object sender, EventArgs e) // Pause playback
    {
        if (!_isPlaying) return;
        _isPaused = true;
        _isRepeating = false;
        RepeatButton.BackgroundColor = Color.FromArgb("#3f8cff");
        ttsCancel?.Cancel();
    }

    private async void OnRepeatClicked(object sender, EventArgs e) // Toggle repeat mode
    {
        _isRepeating = !_isRepeating;
        
        RepeatButton.BackgroundColor = _isRepeating ? Colors.Green : Color.FromArgb("#3f8cff");
        
        if (_isRepeating && !_isPlaying)
        {
            OnPlayClicked(sender, e);
        }
        else if (!_isRepeating)
        {
            // Nothing to do here, playback continues until end or paused
        }
    }
}