using LoggerSystem.Core;

using System.IO;
namespace LoggerSystem.Models;

public sealed class LoggerConfig
{
    #region Appender Settings

    /// <summary>
    /// Legt fest, welche Appender aktiviert werden.
    /// Mehrere Appender können kombiniert werden.
    /// </summary>
    public AppenderType AppenderTypes { get; set; } =
        AppenderType.ListView | AppenderType.RollingFile;

    /// <summary>
    /// Minimales LogLevel.
    /// </summary>
    public string MinimumLevel { get; set; } = "DEBUG";

    #endregion

    #region ListView Settings

    /// <summary>
    /// Maximale Anzahl von Logeinträgen in der ListView.
    /// </summary>
    public int MaximumListViewEntries { get; set; } = 2000;

    /// <summary>
    /// Aktiviert das automatische Scrollen
    /// zum neuesten Logeintrag.
    /// </summary>
    public bool AutoScroll { get; set; } = true;

    #endregion

    #region File Settings

    /// <summary>
    /// Vollständiger oder relativer Pfad der Logdatei.
    /// </summary>
    public string LogFilePath { get; set; } = "Logs/application.log";

    /// <summary>
    /// Dateiname der Logdatei.
    /// </summary>
    public string LogFileName { get; set; } = "application.log";

    /// <summary>
    /// Maximale Dateigröße in Bytes,
    /// bevor eine neue Logdatei erstellt wird.
    /// </summary>
    public long MaximumFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Maximale Anzahl an rotierenden Backup-Logdateien.
    /// </summary>
    public int MaximumBackupFiles { get; set; } = 10;

    /// <summary>
    /// Gibt an, ob neue Logeinträge an eine
    /// bestehende Logdatei angehängt werden.
    /// </summary>
    public bool AppendToFile { get; set; } = true;

    /// <summary>
    /// Definiert das Layout der Lognachrichten.
    /// </summary>
    public string LogMessageLayoutPattern { get; set; } = "%d{HH:mm:ss.ffff} [%level] %type %C:%L - %m%n";

    #endregion

    #region Cleanup Settings

    /// <summary>
    /// Gibt an, ob beim Start eine Bereinigung
    /// alter Logdateien durchgeführt wird.
    /// </summary>
    public bool CleanLogFolder { get; set; } = false;

    /// <summary>
    /// Maximales Alter der Logdateien in Tagen.
    /// </summary>
    public int MaxLogFileAge { get; set; } = 30;

    /// <summary>
    /// Aktiviert die automatische, regelmäßige
    /// Bereinigung alter Logdateien.
    /// </summary>
    public bool AutoCleanLogFolder { get; set; } = true;

    #endregion

    #region Constructors

    /// <summary>
    /// Erstellt eine Logger-Konfiguration
    /// mit Standardwerten.
    /// </summary>
    public LoggerConfig()
    {
    }

    #endregion

    #region Clone

    /// <summary>
    /// Erstellt eine unabhängige Kopie der
    /// Logger-Konfiguration.
    /// </summary>
    /// <returns>
    /// Eine neue LoggerConfig-Instanz
    /// mit identischen Einstellungen.
    /// </returns>
    public LoggerConfig Clone()
    {
        return new LoggerConfig
        {
            AppenderTypes = AppenderTypes,
            MinimumLevel = MinimumLevel,

            MaximumListViewEntries = MaximumListViewEntries,
            AutoScroll = AutoScroll,

            LogFilePath = LogFilePath,
            LogFileName = LogFileName,

            MaximumFileSizeBytes = MaximumFileSizeBytes,
            MaximumBackupFiles = MaximumBackupFiles,
            AppendToFile = AppendToFile,

            LogMessageLayoutPattern = LogMessageLayoutPattern,

            CleanLogFolder = CleanLogFolder,
            MaxLogFileAge = MaxLogFileAge,
            AutoCleanLogFolder = AutoCleanLogFolder
        };
    }

    #endregion

    #region Validation

    /// <summary>
    /// Prüft die Konfiguration auf gültige Werte.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Eine Einstellung ist ungültig.
    /// </exception>
    public void Validate()
    {
        // Appender
        const AppenderType supportedAppenders =
            AppenderType.ListView |
            AppenderType.RollingFile |
            AppenderType.Console;

        if (AppenderTypes == AppenderType.None ||
            (AppenderTypes & ~supportedAppenders) != 0)
        {
            throw new ArgumentException(
                "Invalid AppenderTypes.");
        }

        // LogLevel
        if (string.IsNullOrWhiteSpace(MinimumLevel))
        {
            throw new ArgumentException(
                "MinimumLevel cannot be empty.");
        }

        string level = MinimumLevel.Trim().ToUpperInvariant();

        if (level is not (
            "ALL" or
            "TRACE" or
            "DEBUG" or
            "INFO" or
            "WARN" or
            "WARNING" or
            "ERROR" or
            "FATAL" or
            "OFF"))
        {
            throw new ArgumentException(
                $"Unsupported MinimumLevel: {MinimumLevel}");
        }

        // ListView
        if (MaximumListViewEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumListViewEntries));
        }

        // Logdatei
        if (string.IsNullOrWhiteSpace(LogFilePath))
        {
            throw new ArgumentException(
                "LogFilePath cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(LogFileName))
        {
            throw new ArgumentException(
                "LogFileName cannot be empty.");
        }

        if (Path.GetFileName(LogFileName) != LogFileName ||
            LogFileName is "." or "..")
        {
            throw new ArgumentException(
                "LogFileName must be a filename without a directory.");
        }

        // Dateirotation
        if (MaximumFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumFileSizeBytes));
        }

        if (MaximumBackupFiles < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumBackupFiles));
        }

        // Cleanup
        if (MaxLogFileAge < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxLogFileAge));
        }

        // Layout
        if (string.IsNullOrWhiteSpace(LogMessageLayoutPattern))
        {
            throw new ArgumentException(
                "LogMessageLayoutPattern cannot be empty.");
        }
    }

    #endregion

}
