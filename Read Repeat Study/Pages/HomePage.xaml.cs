using Read_Repeat_Study.Services;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using UglyToad.PdfPig;
using VersOne.Epub;

namespace Read_Repeat_Study.Pages
{
    public partial class HomePage : ContentPage
    {
        private readonly DatabaseService _db;
        private readonly ReportService _reportService;
        public ObservableCollection<ImportedDocument> Documents { get; }
        public ICommand DocumentLongPressedCommand { get; }
        private readonly ObservableCollection<ImportedDocument> selectedDocuments = new();

        private bool _selectionModeTriggeredByLongPress = false;

        // Cooldown tracking for theme toggle
        private DateTime _lastThemeToggle = DateTime.MinValue;
        private bool _isProcessingThemeToggle = false; // prevents recursion when reverting switch
        private bool _currentIsLight = true; // tracks accepted theme state

        public HomePage(DatabaseService db, ReportService reportService) // Dependency Injection
        {
            InitializeComponent();
            _db = db;
            _reportService = reportService;
            Documents = new ObservableCollection<ImportedDocument>();
            DocumentLongPressedCommand = new Command<ImportedDocument>(OnDocumentLongPressed);
            BindingContext = this;
            DocumentsCollection.ItemsSource = Documents;

            InitializeThemeSwitch();
        }

        private void InitializeThemeSwitch() // Set initial theme switch state
        {
            var currentTheme = Application.Current?.RequestedTheme ?? AppTheme.Light;
            _currentIsLight = currentTheme == AppTheme.Light;
            ThemeSwitch.IsToggled = _currentIsLight;
            DarkDayModeImageSwitch.Source = _currentIsLight ? "daymode.png" : "darkmode.png";
            UpdateThemeContainer(_currentIsLight);
        }

        protected override async void OnAppearing() // Load documents when page appears
        {
            base.OnAppearing();
            await LoadDocumentsAsync();
        }

        private void UpdateThemeContainer(bool isLightMode) // Update container colors based on theme
        {
            if (isLightMode)
            {
                ThemeToggleFrame.BackgroundColor = Color.FromArgb("#F3F4F6");
                ThemeToggleFrame.BorderColor = Color.FromArgb("#D1D5DB");
            }
            else
            {
                ThemeToggleFrame.BackgroundColor = Color.FromArgb("#1F2937");
                ThemeToggleFrame.BorderColor = Color.FromArgb("#374151");
            }
        }

        private void OnThemeToggled(object sender, ToggledEventArgs e) // Handle theme toggle with cooldown
        {
            if (_isProcessingThemeToggle) return;

            bool requestedIsLight = e.Value;
            var now = DateTime.UtcNow;

            if (now - _lastThemeToggle < TimeSpan.FromSeconds(2)) // 2-second cooldown
            {
                _isProcessingThemeToggle = true;
                ThemeSwitch.IsToggled = _currentIsLight;
                _isProcessingThemeToggle = false;
                return;
            }

            _lastThemeToggle = now;
            _currentIsLight = requestedIsLight;

            DarkDayModeImageSwitch.Source = requestedIsLight ? "daymode.png" : "darkmode.png";
            DarkDayModeImageSwitch.Opacity = 0.85;
            Application.Current.UserAppTheme = requestedIsLight ? AppTheme.Light : AppTheme.Dark;
            Preferences.Set("app_theme", Application.Current.UserAppTheme.ToString());
            UpdateThemeContainer(requestedIsLight);

            MainThread.BeginInvokeOnMainThread(async () => // Slight delay to ensure UI updates
            {
                await Task.Delay(100);
                await LoadDocumentsAsync();
                UpdateFlagColors();
            });
        }

        private async Task<string> ExtractTextFromPdfAsync(FileResult file) // Extract text from PDF using PdfPig
        {
            using var stream = await file.OpenReadAsync();
            using var pdf = PdfDocument.Open(stream);
            var textBuilder = new StringBuilder();
            foreach (UglyToad.PdfPig.Content.Page page in pdf.GetPages())
                textBuilder.AppendLine(page.Text);
            return textBuilder.ToString();
        }

        private string HtmlToPlainText(string html) // Convert HTML content to plain text
        {
            return Regex.Replace(html, "<.*?>", string.Empty)
                        .Replace("&nbsp;", " ")
                        .Replace("&amp;", "&")
                        .Replace("&lt;", "<")
                        .Replace("&gt;", ">");
        }

