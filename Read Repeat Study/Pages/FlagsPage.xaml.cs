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
        readonly DatabaseService _db; // Injected database service
        readonly ObservableCollection<Flags> selectedFlags = new(); // Currently selected flags

        public ObservableCollection<Flags> FlagsCollectionItems { get; } = new(); // All flags for display
        public ICommand FlagLongPressedCommand { get; } // Long-press command
        public ICommand FlagTappedCommand { get; } // Tap command

        bool MultiSelectMode => SelectionBar.IsVisible; // Are we in multi-select mode?
        bool ignoreNextTapAfterLongPress; // To avoid tap after long-press

        public FlagsPage(DatabaseService db) // Dependency Injection
        {
            InitializeComponent();
            _db = db;

            FlagLongPressedCommand = new Command<int>(OnFlagLongPressed);
            FlagTappedCommand = new Command<int>(OnFlagTapped);

            BindingContext = this;
        }

        protected override async void OnAppearing() // On page appear, load flags
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

        void OnFlagLongPressed(int flagId) // Long-press: enter multi-select and select
        {
            var flag = FlagsCollectionItems.FirstOrDefault(f => f.ID == flagId);
            if (flag == null) return;

            if (!MultiSelectMode)
            {
                SelectionBar.IsVisible = true;
                InputBlocker.IsVisible = true;
            }

            if (!flag.IsSelected)
            {
                flag.IsSelected = true;
                selectedFlags.Add(flag);
            }

            ignoreNextTapAfterLongPress = true;
            UpdateActionButtons();
        }


        void OnFlagTapped(int flagId) // Tap: select/deselect in multi-select, or edit in single-select
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
                _ = Shell.Current.GoToAsync($"AddEditFlagPage?flagId={flag.ID}");
            }
        }

        async void OnAddFlagClicked(object sender, EventArgs e) // Add new flag
            => await Shell.Current.GoToAsync("AddEditFlagPage?flagId=0");

        void OnEditSelected(object sender, EventArgs e) // Edit selected (only if one)
        {
            if (selectedFlags.Count == 1)
                _ = Shell.Current.GoToAsync($"AddEditFlagPage?flagId={selectedFlags[0].ID}");

            async void OnDeleteSelected(object sender, EventArgs e) // Delete selected (one or more)
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

            void OnSelectAll(object sender, EventArgs e) // Select all flags
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

            void OnCancelSelection(object sender, EventArgs e) // Cancel selection
                => HideSelectionBar();

            void HideSelectionBar() // Hide selection bar and clear selections
            {
                foreach (var f in selectedFlags)
                    f.IsSelected = false;

                selectedFlags.Clear();
                SelectionBar.IsVisible = false;
                InputBlocker.IsVisible = false;
                UpdateActionButtons();
            }
        }

        // Move HideSelectionBar and related methods out of OnEditSelected to be at the class level

        void OnEditSelection(object sender, EventArgs e) // Edit selected (only if one)
        {
            if (selectedFlags.Count == 1)
                _ = Shell.Current.GoToAsync($"AddEditFlagPage?flagId={selectedFlags[0].ID}");
        }

        async void OnDeleteSelected(object sender, EventArgs e) // Delete selected (one or more)
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

        void OnSelectAll(object sender, EventArgs e) // Select all flags
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

        void OnCancelSelection(object sender, EventArgs e) // Cancel selection
            => HideSelectionBar();

        private void OnBlockerTapped(object sender, TappedEventArgs e) // Tap on blocker also cancels selection
        {
            HideSelectionBar();
            InputBlocker.IsVisible = false;
        }

        void HideSelectionBar() // Hide selection bar and clear selections
        {
            foreach (var f in selectedFlags)
                f.IsSelected = false;

            selectedFlags.Clear();
            SelectionBar.IsVisible = false;
            InputBlocker.IsVisible = false;
            UpdateActionButtons();
        }

        void UpdateActionButtons() // Update action buttons based on selection
        {
            EditButton.IsVisible = selectedFlags.Count == 1;
            DeleteButton.IsVisible = selectedFlags.Count > 0;
        }

        protected override void OnDisappearing() // On page disappear, hide selection bar
        {
            base.OnDisappearing();
            HideSelectionBar();
        }


    }
}
