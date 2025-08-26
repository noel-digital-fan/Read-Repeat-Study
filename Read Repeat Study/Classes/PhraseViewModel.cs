using System.ComponentModel;

namespace Read_Repeat_Study
{
    public class PhraseViewModel : INotifyPropertyChanged // ViewModel for a phrase with text, text color, and background color
    {
        private Color _textColor = Colors.Black;
        private Color _backgroundColor = Colors.Transparent;
        public string Text { get; set; } = string.Empty;
        public Color TextColor { get => _textColor; set { if (_textColor != value) { _textColor = value; OnPropertyChanged(); } } } // text color with notification
        public Color BackgroundColor { get => _backgroundColor; set { if (_backgroundColor != value) { _backgroundColor = value; OnPropertyChanged(); } } } // background color with notification
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); // notify property change
    }
}