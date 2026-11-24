using LoggerSystem.ViewModels;

using System.Windows;
using System.Windows.Media;

namespace LoggerSystem.Models
{
    /// <summary>
    /// Stellt LogLevel-Icons ohne Verwendung von Convertern bereit.
    /// Verwendung auf dem WPF-UI-Thread.
    /// </summary>
    public sealed class LogLevelIconItem : ViewModelBase
    {

        #region Fields

        private string? _iconPath;

        private ImageSource? _icon;

        #endregion

        #region Properties

        public string Level { get; }

        public string DisplayName =>
            Level == "WARN" ? "WARNING" : Level;

        public string? IconPath
        {
            get => _iconPath;

            set
            {
                if (!SetProperty(ref _iconPath, value))
                    return;

                OnPropertyChanged(nameof(IsCustomIcon));

                OnPropertyChanged(nameof(IconDescription));

                OnPropertyChanged(nameof(CustomIconVisibility));

                OnPropertyChanged(nameof(DefaultIconVisibility));

                OnPropertyChanged(nameof(StatusBrush));

                OnPropertyChanged(nameof(CanResetIcon));
            }
        }

        public ImageSource? Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public bool IsCustomIcon =>
            !string.IsNullOrWhiteSpace(IconPath);

        public bool CanResetIcon => IsCustomIcon;

        public string IconDescription =>
            IsCustomIcon
                ? "Benutzerdefiniertes Icon"
                : "Standard-Icon";

        public Visibility CustomIconVisibility =>
            IsCustomIcon
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility DefaultIconVisibility =>
            IsCustomIcon
                ? Visibility.Collapsed
                : Visibility.Visible;

        public Brush StatusBrush =>
            IsCustomIcon
                ? Brushes.ForestGreen
                : Brushes.DimGray;

        #endregion

        #region Constructor

        /// <summary>
        /// Initialisiert die Icon-Zuordnung.
        /// </summary>
        public LogLevelIconItem(string level)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(level);

            Level = level.Trim().ToUpperInvariant();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Aktualisiert das Icon und den relativen Dateipfad.
        /// </summary>
        public void SetIcon(
            ImageSource icon,
            string? iconPath = null)
        {
            IconPath = iconPath;

            Icon = icon;
        }

        /// <summary>
        /// Stellt das Standard-Icon wieder her.
        /// </summary>
        public void ResetIcon(ImageSource defaultIcon)
        {
            SetIcon(defaultIcon);
        }

        #endregion

    }
}