        // Show the action toolbar
        private void ShowActionButtons()
        {
            ActionButtonsContainer.IsVisible = true;
            InputBlocker.IsVisible = true;
            UpdateEditButtonVisibility();
        }

        // Hide and clear selection toolbar
        private void HideActionButtons()
        {
            ActionButtonsContainer.IsVisible = false;
            InputBlocker.IsVisible = false;
            UpdateEditButtonVisibility();
        }

        private void HideSelectionBar() // Clear selection and hide toolbar
        {
            foreach (var doc in selectedDocuments)
                doc.IsSelected = false;
            selectedDocuments.Clear();
            HideActionButtons();
            UpdateEditButtonVisibility();
        }


        private void OnBlockerTapped(object sender, EventArgs e) // Tap on blocker also cancels selection
        {
            HideSelectionBar();
            InputBlocker.IsVisible = false;
        }

        private void OnDocumentLongPressed(ImportedDocument doc) // Long-press to enter selection mode
        {
            if (!ActionButtonsContainer.IsVisible)
                ShowActionButtons();
            if (!doc.IsSelected)
            {
                doc.IsSelected = true;
                selectedDocuments.Add(doc);
            }
            _selectionModeTriggeredByLongPress = true;
            UpdateEditButtonVisibility();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) // Sync selection state
        {
            foreach (var doc in Documents)
                doc.IsSelected = false;
            foreach (var doc in e.CurrentSelection.Cast<ImportedDocument>())
                doc.IsSelected = true;
            selectedDocuments.Clear();
            foreach (var doc in e.CurrentSelection.Cast<ImportedDocument>())
                selectedDocuments.Add(doc);
            TakeActionsButton.IsVisible = selectedDocuments.Any();
            UpdateEditButtonVisibility();
        }

        private void OnDocumentTapped(object sender, EventArgs e) // Tap to open or select
        {
            var doc = (sender as VisualElement)?.BindingContext as ImportedDocument;
            if (doc == null) return;
            if (ActionButtonsContainer.IsVisible)
            {
                if (_selectionModeTriggeredByLongPress)
                {
                    _selectionModeTriggeredByLongPress = false;
                    return;
                }
                doc.IsSelected = !doc.IsSelected;
                if (doc.IsSelected && !selectedDocuments.Contains(doc))
                    selectedDocuments.Add(doc);
                else if (!doc.IsSelected && selectedDocuments.Contains(doc))
                    selectedDocuments.Remove(doc);
                if (!selectedDocuments.Any())
                    HideActionButtons();
                else
                    UpdateEditButtonVisibility();
            }
            else
            {
                Shell.Current.GoToAsync($"ReaderPage?docId={doc.ID}");
            }
        }

        private async void OnTakeActionsClicked(object sender, EventArgs e) // Show action sheet for bulk actions
        {
            var action = await DisplayActionSheet("Take Actions", "Cancel", null,
                "Add/Change Flag", "Remove Flag", "Delete");
            switch (action)
            {
                case "Add/Change Flag":
                    await BulkAddFlagAsync();
                    break;
                case "Remove Flag":
                    await BulkRemoveFlagAsync();
                    break;
                case "Delete":
                    await BulkDeleteAsync();
                    break;
            }
            OnCancelSelectionClicked(this, EventArgs.Empty); // Clear selection after action
        }

        private void OnCancelSelectionClicked(object sender, EventArgs e) // Cancel selection mode
        {
            foreach (var doc in selectedDocuments)
                doc.IsSelected = false;
            selectedDocuments.Clear();
            HideActionButtons();
            UpdateEditButtonVisibility();
        }

        private void OnSelectAllClicked(object sender, EventArgs e) // Select all documents
        {
            foreach (var doc in Documents)
            {
                if (!doc.IsSelected)
                {
                    doc.IsSelected = true;
                    selectedDocuments.Add(doc);
                }
            }
        }

        private async Task BulkAddFlagAsync() // Bulk add/change flag for selected documents
        {
            var flag = await SelectFlagAsync();
            if (flag == null) return;
            foreach (var doc in selectedDocuments)
            {
                doc.FlagId = flag.ID;
                doc.Flag = flag;
                await _db.SaveDocumentAsync(doc);
            }
            UpdateFlagColors();
        }

        private async Task BulkRemoveFlagAsync() // Bulk remove flag from selected documents
        {
            foreach (var doc in selectedDocuments)
            {
                doc.FlagId = null;
                doc.Flag = null;
                await _db.SaveDocumentAsync(doc);
            }
            UpdateFlagColors();
        }

