using Read_Repeat_Study;
using SQLite;
using System.ComponentModel;

namespace Read_Repeat_Study
{
    public class ImportedDocument : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime ImportedDate { get; set; }
        public int? FlagId { get; set; }

        public string VoiceLocale { get; set; } = string.Empty;

        // Add LastPageIndex property to save the last page viewed
        public int? LastPageIndex { get; set; }

        [Ignore]  // Navigation property, ignored by SQLite
        public Flags? Flag { get; set; }

        private bool isSelected;

        [Ignore]
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
