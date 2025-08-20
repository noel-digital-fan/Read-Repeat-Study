using Read_Repeat_Study.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Read_Repeat_Study.Pages
{
    public partial class FlagsPage : ContentPage
    {
        readonly DatabaseService _db;
        readonly ObservableCollection<Flags> selectedFlags = new();

        public ObservableCollection<Flags> FlagsCollectionItems { get; } = new();
        public ICommand FlagLongPressedCommand { get; }
        public ICommand FlagTappedCommand { get; }

        bool MultiSelectMode => SelectionBar.IsVisible;
        bool ignoreNextTapAfterLongPress;

        public FlagsPage(DatabaseService db)
        {
            InitializeComponent();
            _db = db;

            FlagLongPressedCommand = new Command<int>(OnFlagLongPressed);
            FlagTappedCommand = new Command<int>(OnFlagTapped);

            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            FlagsCollectionItems.Clear();
            var flags = await _db.GetAllFlagsAsync();
            foreach (var f in flags)
            {
                f.IsSelected = false;
                FlagsCollectionItems.Add(f);
            }

            selectedFlags.Clear();
            SelectionBar.IsVisible = false;
            UpdateActionButtons();
        }

        // Long-press: enter multi-select and select item
        void OnFlagLongPressed(int flagId)
        {
            var flag = FlagsCollectionItems.FirstOrDefault(f => f.ID == flagId);
            if (flag == null) return;

            if (!MultiSelectMode)
            {
                SelectionBar.IsVisible = true;
                InputBlocker.IsVisible = true; // Show blocker behind
            }

            if (!flag.IsSelected)
            {
                flag.IsSelected = true;
                selectedFlags.Add(flag);
            }

            ignoreNextTapAfterLongPress = true;
            UpdateActionButtons();
        }


        // Tap: if in multi-select, toggle selection; else navigate
        void OnFlagTapped(int flagId)
        {
            var flag = FlagsCollectionItems.FirstOrDefault(f => f.ID == flagId);
            if (flag == null) return;

            if (MultiSelectMode)
            {
                if (ignoreNextTapAfterLongPress)
                {
                    ignoreNextTapAfterLongPress = false;
                    return;
                }

                flag.IsSelected = !flag.IsSelected;
                if (flag.IsSelected)
                    selectedFlags.Add(flag);
                else
                    selectedFlags.Remove(flag);

                if (!selectedFlags.Any())
                    HideSelectionBar();

                UpdateActionButtons();
            }
            else
            {
                // Single-tap ? edit
                _ = Shell.Current.GoToAsync($"AddEditFlagPage?flagId={flag.ID}");
            }
        }

        // Add Flag button
        async void OnAddFlagClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("AddEditFlagPage?flagId=0");

        // Edit selected (only one)
        void OnEditSelected(object sender, EventArgs e)
        {
            if (selectedFlags.Count == 1)
                _ = Shell.Current.GoToAsync($"AddEditFlagPage?flagId={selectedFlags[0].ID}");
        }

        // Delete selected (one or many)
        async void OnDeleteSelected(object sender, EventArgs e)
        {
            if (!selectedFlags.Any()) return;

            bool ok = await DisplayAlert(
                "Confirm Delete",
                $"Delete {selectedFlags.Count} flag(s)?",
                "Yes", "No");
            if (!ok) return;

            foreach (var f in selectedFlags.ToList())
            {
                await _db.DeleteFlagAsync(f);
                FlagsCollectionItems.Remove(f);
            }

            HideSelectionBar();
        }

        // Select all
        void OnSelectAll(object sender, EventArgs e)
        {
            foreach (var f in FlagsCollectionItems)
            {
                if (!f.IsSelected)
                {
                    f.IsSelected = true;
                    selectedFlags.Add(f);
                }
            }
            UpdateActionButtons();
        }

        // Cancel selection
        void OnCancelSelection(object sender, EventArgs e)
            => HideSelectionBar();

        // Helper to hide toolbar and clear selections
        void HideSelectionBar()
        {
            foreach (var f in selectedFlags)
                f.IsSelected = false;

            selectedFlags.Clear();
            SelectionBar.IsVisible = false;
            InputBlocker.IsVisible = false;
            UpdateActionButtons();
        }

        private void OnBlockerTapped(object sender, TappedEventArgs e)
        {
            HideSelectionBar();
            InputBlocker.IsVisible = false;
        }


        // Update button visibility in toolbar
        void UpdateActionButtons()
        {
            EditButton.IsVisible = selectedFlags.Count == 1;
            DeleteButton.IsVisible = selectedFlags.Count > 0;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            HideSelectionBar();
        }


    }
}