        private async Task BulkDeleteAsync() // Bulk delete selected documents with confirmation
        {
            if (await DisplayAlert("Confirm Delete",
                $"Delete {selectedDocuments.Count} document(s)?", "Yes", "No"))
            {
                foreach (var doc in selectedDocuments.ToList())
                {
                    await _db.DeleteDocumentAsync(doc);
                    Documents.Remove(doc);
                }
            }
        }

        private void UpdateFlagColors() // Refresh flag colors in the UI
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var items = Documents.ToList();
                Documents.Clear();
                foreach (var i in items) Documents.Add(i);
            });

        }

        private async Task<Flags> SelectFlagAsync() // Prompt user to select a flag
        {
            var flags = await _db.GetAllFlagsAsync();
            if (!flags.Any() && await DisplayAlert("No Flags", "No flags exist. Create one?", "Yes", "No"))
            {
                await Shell.Current.GoToAsync("AddEditFlagPage?flagId=0");
                return null;
            }
            var names = flags.Select(f => f.Name).ToArray();
            var choice = await DisplayActionSheet("Select Flag", "Cancel", null, names);
            return flags.FirstOrDefault(f => f.Name == choice);
        }

        private async void OnImportFilesClicked(object sender, EventArgs e) // Import multiple files
        {
            try
            {
                var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            { DevicePlatform.Android, new[]
                                {
                                    "text/plain",
                                    "application/pdf",
                                    "application/msword",
                                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                                    "application/epub+zip",
                                }
                            }
                        })
                });

                if (results?.Any() == true)
                    await ImportMultipleFilesAsync(results);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task ImportMultipleFilesAsync(IEnumerable<FileResult> files) // Import and process selected files
        {
            var allowedExtensions = new[] { ".txt", ".docx", ".epub", ".pdf" };
            var supportedFiles = files
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f.FullPath).ToLower()))
                .ToList();

            if (!supportedFiles.Any())
            {
                await DisplayAlert("No Valid Files", "Please select supported files.", "OK");
                return;
            }

            bool wantsFlag = await DisplayAlert("Add Flag", $"Would you like to flag the {supportedFiles.Count} file(s)?", "Yes", "No");
            Flags? flag = null;

            if (wantsFlag)
                flag = await SelectFlagAsync();

            foreach (var file in supportedFiles)
            {
                string content = string.Empty;
                string ext = Path.GetExtension(file.FullPath).ToLower();

                try
                {
                    switch (ext)
                    {
                        case ".txt":
                            using (var s = await file.OpenReadAsync())
                            using (var reader = new StreamReader(s))
                                content = await reader.ReadToEndAsync();
                            break;

                        case ".pdf":
                            content = await ExtractTextFromPdfAsync(file);
                            break;
                        case ".docx":
                            using (var stream = await file.OpenReadAsync())
                            {
                                using var memoryStream = new MemoryStream();
                                await stream.CopyToAsync(memoryStream);
                                memoryStream.Position = 0;

                                var docx = Xceed.Words.NET.DocX.Load(memoryStream);
                                content = docx.Text;
                            }
                            break;

                        case ".epub":
                            var epubBook = await EpubReader.ReadBookAsync(file.FullPath);
                            var builder = new StringBuilder();

                            foreach (var chapter in epubBook.ReadingOrder)
                            {
                                string html = chapter.Content;
                                html = Regex.Replace(html, "<style[\\s\\S]*?>[\\s\\S]*?<\\/style>", string.Empty, RegexOptions.IgnoreCase); // 1. Remove <style> tags and their contents
                                html = Regex.Replace(html, "<(header|footer|nav)[\\s\\S]*?>[\\s\\S]*?<\\/style>", string.Empty, RegexOptions.IgnoreCase); // 2. Remove <header>, <footer>, and <nav> tags and their contents
                                string plainText = HtmlToPlainText(html); // 3. Convert remaining HTML to plain text
                                builder.AppendLine(plainText);
                            }
                            content = builder.ToString();
                            break;
                    }

                    var doc = new ImportedDocument
                    {
                        Name = Path.GetFileNameWithoutExtension(file.FileName),
                        FilePath = null, // Internal storage only
                        Content = content,
                        ImportedDate = DateTime.Now,
                        FlagId = flag?.ID,
                        Flag = flag
                    };

                    await _db.SaveDocumentAsync(doc);
                    Documents.Insert(0, doc);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Import Error", $"Failed to import {file.FileName}:\n{ex.Message}", "OK");
                }
            }

            UpdateFlagColors();
            await DisplayAlert("Import Complete", $"Imported {supportedFiles.Count} file(s).", "OK");
        }

        private async Task LoadDocumentsAsync() // Load documents from database
        {
            Documents.Clear();

            var docs = await _db.GetAllDocumentsAsync();
            foreach (var d in docs.OrderByDescending(d => d.ImportedDate))
            {
                if (d.FlagId.HasValue) d.Flag = await _db.GetFlagAsync(d.FlagId.Value);
                Documents.Add(d);
            }
            UpdateFlagColors();
        }

        private async void OnCreateDocumentClicked(object sender, EventArgs e) // Create new blank document
            => await Shell.Current.GoToAsync("ReaderPage?docId=-1");

        private void OnDocumentFrameLoaded(object sender, EventArgs e) // Set flag color box when frame loads
        {
            if (sender is Microsoft.Maui.Controls.Frame frame && frame.BindingContext is ImportedDocument document)
            {
                var box = frame.FindByName<BoxView>("FlagColorBox");
                if (box != null)
                {
                    if (document.Flag != null && !string.IsNullOrWhiteSpace(document.Flag.Color))
                    {
                        try
                        {
                            box.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb(document.Flag.Color);
                        }
                        catch
                        {
                            box.BackgroundColor = Microsoft.Maui.Graphics.Colors.Transparent;
                        }
                    }
                    else
                    {
                        box.BackgroundColor = Microsoft.Maui.Graphics.Colors.Transparent;
                    }
                }
            }
        }

        private async void OnBulkAddFlagClicked(object sender, EventArgs e) // Bulk add/change flag
        {
            if (!selectedDocuments.Any())
            {
                await DisplayAlert("No Selection", "Please select document(s) first.", "OK");
                return;
            }

            var flag = await SelectFlagAsync();
            if (flag == null)
                return;

            foreach (var doc in selectedDocuments)
            {
                doc.FlagId = flag.ID;
                doc.Flag = flag;
                await _db.SaveDocumentAsync(doc);
            }

            UpdateFlagColors();
        }

        private async void OnBulkDeleteClicked(object sender, EventArgs e) // Bulk delete with confirmation
        {
            if (!selectedDocuments.Any())
            {
                await DisplayAlert("No Selection", "Please select document(s) first.", "OK");
                return;
            }

            bool confirm = await DisplayAlert("Confirm Delete",
                $"Are you sure you want to delete {selectedDocuments.Count} document(s)?", "Yes", "No");

            if (!confirm)
                return;

            foreach (var doc in selectedDocuments.ToList())
            {
                await _db.DeleteDocumentAsync(doc);
                Documents.Remove(doc);
                selectedDocuments.Remove(doc);
            }

            TakeActionsButton.IsVisible = false;
        }

        private void OnEditDocumentClicked(object sender, EventArgs e)
        {
            var doc = selectedDocuments.FirstOrDefault();
            if (doc != null)
            {
                // Navigate with edit=true so ReaderPage starts in edit mode
                Shell.Current.GoToAsync($"ReaderPage?docId={doc.ID}&edit=true");
            }
            else
            {
                DisplayAlert("No Selection", "Please select a document to edit.", "OK");
            }
        }

        private void UpdateEditButtonVisibility()
        {
            EditDocumentButton.IsVisible = selectedDocuments.Count == 1;
        }

        private async void OnExportReportClicked(object sender, EventArgs e)
        {
            try
            {
                if (!Documents.Any())
                {
                    await DisplayAlert("No Data", "No documents to export.", "OK");
                    return;
                }

                var choice = await DisplayActionSheet("Export Format", "Cancel", null, "CSV Report", "Text Report");

                if (choice == "Cancel") return;

                string filePath;

                if (choice == "CSV Report")
                {
                    filePath = await _reportService.ExportToCsvAsync(Documents);
                    await DisplayAlert("Export Complete",
                        $"CSV report exported successfully!\nLocation: {filePath}", "OK");
                }
                else if (choice == "Text Report")
                {
                    filePath = await _reportService.ExportToTextAsync(Documents);
                    await DisplayAlert("Export Complete",
                        $"Text report exported successfully!\nLocation: {filePath}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Error", $"Failed to export report: {ex.Message}", "OK");
            }
        }

        protected override async void OnDisappearing() // On page disappear, cancel selection
        {
            base.OnDisappearing();
            // Cancel selection automatically when leaving the page
            OnCancelSelectionClicked(this, EventArgs.Empty);
        }

    }
}