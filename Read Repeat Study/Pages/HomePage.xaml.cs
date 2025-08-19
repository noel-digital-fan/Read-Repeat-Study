using Read_Repeat_Study.Models;
using Read_Repeat_Study.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Read_Repeat_Study.Pages
{
    public partial class HomePage : ContentPage
    {
        private readonly DatabaseService _db;
        public ObservableCollection<ImportedDocument> Documents { get; set; }
        public ICommand DocumentLongPressedCommand { get; private set; }
        // Flag to track if long press was triggered
        private bool _isLongPressTriggered;


        public HomePage(DatabaseService db)
        {
            InitializeComponent();

            _db = db;
            Documents = new ObservableCollection<ImportedDocument>();

            DocumentLongPressedCommand = new Command<ImportedDocument>(async document =>
            {
                _isLongPressTriggered = true;
                await ShowDocumentOptionsAsync(document);
            });


            BindingContext = this;
            DocumentsCollection.ItemsSource = Documents;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDocumentsAsync();
        }

        // Your existing methods: LoadDocumentsAsync, ImportMultipleFilesAsync, OnDocumentTapped, etc.

        // New/needed methods here:

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

            if (selectedFlagName == "Cancel" || string.IsNullOrEmpty(selectedFlagName))
                return null;

            return flags.FirstOrDefault(f => f.Name == selectedFlagName);
        }

        private void OnDocumentFrameLoaded(object sender, EventArgs e)
        {
            if (sender is Frame frame && frame.BindingContext is ImportedDocument document)
            {
                if (frame.Content is Grid grid)
                {
                    var flagColorBox = grid.Children.OfType<BoxView>().FirstOrDefault(b => b.StyleId == "FlagColorBox");
                    var flagNameLabel = grid.Children.OfType<VerticalStackLayout>().FirstOrDefault()
                        ?.Children.OfType<Label>().FirstOrDefault(l => l.StyleId == "FlagNameLabel");

                    if (flagColorBox != null)
                    {
                        if (document.Flag != null && !string.IsNullOrWhiteSpace(document.Flag.Color))
                        {
                            try
                            {
                                flagColorBox.BackgroundColor = Color.FromArgb(document.Flag.Color);
                                if (flagNameLabel != null)
                                {
                                    flagNameLabel.Text = $"??? {document.Flag.Name}";
                                    flagNameLabel.IsVisible = true;
                                    flagNameLabel.FontSize = 12; // smaller font for preview
                                }
                            }
                            catch
                            {
                                flagColorBox.BackgroundColor = Colors.Transparent;
                                if (flagNameLabel != null)
                                {
                                    flagNameLabel.IsVisible = false;
                                }
                            }
                        }
                        else
                        {
                            flagColorBox.BackgroundColor = Colors.Transparent;
                            if (flagNameLabel != null)
                            {
                                flagNameLabel.IsVisible = false;
                            }
                        }
                    }
                }
            }
        }

        private async Task ShowDocumentOptionsAsync(ImportedDocument document)
        {
            var action = await DisplayActionSheet("Document Options", "Cancel", "Delete",
                "Add/Change Flag", "Remove Flag");

            switch (action)
            {
                case "Add/Change Flag":
                    await AddOrChangeFlagAsync(document);
                    break;
                case "Remove Flag":
                    await RemoveFlagAsync(document);
                    break;
                case "Delete":
                    await DeleteDocumentAsync(document);
                    break;
            }
        }

        private void UpdateFlagColors()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var items = Documents.ToList();
                Documents.Clear();
                foreach (var item in items)
                {
                    Documents.Add(item);
                }
            });
        }

        private async Task AddOrChangeFlagAsync(ImportedDocument document)
        {
            var selectedFlag = await SelectFlagAsync();
            if (selectedFlag != null)
            {
                document.FlagId = selectedFlag.ID;
                document.Flag = selectedFlag;
                await _db.SaveDocumentAsync(document);
                UpdateFlagColors();
            }
        }

        private async Task RemoveFlagAsync(ImportedDocument document)
        {
            document.FlagId = null;
            document.Flag = null;
            await _db.SaveDocumentAsync(document);
            UpdateFlagColors();
        }

        private async Task DeleteDocumentAsync(ImportedDocument document)
        {
            var confirm = await DisplayAlert("Confirm Delete",
                $"Are you sure you want to delete '{document.Name}'?", "Yes", "No");
            if (confirm)
            {
                await _db.DeleteDocumentAsync(document);
                Documents.Remove(document);
            }
        }

        private async void OnImportFilesClicked(object sender, EventArgs e)
        {
            try
            {
                var fileResults = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select text files",
                    FileTypes = new FilePickerFileType(new System.Collections.Generic.Dictionary<DevicePlatform, System.Collections.Generic.IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "text/plain" } },
                { DevicePlatform.iOS, new[] { "public.plain-text" } },
                { DevicePlatform.WinUI, new[] { ".txt" } }
            })
                });

                if (fileResults != null && fileResults.Any())
                {
                    await ImportMultipleFilesAsync(fileResults);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to import files: {ex.Message}", "OK");
            }
        }

        private async Task ImportMultipleFilesAsync(IEnumerable<FileResult> fileResults)
        {
            var validFiles = fileResults.Where(f => System.IO.Path.GetExtension(f.FullPath).ToLowerInvariant() == ".txt").ToList();
            if (!validFiles.Any())
            {
                await DisplayAlert("No Valid Files", "No TXT files found in selection.", "OK");
                return;
            }

            var addFlag = await DisplayAlert("Add Flag",
                $"Would you like to add a flag to all {validFiles.Count} documents?", "Yes", "No");

            Flags selectedFlag = null;
            if (addFlag)
                selectedFlag = await SelectFlagAsync();

            int successCount = 0;
            foreach (var fileResult in validFiles)
            {
                try
                {
                    using var stream = await fileResult.OpenReadAsync();
                    using var reader = new System.IO.StreamReader(stream);
                    string content = await reader.ReadToEndAsync();

                    var document = new ImportedDocument
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(fileResult.FileName),
                        FilePath = fileResult.FullPath,
                        Content = content,
                        ImportedDate = DateTime.Now,
                        FlagId = selectedFlag?.ID,
                        Flag = selectedFlag
                    };

                    await _db.SaveDocumentAsync(document);
                    Documents.Insert(0, document);
                    successCount++;
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to import {fileResult.FileName}: {ex.Message}", "OK");
                }
            }
            await DisplayAlert("Import Complete",
                $"Successfully imported {successCount} of {validFiles.Count} files.", "OK");

            UpdateFlagColors();
        }

        private async void OnDocumentTapped(object sender, TappedEventArgs e)
        {
            if (_isLongPressTriggered)
            {
                _isLongPressTriggered = false; // Reset flag
                return; // Ignore tap if long press just happened
            }

            if (e.Parameter is ImportedDocument document)
            {
                ReaderPageHelper.DocumentContent = document.Content;
                ReaderPageHelper.DocumentName = document.Name;
                await Shell.Current.GoToAsync("ReaderPage");
            }
        }

        private async Task LoadDocumentsAsync()
        {
            try
            {
                var documents = await _db.GetAllDocumentsAsync();

                foreach (var doc in documents)
                {
                    if (doc.FlagId.HasValue)
                    {
                        doc.Flag = await _db.GetFlagAsync(doc.FlagId.Value);
                    }
                }

                Documents.Clear();

                foreach (var doc in documents.OrderByDescending(d => d.ImportedDate))
                {
                    Documents.Add(doc);
                }

                // Allow UI to render before updating flag colors
                await Task.Delay(100);

                UpdateFlagColors();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load documents: {ex.Message}", "OK");
            }
        }

    }
}
