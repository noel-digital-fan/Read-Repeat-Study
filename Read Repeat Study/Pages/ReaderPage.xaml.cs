using Read_Repeat_Study.Services;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Read_Repeat_Study;

public class PhraseViewModel : INotifyPropertyChanged
{
    private Color _textColor = Colors.Black;
    private Color _backgroundColor = Colors.Transparent;
    public string Text { get; set; } = string.Empty;
    public Color TextColor { get => _textColor; set { if (_textColor != value) { _textColor = value; OnPropertyChanged(); } } }
    public Color BackgroundColor { get => _backgroundColor; set { if (_backgroundColor != value) { _backgroundColor = value; OnPropertyChanged(); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

[QueryProperty(nameof(DocumentId), "docId")]
[QueryProperty(nameof(StartInEdit), "edit")]
public partial class ReaderPage : ContentPage
{
    private readonly DatabaseService _db;
    private ImportedDocument? CurrentDocument;
    private int _documentId;
    private bool _isNewDocument;
    private readonly List<string> _pages = new();
    private readonly List<List<PhraseViewModel>> _pagePhrases = new();
    private int _currentPageIndex;
    private string _fullText = string.Empty;
    private bool _isPlaying;
    private bool _isPaused;
    private int _resumePageIndex;
    private int _resumePhraseIndex;
    private int _currentPhraseIndex;
    private CancellationTokenSource? ttsCancel;
    private bool _isEditing;
    private List<Locale> systemLocales = new();
    private List<Locale> filteredLocales = new();
    private Locale? selectedLocale;
    private string pendingSearchText = string.Empty;
    public bool StartInEdit { get; set; }
    public ObservableCollection<PhraseViewModel> CurrentPagePhrases { get; } = new();

    private bool _completedDocument; // end-of-document playback finished
    private int _lastPhrasePage;
    private int _lastPhraseIndex;
    private bool _userSelected; // user explicitly tapped a phrase
    private int _selectedStartPage;
    private int _selectedStartPhrase;

    private Task? _loadDocumentTask; // track async load
    private string? _pendingVoiceLocale; // voice locale to apply after locales load

    public int DocumentId
    {
        get => _documentId;
        set
        {
            _documentId = value;
            if (_documentId <= 0)
            {
                _isNewDocument = true;
                CurrentDocument = new ImportedDocument { Name = "Untitled", Content = string.Empty };
                _fullText = string.Empty;
                Title = "New Document";
                SetEditMode(true);
                _ = PaginateAsync(_fullText);
            }
            else
            {
                _isNewDocument = false;
                _loadDocumentTask = LoadDocumentAsync(_documentId); // start async load
            }
        }
    }

    public ReaderPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
        BindingContext = this;
    }

    private async Task LoadDocumentAsync(int id)
    {
        CurrentDocument = await _db.GetDocumentByIdAsync(id);
        if (CurrentDocument != null)
        {
            _fullText = CurrentDocument.Content ?? string.Empty;
            Title = CurrentDocument.Name;
            SetEditMode(false);
            await PaginateAsync(_fullText);
            _pendingVoiceLocale = CurrentDocument.VoiceLocale; // remember for later application
            if (StartInEdit) SetEditMode(true);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        systemLocales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();
        filteredLocales = systemLocales.OrderBy(l => l.Language + l.Name + l.Country).ToList();
        VoicePicker.ItemsSource = filteredLocales.Select(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();

        // Ensure document load completes (if started) before applying saved voice
        if (_loadDocumentTask != null)
        {
            try { await _loadDocumentTask; } catch { /* ignore load errors here */ }
        }

        // Apply saved voice if available
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

    private void ApplySavedVoiceLocale()
    {
        string? target = _pendingVoiceLocale ?? CurrentDocument?.VoiceLocale;
        if (string.IsNullOrWhiteSpace(target)) return;
        var idx = filteredLocales.FindIndex(l => $"{l.Language}-{l.Country}-{l.Name}" == target);
        if (idx >= 0)
        {
            VoicePicker.SelectedIndex = idx;
            selectedLocale = filteredLocales[idx];
            _pendingVoiceLocale = null; // applied
        }
    }

    private Task PaginateAsync(string text)
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
        UpdatePageDisplay();
        return Task.CompletedTask;
    }

    private List<PhraseViewModel> CreatePhrasesFromText(string text)
    {
        var list = new List<PhraseViewModel>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        var pattern = @"(?<=[.!?:])\s+|(?<=;)\s+|(?:\r?\n){1,}";
        foreach (var part in Regex.Split(text, pattern))
        {
            var t = part.Trim();
            if (!string.IsNullOrEmpty(t)) list.Add(new PhraseViewModel { Text = t });
        }
        if (list.Count == 0) list.Add(new PhraseViewModel { Text = text });
        return list;
    }

    private void UpdatePageDisplay()
    {
        if (_currentPageIndex < 0 || _currentPageIndex >= _pages.Count) return;
        CurrentPagePhrases.Clear();
        foreach (var p in _pagePhrases[_currentPageIndex]) CurrentPagePhrases.Add(p);
        if (!_isEditing) RenderPhrases();
        PageInfoLabel.Text = $"Page {_currentPageIndex + 1} of {_pages.Count}";
        PreviousPageButton.IsEnabled = _currentPageIndex > 0 && !_isPlaying;
        NextPageButton.IsEnabled = _currentPageIndex < _pages.Count - 1 && !_isPlaying;
        ScrollToTop();
    }

    private void RenderPhrases()
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
            int capture = i;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                // User explicitly selected a phrase; override any paused resume position
                _userSelected = true;
                _isPaused = false; // prevent PlayFromAsync from using resume indices
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

    private void ScrollToTop() => PhraseScrollView.ScrollToAsync(0, 0, false);
    private void ScrollToPhrase(int phraseIndex)
    {
        if (phraseIndex < 0 || phraseIndex >= PhraseStack.Children.Count) return;
        if (PhraseStack.Children[phraseIndex] is VisualElement ve)
            PhraseScrollView.ScrollToAsync(ve, ScrollToPosition.Center, true);
    }

    private async Task PlayFromAsync(int startPage, int startPhrase)
    {
        if (selectedLocale == null)
        {
            await DisplayAlert("Voice Required", "Select a voice first.", "OK");
            return;
        }
        if (_pages.Count == 0) return;
        // Only apply resume indices if paused AND user did not tap a new phrase
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
        _completedDocument = false; // starting new playback
        try
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
            // Successful completion reached end of document
            if (!_isPaused && !_userSelected)
            {
                _completedDocument = true;
                _lastPhrasePage = _currentPageIndex;
                _lastPhraseIndex = _currentPhraseIndex;
            }
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
            _completedDocument = false; // cancellation means no completion
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
            _userSelected = false; // consume user selection after run
        }
    }

    private void HighlightPhrase(int index)
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
                vm.TextColor = Colors.Black;
            }
        }
    }
    private void DimPhrase(int index)
    {
        if (index >= 0 && index < CurrentPagePhrases.Count)
        {
            var vm = CurrentPagePhrases[index];
            vm.BackgroundColor = Colors.Transparent;
            vm.TextColor = Colors.Gray;
        }
    }
    private void ResetHighlighting()
    {
        foreach (var vm in CurrentPagePhrases)
        {
            vm.BackgroundColor = Colors.Transparent;
            vm.TextColor = Colors.Black;
        }
    }

    private void OnPreviousPageClicked(object sender, EventArgs e)
    {
        if (_isPlaying) return;
        if (_currentPageIndex > 0)
        {
            _currentPageIndex--;
            UpdatePageDisplay();
        }
    }
    private void OnNextPageClicked(object sender, EventArgs e)
    {
        if (_isPlaying) return;
        if (_currentPageIndex < _pages.Count - 1)
        {
            _currentPageIndex++;
            UpdatePageDisplay();
        }
    }

    private void SetEditMode(bool editing)
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
    private void OnEditDocumentClicked(object sender, EventArgs e) => SetEditMode(true);
    private void OnSaveDocumentClicked(object sender, EventArgs e) => SetEditMode(false);

    private void OnVoicePickerFocused(object sender, FocusEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(pendingSearchText))
        {
            filteredLocales = systemLocales.OrderBy(l => l.Language + l.Name + l.Country).ToList();
            VoicePicker.ItemsSource = filteredLocales.Select(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();
        }
    }
    private void OnVoiceChanged(object sender, EventArgs e)
    {
        if (VoicePicker.SelectedIndex >= 0)
        {
            selectedLocale = filteredLocales[VoicePicker.SelectedIndex];
            _ = SaveVoiceSelectionAsync();
        }
    }
    private async Task SaveVoiceSelectionAsync()
    {
        if (CurrentDocument != null && selectedLocale != null)
        {
            CurrentDocument.VoiceLocale = $"{selectedLocale.Language}-{selectedLocale.Country}-{selectedLocale.Name}";
            await _db.SaveDocumentAsync(CurrentDocument);
        }
    }
    private void OnVoiceSearchTextChanged(object sender, TextChangedEventArgs e) => pendingSearchText = e.NewTextValue ?? string.Empty;
    private void OnVoiceSearchButtonPressed(object sender, EventArgs e)
    {
        var search = pendingSearchText.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(search))
            filteredLocales = systemLocales.OrderBy(l => l.Language + l.Name + l.Country).ToList();
        else
            filteredLocales = systemLocales.Where(l => l.Language.ToLowerInvariant().Contains(search) || l.Name.ToLowerInvariant().Contains(search) || l.Country.ToLowerInvariant().Contains(search)).OrderBy(l => l.Language + l.Name + l.Country).ToList();
        VoicePicker.ItemsSource = filteredLocales.Select(l => $"{l.Language} - {l.Name} ({l.Country})").ToList();
    }

    private async void OnPlayClicked(object sender, EventArgs e)
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

    private void OnPauseClicked(object sender, EventArgs e)
    {
        if (!_isPlaying) return;
        _isPaused = true;
        ttsCancel?.Cancel();
    }

    private void OnRepeatClicked(object sender, EventArgs e)
    {
        if (_completedDocument && !_isPlaying)
        {
            OnPlayClicked(sender, e);
        }
    }
}
