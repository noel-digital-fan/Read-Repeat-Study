using System.ComponentModel;
using SQLite;

public class Flags : INotifyPropertyChanged // Represents a flag with ID, Name, Color, and selection state
{
    [PrimaryKey, AutoIncrement]
    public int ID { get; set; }

    public string Name { get; set; }
    public string Color { get; set; }

    private bool isSelected;

    [Ignore] 
    public bool IsSelected // Indicates whether the flag is selected
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

    public event PropertyChangedEventHandler PropertyChanged; // Event for property change notifications
}
