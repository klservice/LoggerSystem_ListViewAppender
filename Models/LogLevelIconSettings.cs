using System.Text.Json.Serialization;

namespace LoggerSystem.Models
{
    /// <summary>
    /// Verwaltet die Zuordnung zwischen LogLevel
    /// und benutzerdefiniertem Icon-Dateipfad.
    /// </summary>
    public sealed class LogLevelIconSettings
    {
        #region Fields

        private Dictionary<string, string> _icons =
            new(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        /// <summary>
        /// Enthält die gespeicherten Icon-Zuordnungen.
        /// </summary>
        public Dictionary<string, string> Icons
        {
            get => _icons;

            set
            {
                var normalized = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

                if (value is not null)
                {
                    foreach (var entry in value)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Key) ||
                            string.IsNullOrWhiteSpace(entry.Value))
                        {
                            continue;
                        }

                        normalized[NormalizeLevel(entry.Key)] =
                            entry.Value;
                    }
                }

                _icons = normalized;
            }
        }

        /// <summary>
        /// Gibt die Anzahl der gespeicherten Icon-Zuordnungen zurück.
        /// </summary>
        [JsonIgnore]
        public int Count => _icons.Count;

        /// <summary>
        /// Gibt an, ob benutzerdefinierte Icons vorhanden sind.
        /// </summary>
        [JsonIgnore]
        public bool HasCustomIcons => _icons.Count > 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Initialisiert eine leere Icon-Konfiguration.
        /// </summary>
        public LogLevelIconSettings()
        {
        }

        /// <summary>
        /// Initialisiert die Konfiguration mit vorhandenen Icons.
        /// </summary>
        public LogLevelIconSettings(
            Dictionary<string, string> icons)
        {
            ArgumentNullException.ThrowIfNull(icons);

            Icons = icons;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Erstellt oder aktualisiert eine Icon-Zuordnung.
        /// </summary>
        /// <param name="level">Name des LogLevels.</param>
        /// <param name="iconPath">Relativer Icon-Dateipfad.</param>
        public void SetIcon(string level, string iconPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(level);
            ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);

            _icons[NormalizeLevel(level)] = iconPath;
        }

        /// <summary>
        /// Gibt den gespeicherten Icon-Dateipfad zurück.
        /// </summary>
        public string? GetIcon(string level)
        {
            return _icons.TryGetValue(
                NormalizeLevel(level),
                out string? iconPath)
                    ? iconPath
                    : null;
        }

        /// <summary>
        /// Versucht, den gespeicherten Icon-Dateipfad zu ermitteln.
        /// </summary>
        public bool TryGetIcon(
            string level,
            out string? iconPath)
        {
            return _icons.TryGetValue(
                NormalizeLevel(level),
                out iconPath);
        }

        /// <summary>
        /// Prüft, ob eine benutzerdefinierte Icon-Zuordnung existiert.
        /// </summary>
        public bool HasIcon(string level)
        {
            return TryGetIcon(level, out string? path) &&
                   !string.IsNullOrWhiteSpace(path);
        }

        /// <summary>
        /// Entfernt die Icon-Zuordnung eines LogLevels.
        /// </summary>
        public bool RemoveIcon(string level)
        {
            return _icons.Remove(NormalizeLevel(level));
        }

        /// <summary>
        /// Entfernt sämtliche Icon-Zuordnungen.
        /// </summary>
        public void Clear()
        {
            _icons.Clear();
        }

        /// <summary>
        /// Setzt einen LogLevel auf sein Standard-Icon zurück.
        /// </summary>
        public void ResetIcon(string level)
        {
            RemoveIcon(level);
        }

        /// <summary>
        /// Setzt alle LogLevel auf ihre Standard-Icons zurück.
        /// </summary>
        public void ResetAllIcons()
        {
            Clear();
        }

        /// <summary>
        /// Erstellt eine unabhängige Kopie aller Icon-Zuordnungen.
        /// </summary>
        public Dictionary<string, string> Export()
        {
            return new Dictionary<string, string>(
                _icons,
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Erstellt eine unabhängige Kopie der Konfiguration.
        /// </summary>
        public LogLevelIconSettings Clone()
        {
            return new LogLevelIconSettings(Export());
        }

        /// <summary>
        /// Erstellt eine leere Standardkonfiguration.
        /// </summary>
        public static LogLevelIconSettings CreateDefault()
        {
            return new LogLevelIconSettings();
        }

        /// <summary>
        /// Vereinheitlicht die Schreibweise eines LogLevels.
        /// </summary>
        public static string NormalizeLevel(string? level)
        {
            return level?.Trim().ToUpperInvariant() switch
            {
                null or "" => "INFO",

                "WARNING" => "WARN",

                var value => value
            };
        }

        #endregion
    }
}
