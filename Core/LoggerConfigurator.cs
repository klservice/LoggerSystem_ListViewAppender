using log4net;

using LoggerSystem.Interfaces;
using LoggerSystem.Models;
//using SettingsAppenderType = LoggerSystem.Models.AppenderType;

namespace LoggerSystem.Core;

/// <summary>
/// Adapter: one log4net repository and one legacy factory for both old and new clients.
/// The WPF view subscribes to the actual legacy memory appender.
/// </summary>
public sealed class LoggerConfigurator
{
    #region Fields

    private readonly object _gate = new();

    private LoggerConfig _configuration;

    private ILogEntrySink? _logEntrySink;

    #endregion

    #region Properties

    /// <summary>
    /// Gibt eine unabhängige Kopie der aktuellen
    /// Logger-Konfiguration zurück.
    /// </summary>
    public LoggerConfig Configuration
    {
        get
        {
            lock (_gate)
            {
                return _configuration.Clone();
            }
        }
    }

    /// <summary>
    /// Gibt an, ob die LoggerFactory konfiguriert ist.
    /// </summary>
    public bool IsConfigured => LoggerFactory.IsConfigured;

    /// <summary>
    /// Gibt den Pfad der aktiven Logdatei zurück.
    /// </summary>
    public string LogFilePath => LoggerFactory.LogFilePath;

    #endregion

    #region Constructors

    /// <summary>
    /// Erstellt einen LoggerConfigurator mit
    /// der Standardkonfiguration.
    /// </summary>
    public LoggerConfigurator()
        : this(new LoggerConfig())
    {
    }

    /// <summary>
    /// Erstellt einen LoggerConfigurator mit
    /// einer angegebenen Konfiguration.
    /// </summary>
    /// <param name="configuration">
    /// Initiale Logger-Konfiguration.
    /// </param>
    public LoggerConfigurator(LoggerConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _configuration = configuration.Clone();

        _configuration.Validate();
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Initialisiert das LoggerSystem mit der
    /// gespeicherten Konfiguration.
    /// </summary>
    public void Configure()
    {
        Configure(Configuration);
    }

    /// <summary>
    /// Initialisiert oder aktualisiert das LoggerSystem
    /// mit einer neuen Konfiguration.
    /// </summary>
    /// <param name="configuration">
    /// Neue Logger-Konfiguration.
    /// </param>
    public void Configure(LoggerConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        LoggerConfig snapshot = configuration.Clone();
        snapshot.Validate();

        lock (_gate)
        {
            LoggerFactory.ConfigureLogger(snapshot);

            _configuration = snapshot;

            if (_logEntrySink is not null)
            {
                LoggerFactory.RegisterLogEntrySink(
                    _logEntrySink);
            }
        }
    }

    /// <summary>
    /// Setzt die Logger-Konfiguration auf
    /// die Standardwerte zurück.
    /// </summary>
    public void ResetToDefaults()
    {
        Configure(new LoggerConfig());
    }

    #endregion

    #region LogEntrySink

    /// <summary>
    /// Registriert einen Sink für die Weiterleitung
    /// von Logeinträgen an die Benutzeroberfläche.
    /// </summary>
    /// <param name="sink">
    /// Empfänger der Logeinträge.
    /// </param>
    public void RegisterLogEntrySink(ILogEntrySink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        lock (_gate)
        {
            LoggerFactory.RegisterLogEntrySink(sink);

            _logEntrySink = sink;
        }
    }

    /// <summary>
    /// Entfernt den aktuell registrierten LogEntrySink.
    /// </summary>
    public void UnregisterLogEntrySink()
    {
        lock (_gate)
        {
            if (_logEntrySink is null)
                return;

            LoggerFactory.UnregisterLogEntrySink(
                _logEntrySink);

            _logEntrySink = null;
        }
    }

    #endregion

    #region Logger Creation

    /// <summary>
    /// Erstellt einen Logger für den angegebenen Typ.
    /// </summary>
    /// <typeparam name="T">
    /// Typ, für den der Logger erstellt wird.
    /// </typeparam>
    /// <returns>ILog-Instanz.</returns>
    public ILog CreateLogger<T>()
    {
        EnsureConfigured();

        return LoggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// Erstellt einen Logger für einen Typ.
    /// </summary>
    public ILog CreateLogger(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        EnsureConfigured();

        return LoggerFactory.CreateLogger(type);
    }

    /// <summary>
    /// Erstellt einen Logger anhand eines Namens.
    /// </summary>
    public ILog CreateLogger(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        EnsureConfigured();

        return LoggerFactory.CreateLogger(name);
    }

    #endregion

    #region Log File Management

    /// <summary>
    /// Erstellt ein ZIP-Backup von Logdateien,
    /// die älter als die angegebene Anzahl Tage sind.
    /// </summary>
    /// <param name="days">
    /// Mindestalter der Logdateien in Tagen.
    /// </param>
    /// <param name="backupFolder">
    /// Optionales Zielverzeichnis für ZIP-Backups.
    /// </param>
    /// <returns>
    /// Pfad der ZIP-Datei oder null,
    /// wenn keine Dateien gefunden wurden.
    /// </returns>
    public string? BackupOldLogFiles(int days, string? backupFolder = null)
    {
        EnsureConfigured();

        return LoggerFactory.ZipLogFilesOlderThanDays(
            days,
            backupFolder);
    }

    /// <summary>
    /// Erstellt vor dem Löschen ein ZIP-Backup
    /// der betroffenen Logdateien.
    /// </summary>
    /// <returns>
    /// Anzahl der gelöschten Logdateien.
    /// </returns>
    public int DeleteOldLogFiles(int days = 30, string? backupFolder = null)
    {
        EnsureConfigured();

        return LoggerFactory.DeleteLogFilesOlderThanDays(
            days,
            backupFolder);
    }

    /// <summary>
    /// Verschiebt abgeschlossene Logdateien
    /// in ein angegebenes Zielverzeichnis.
    /// Die aktive Logdatei wird ausgeschlossen.
    /// </summary>
    /// <param name="destinationFolder">
    /// Zielverzeichnis.
    /// </param>
    /// <returns>
    /// Anzahl der verschobenen Dateien.
    /// </returns>
    public int MoveLogFiles(string destinationFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            destinationFolder);

        EnsureConfigured();

        return LoggerFactory.MoveLogFiles(
            destinationFolder);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Stellt sicher, dass die LoggerFactory
    /// initialisiert wurde.
    /// </summary>
    private void EnsureConfigured()
    {
        if (!LoggerFactory.IsConfigured)
        {
            Configure();
        }
    }

    #endregion

    #region Shutdown

    /// <summary>
    /// Entfernt den registrierten LogEntrySink
    /// und beendet das LoggerSystem.
    /// </summary>
    public void Shutdown()
    {
        lock (_gate)
        {
            UnregisterLogEntrySink();

            LoggerFactory.Shutdown();
        }
    }

    #endregion
}