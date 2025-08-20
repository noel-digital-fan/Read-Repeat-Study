using Read_Repeat_Study;
using SQLite;
using System;
using System.ComponentModel;

public class ImportedDocument : INotifyPropertyChanged
{
    [PrimaryKey, AutoIncrement]
    public int ID { get; set; }

    public string Name { get; set; }
    public string FilePath { get; set; }
    public string Content { get; set; }
    public DateTime ImportedDate { get; set; }
    public int? FlagId { get; set; }

    public string VoiceLocale { get; set; }

    [Ignore]  // Navigation property, ignored by SQLite
    public Flags Flag { get; set; }

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

    public event PropertyChangedEventHandler PropertyChanged;
}
