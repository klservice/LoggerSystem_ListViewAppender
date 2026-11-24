using LoggerSystem.Commands;
using LoggerSystem.Interfaces;
using LoggerSystem.Models;
using LoggerSystem.Providers;

using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LoggerSystem.ViewModels
{
    /// <summary>
    /// Verwaltet die LogLevel-Icon-Einstellungen nach MVVM.
    /// </summary>
    public sealed class LogLevelIconSettingsViewModel : ViewModelBase
    {
        #region Fields

        private readonly IFileDialogService _fileDialogService;

        private readonly ILogLevelIconSettingsService _settingsService;

        private readonly LogLevelIconProvider _iconProvider;

        private bool _hasChanges;

        private string _statusMessage = string.Empty;

        private static readonly string[] SupportedLevels =
            {
            "TRACE",
            "DEBUG",
            "INFO",
            "WARN",
            "ERROR",
            "FATAL",
            "SUCCESS"
        };

        #endregion

        #region Properties

        public ObservableCollection<LogLevelIconItem> Icons
        { get; } = new();

        public string IconDirectory => _settingsService.IconDirectory;

        public string StatusMessage
        {
            get => _statusMessage;

            private set => SetProperty(ref _statusMessage, value);
        }

        public bool HasChanges
        {
            get => _hasChanges;

            private set
            {
                if (SetProperty(ref _hasChanges, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        #endregion

        #region Commands

        public ICommand SelectIconCommand { get; }

        public ICommand ResetIconCommand { get; }

        public ICommand ResetAllIconsCommand { get; }

        public ICommand SaveCommand { get; }

        public ICommand ReloadCommand { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initialisiert Services, Commands und Icon-Zuordnungen.
        /// </summary>
        public LogLevelIconSettingsViewModel(IFileDialogService fileDialogService, ILogLevelIconSettingsService settingsService, LogLevelIconProvider iconProvider)
        {
            _fileDialogService = fileDialogService ??
                throw new ArgumentNullException(
                    nameof(fileDialogService));

            _settingsService = settingsService ??
                throw new ArgumentNullException(
                    nameof(settingsService));

            _iconProvider = iconProvider ??
                throw new ArgumentNullException(
                    nameof(iconProvider));

            SelectIconCommand = new RelayCommand(
                SelectIcon,
                parameter => parameter is LogLevelIconItem);

            ResetIconCommand = new RelayCommand(
                ResetIcon,
                parameter =>
                    parameter is LogLevelIconItem item &&
                    item.CanResetIcon);

            ResetAllIconsCommand = new RelayCommand(
                _ => ResetAllIcons(),
                _ => Icons.Any(item => item.IsCustomIcon));

            SaveCommand = new RelayCommand(
                _ => Save(),
                _ => HasChanges);

            ReloadCommand = new RelayCommand(
                _ => Load());

            Load();
        }

        #endregion

        #region Load and Save

        /// <summary>
        /// Lädt alle gespeicherten Icon-Zuordnungen.
        /// </summary>
        public void Load()
        {
            try
            {
                ILogLevelIconSettingsService settings = _settingsService.Load();

                Icons.Clear();

                foreach (string level in SupportedLevels)
                {
                    settings.UseCustomIcons.TryGetValue(
                        level,
                        out string? relativePath);

                    string? fullPath =
                        _settingsService.ResolveIconPath(relativePath);

                    if (!_iconProvider.IsValidImage(fullPath ?? ""))
                        relativePath = null;

                    var item = new LogLevelIconItem(level);

                    item.SetIcon(
                        _iconProvider.GetPreviewIcon(
                            level,
                            relativePath),
                        relativePath);

                    Icons.Add(item);
                }

                HasChanges = false;

                StatusMessage =
                    "Icon-Einstellungen wurden geladen.";
            }
            catch (Exception ex)
            {
                StatusMessage =
                    $"Fehler beim Laden: {ex.Message}";
            }
        }

        /// <summary>
        /// Speichert alle Icon-Zuordnungen und aktiviert diese.
        /// </summary>
        public void Save()
        {
            try
            {
                var settings = new LogLevelIconSettings();

                foreach (LogLevelIconItem item in Icons)
                {
                    if (string.IsNullOrWhiteSpace(item.IconPath))
                        continue;

                    settings.Icons[item.Level] =
                        item.IconPath;
                }

                _settingsService.Save(settings);

                _iconProvider.ApplySettings(settings);

                HasChanges = false;

                StatusMessage =
                    "Icon-Einstellungen wurden gespeichert.";
            }
            catch (Exception ex)
            {
                StatusMessage =
                    $"Fehler beim Speichern: {ex.Message}";
            }
        }

        #endregion

        #region Icon Handling

        /// <summary>
        /// Öffnet die Dateiauswahl und importiert ein Icon.
        /// </summary>
        private void SelectIcon(object? parameter)
        {
            if (parameter is not LogLevelIconItem item)
                return;

            try
            {
                string? sourcePath =
                    _fileDialogService.SelectImageFile();

                if (string.IsNullOrWhiteSpace(sourcePath))
                    return;

                if (!_iconProvider.IsValidImage(sourcePath))
                {
                    StatusMessage = "Ungültige Bilddatei.";

                    return;
                }

                string relativePath =
                    _settingsService.ImportIcon(sourcePath);

                ImageSourceForItem(item, relativePath);

                HasChanges = true;

                CommandManager.InvalidateRequerySuggested();

                StatusMessage =
                    $"Icon für {item.DisplayName} ausgewählt.";
            }
            catch (Exception ex)
            {
                StatusMessage =
                    $"Fehler beim Importieren: {ex.Message}";
            }
        }

        /// <summary>
        /// Aktualisiert das Icon eines einzelnen Eintrags.
        /// </summary>
        private void ImageSourceForItem(
            LogLevelIconItem item,
            string? relativePath)
        {
            item.SetIcon(
                _iconProvider.GetPreviewIcon(
                    item.Level,
                    relativePath),
                relativePath);
        }

        /// <summary>
        /// Setzt einen LogLevel auf das Standard-Icon zurück.
        /// </summary>
        private void ResetIcon(object? parameter)
        {
            if (parameter is not LogLevelIconItem item)
                return;

            item.ResetIcon(
                _iconProvider.GetDefaultIcon(item.Level));

            HasChanges = true;

            CommandManager.InvalidateRequerySuggested();

            StatusMessage =
                $"{item.DisplayName}-Icon zurückgesetzt.";
        }

        /// <summary>
        /// Stellt alle Standard-Icons wieder her.
        /// </summary>
        public void ResetAllIcons()
        {
            foreach (LogLevelIconItem item in Icons)
            {
                item.ResetIcon(
                    _iconProvider.GetDefaultIcon(item.Level));
            }

            HasChanges = true;

            CommandManager.InvalidateRequerySuggested();

            StatusMessage =
                "Alle Standard-Icons wiederhergestellt.";
        }

        #endregion
    }
}
