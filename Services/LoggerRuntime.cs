using LoggerSystem.Core;
using LoggerSystem.Interfaces;
using LoggerSystem.Models;
namespace LoggerSystem.Services;

public sealed class LoggerRuntime : IDisposable
{
    //readonly LoggerConfigurator _configurator = new();
    //readonly LogFolderCleanupService _cleanup = new();
    //readonly ILogEntrySink _sink;
    //readonly SemaphoreSlim _gate = new(1, 1);
    //Timer? _timer;
    //LoggerConfig _active = new();
    //bool _disposed;
    //public LoggerRuntime(ILogEntrySink sink) => _sink = sink;
    //public async Task ApplyAsync(LoggerConfig settings)
    //{
    //    var snapshot = settings.Clone(); snapshot.Validate(); await _gate.WaitAsync();
    //    try
    //    {
    //        ObjectDisposedException.ThrowIf(_disposed, this);
    //        _configurator.Configure(snapshot, _sink); _active = snapshot;
    //        _timer?.Dispose(); _timer = null;
    //        if (snapshot.CleanLogFolder || snapshot.AutoCleanLogFolder)
    //            await _cleanup.CleanAsync(snapshot);
    //        if (snapshot.AutoCleanLogFolder)
    //            _timer = new Timer(_ => _ = ScheduledCleanupAsync(), null, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
    //    }
    //    finally
    //    {
    //        _gate.Release();
    //    }
    //}
    //async Task ScheduledCleanupAsync()
    //{
    //    if (!await _gate.WaitAsync(0))
    //        return;
    //    try
    //    {
    //        if (!_disposed && _active.AutoCleanLogFolder)
    //            await _cleanup.CleanAsync(_active);
    //    }
    //    catch (Exception ex)
    //    {
    //        System.Diagnostics.Trace.WriteLine(ex);
    //    }
    //    finally
    //    {
    //        _gate.Release();
    //    }
    //}
    //public void Dispose()
    //{
    //    _disposed = true;
    //    _timer?.Dispose();
    //    _configurator.Dispose();
    //}

    #region Fields
    private readonly ILogEntrySink _sink;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Timer? _cleanupTimer;
    private LoggerConfig _activeConfig = new();
    private bool _disposed;

    #endregion

    #region Properties

    /// <summary>
    /// Gibt die aktuell angewendete Logger-Konfiguration zurück.
    /// </summary>
    public LoggerConfig ActiveConfiguration => _activeConfig.Clone();

    /// <summary>
    /// Gibt an, ob eine automatische Bereinigung aktiviert ist.
    /// </summary>
    public bool AutoCleanupEnabled => _activeConfig.AutoCleanLogFolder;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialisiert die Logger-Laufzeitverwaltung.
    /// </summary>
    /// <param name="sink">
    /// Empfänger für Logeinträge, beispielsweise LogViewModel.
    /// </param>
    public LoggerRuntime(ILogEntrySink sink)
    {
        _sink = sink ??
            throw new ArgumentNullException(nameof(sink));
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Übernimmt die Logger-Konfiguration, registriert
    /// den LogEntrySink und initialisiert die Bereinigung.
    /// </summary>
    public async Task ApplyAsync(LoggerConfig settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        LoggerConfig snapshot = settings.Clone();

        snapshot.Validate();

        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Stop previous cleanup timer.
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;

            // Configure log4net appenders.
            LoggerFactory.ConfigureLogger(settings);

            // Connect log4net to the MVVM LogViewModel.
            LoggerFactory.RegisterLogEntrySink(_sink);

            _activeConfig = snapshot;

            // Optional cleanup when applying settings.
            if (snapshot.CleanLogFolder)
            {
                await CleanupOldLogFilesCoreAsync(
                    snapshot.MaxLogFileAge)
                    .ConfigureAwait(false);
            }

            // Enable automatic cleanup.
            if (snapshot.AutoCleanLogFolder)
            {
                StartCleanupTimer();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Erstellt ein ZIP-Backup und löscht Logdateien,
    /// die älter als die konfigurierte Anzahl Tage sind.
    /// </summary>
    /// <returns>Anzahl gelöschter Logdateien.</returns>
    public async Task<int> CleanupOldLogFilesAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            return await CleanupOldLogFilesCoreAsync(
                _activeConfig.MaxLogFileAge)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Führt die eigentliche Bereinigung aus.
    /// Diese Methode wird bei bereits gehaltenem
    /// SemaphoreSlim aufgerufen.
    /// </summary>
    private static Task<int> CleanupOldLogFilesCoreAsync(
        int maxFileAge)
    {
        return Task.Run(() =>
            LoggerFactory.DeleteLogFilesOlderThanDays(days: maxFileAge));
    }

    /// <summary>
    /// Startet die tägliche automatische Bereinigung.
    /// </summary>
    private void StartCleanupTimer()
    {
        _cleanupTimer = new Timer(
            _ => _ = ScheduledCleanupAsync(),
            null,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(1));
    }

    /// <summary>
    /// Führt die automatische Bereinigung aus,
    /// sofern aktuell keine andere Operation läuft.
    /// </summary>
    private async Task ScheduledCleanupAsync()
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            if (_disposed ||
                !_activeConfig.AutoCleanLogFolder)
            {
                return;
            }

            await CleanupOldLogFilesCoreAsync(_activeConfig.MaxLogFileAge)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"Automatic log cleanup failed: {ex}");
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion

    #region Archive

    /// <summary>
    /// Erstellt ein ZIP-Backup aller Logdateien,
    /// die älter als die angegebene Anzahl Tage sind.
    /// </summary>
    public async Task<string?> BackupOldLogFilesAsync(int days, string? backupFolder = null)
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return await Task.Run(() =>
                LoggerFactory.ZipLogFilesOlderThanDays(
                    days,
                    backupFolder))
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion

    #region Move

    /// <summary>
    /// Verschiebt abgeschlossene Logdateien
    /// in ein anderes Verzeichnis.
    /// </summary>
    public async Task<int> MoveLogFilesAsync(string destinationFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            destinationFolder);

        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            return await Task.Run(() => LoggerFactory.MoveLogFiles(destinationFolder))
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion

    #region Dispose

    /// <summary>
    /// Beendet die automatische Bereinigung
    /// und entfernt den registrierten LogEntrySink.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _cleanupTimer?.Dispose();
        _cleanupTimer = null;

        LoggerFactory.UnregisterLogEntrySink(_sink);
    }

    #endregion
}
