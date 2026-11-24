using LoggerSystem.Models;

namespace LoggerSystem.Interfaces
{
    /// <summary>
    /// Definiert einen Service zur Verwaltung und
    /// persistenten Speicherung von LogLevel-Icons.
    /// </summary>
    public interface ILogLevelIconSettingsService
    {
        #region Properties

        /// <summary>
        /// Gibt den vollständigen Verzeichnispfad zurück,
        /// in dem benutzerdefinierte Icons gespeichert werden.
        /// </summary>
        string IconDirectory { get; }

        /// <summary>
        /// Gibt den vollständigen Dateipfad der
        /// Icon-Konfigurationsdatei zurück.
        /// </summary>
        string SettingsFilePath { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Lädt die gespeicherten LogLevel-Icon-Einstellungen.
        /// </summary>
        /// <returns>
        /// Die gespeicherten Einstellungen oder eine
        /// Standardkonfiguration.
        /// </returns>
        LogLevelIconSettings Load();

        /// <summary>
        /// Speichert die LogLevel-Icon-Einstellungen dauerhaft.
        /// </summary>
        /// <param name="settings">
        /// Die zu speichernden Icon-Einstellungen.
        /// </param>
        void Save(LogLevelIconSettings settings);

        /// <summary>
        /// Importiert eine Bilddatei in das anwendungseigene
        /// Icon-Verzeichnis.
        /// </summary>
        /// <param name="sourcePath">
        /// Vollständiger Pfad der zu importierenden Bilddatei.
        /// </param>
        /// <returns>
        /// Relativer Dateipfad des importierten Icons.
        /// </returns>
        string ImportIcon(string sourcePath);

        /// <summary>
        /// Ermittelt den vollständigen Dateipfad eines
        /// gespeicherten Icons anhand seines relativen Pfades.
        /// </summary>
        /// <param name="relativePath">
        /// Relativer Dateipfad innerhalb des Icon-Verzeichnisses.
        /// </param>
        /// <returns>
        /// Vollständiger Dateipfad oder null, wenn das Icon
        /// nicht vorhanden oder der Pfad ungültig ist.
        /// </returns>
        string? ResolveIconPath(string? relativePath);

        #endregion
    }
}
