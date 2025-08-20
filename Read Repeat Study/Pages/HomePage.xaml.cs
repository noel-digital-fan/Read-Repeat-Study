using Read_Repeat_Study.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Read_Repeat_Study.Pages
{
    public partial class HomePage : ContentPage
    {
        private readonly DatabaseService _db;
        public ObservableCollection<ImportedDocument> Documents { get; }
        public ICommand DocumentLongPressedCommand { get; }
        private readonly ObservableCollection<ImportedDocument> selectedDocuments = new();

        private bool _selectionModeTriggeredByLongPress = false;

        public HomePage(DatabaseService db)
        {
            InitializeComponent();
            _db = db;
            Documents = new ObservableCollection<ImportedDocument>();
            DocumentLongPressedCommand = new Command<ImportedDocument>(OnDocumentLongPressed);
            BindingContext = this;
            DocumentsCollection.ItemsSource = Documents;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDocumentsAsync();
        }

        // Show the action toolbar
        void ShowActionButtons()
        {
            ActionButtonsContainer.IsVisible = true;
            InputBlocker.IsVisible = true;
            UpdateEditButtonVisibility();
        }

        // Hide and clear selection toolbar
        void HideActionButtons()
        {
            ActionButtonsContainer.IsVisible = false;
            InputBlocker.IsVisible = false;
            UpdateEditButtonVisibility();
        }

        void HideSelectionBar()
        {
            foreach (var doc in selectedDocuments)
                doc.IsSelected = false;

            selectedDocuments.Clear();
            HideActionButtons();
            UpdateEditButtonVisibility();
        }


        private void OnBlockerTapped(object sender, EventArgs e)
        {
            HideSelectionBar();        // Clear selection and hide toolbar
            InputBlocker.IsVisible = false; // Hide the input blocker overlay
        }




        void OnDocumentLongPressed(ImportedDocument doc)
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

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var doc in Documents)
            {
                doc.IsSelected = false;
            }
            foreach (var doc in e.CurrentSelection.Cast<ImportedDocument>())
            {
                doc.IsSelected = true;
            }

            selectedDocuments.Clear();
            foreach (var doc in e.CurrentSelection.Cast<ImportedDocument>())
            {
                selectedDocuments.Add(doc);
            }

            TakeActionsButton.IsVisible = selectedDocuments.Any();
            UpdateEditButtonVisibility();
        }

        void OnDocumentTapped(object sender, EventArgs e)
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
                    UpdateEditButtonVisibility(); // <-- Ensure visibility is updated after tap
            }
            else
            {
                Shell.Current.GoToAsync($"ReaderPage?docId={doc.ID}");
            }
        }

        // Take Actions button handler (bulk operations)
        async void OnTakeActionsClicked(object sender, EventArgs e)
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

            // After action, clear selection
            OnCancelSelectionClicked(this, EventArgs.Empty);
        }

        void OnCancelSelectionClicked(object sender, EventArgs e)
        {
            foreach (var doc in selectedDocuments)
                doc.IsSelected = false;

            selectedDocuments.Clear();
            HideActionButtons();
            UpdateEditButtonVisibility();
        }

        // Select All button handler
        void OnSelectAllClicked(object sender, EventArgs e)
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

        async Task BulkAddFlagAsync()
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

        async Task BulkRemoveFlagAsync()
        {
            foreach (var doc in selectedDocuments)
            {
                doc.FlagId = null;
                doc.Flag = null;
                await _db.SaveDocumentAsync(doc);
            }
            UpdateFlagColors();
        }

        async Task BulkDeleteAsync()
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

        void UpdateFlagColors()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var items = Documents.ToList();
                Documents.Clear();
                foreach (var i in items) Documents.Add(i);
            });
        }

        async Task<Flags> SelectFlagAsync()
        {
            var flags = await _db.GetAllFlagsAsync();
            if (!flags.Any() && await DisplayAlert("No Flags",
                "No flags exist. Create one?", "Yes", "No"))
            {
                await Shell.Current.GoToAsync("AddEditFlagPage?flagId=0");
                return null;
            }
            var names = flags.Select(f => f.Name).ToArray();
            var choice = await DisplayActionSheet("Select Flag", "Cancel", null, names);
            return flags.FirstOrDefault(f => f.Name == choice);
        }

        async void OnImportFilesClicked(object sender, EventArgs e)
        {
            try
            {
                var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "text/plain" } },
                        { DevicePlatform.iOS, new[] { "public.plain-text" } },
                        { DevicePlatform.WinUI, new[] { ".txt" } }
                    })
                });
                if (results?.Any() == true) await ImportMultipleFilesAsync(results);
            }
            catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        }

        async Task ImportMultipleFilesAsync(IEnumerable<FileResult> files)
        {
            var txts = files.Where(f => Path.GetExtension(f.FullPath).ToLower() == ".txt").ToList();
            if (!txts.Any()) { await DisplayAlert("No Valid Files", "Select TXT files.", "OK"); return; }
            if (await DisplayAlert("Add Flag", $"Flag all {txts.Count} files?", "Yes", "No"))
            {
                var flag = await SelectFlagAsync();
                foreach (var f in txts)
                {
                    using var s = await f.OpenReadAsync();
                    using var r = new StreamReader(s);
                    var doc = new ImportedDocument
                    {
                        Name = Path.GetFileNameWithoutExtension(f.FileName),
                        FilePath = f.FullPath,
                        Content = await r.ReadToEndAsync(),
                        ImportedDate = DateTime.Now,
                        FlagId = flag?.ID,
                        Flag = flag
                    };
                    await _db.SaveDocumentAsync(doc);
                    Documents.Insert(0, doc);
                }
                UpdateFlagColors();
                await DisplayAlert("Imported", $"Imported {txts.Count} files.", "OK");
            }
        }

        private async Task LoadDocumentsAsync()
        {
            Documents.Clear(); // Clear the collection before loading to avoid duplicates

            var docs = await _db.GetAllDocumentsAsync();
            foreach (var d in docs.OrderByDescending(d => d.ImportedDate))
            {
                if (d.FlagId.HasValue) d.Flag = await _db.GetFlagAsync(d.FlagId.Value);
                Documents.Add(d);
            }
            UpdateFlagColors();
        }

        async void OnCreateDocumentClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("ReaderPage?docId=-1");

        private void OnDocumentFrameLoaded(object sender, EventArgs e)
        {
            if (sender is Frame frame && frame.BindingContext is ImportedDocument document)
            {
                // Find the BoxView by name
                var box = frame.FindByName<BoxView>("FlagColorBox");
                if (box != null)
                {
                    if (document.Flag != null && !string.IsNullOrWhiteSpace(document.Flag.Color))
                    {
                        try
                        {
                            box.BackgroundColor = Color.FromArgb(document.Flag.Color);
                        }
                        catch
                        {
                            box.BackgroundColor = Colors.Transparent;
                        }
                    }
                    else
                    {
                        box.BackgroundColor = Colors.Transparent;
                    }
                }
            }
        }

        private async void OnBulkAddFlagClicked(object sender, EventArgs e)
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

        private async void OnBulkDeleteClicked(object sender, EventArgs e)
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

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            // Cancel selection automatically when leaving the page
            OnCancelSelectionClicked(this, EventArgs.Empty);
        }

        private void OnEditDocumentClicked(object sender, EventArgs e)
        {
            // Edit the first selected document, or show a message if none selected
            var doc = selectedDocuments.FirstOrDefault();
            if (doc != null)
            {
                Shell.Current.GoToAsync($"ReaderPage?docId={doc.ID}");
            }
            else
            {
                DisplayAlert("No Selection", "Please select a document to edit.", "OK");
            }
        }

        // Add this helper method:
        void UpdateEditButtonVisibility()
        {
            if (selectedDocuments.Count == 1)
            {
                EditDocumentButton.IsVisible = true;
            }
            else
            {
                EditDocumentButton.IsVisible = false;
            }
        }
    }
}