
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;
using log4net.Repository.Hierarchy;

using LoggerSystem.Appenders;
using LoggerSystem.Interfaces;
using LoggerSystem.Models;

using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;



namespace LoggerSystem.Core;

/// <summary>One explicitly owned repository; existing loggers remain valid after initialization.</summary>
public static class LoggerFactory
{
    private static readonly object Gate = new();
    private static readonly object DeliveryGate = new();

    internal static readonly ILoggerRepository Repository = LogManager.CreateRepository("LoggerSystem." + Guid.NewGuid().ToString("N"));

    private static LoggerConfig? _configuration;
    private static LoggerListViewAppender? _buffer;
    private static ILogEntrySink? _sink;
    private static EventHandler? _handler;
    private static LoggingEvent? _lastDelivered;
    private static bool _shutdown;


    #region Properties    
    /// <summary>
    /// Gibt an, ob der Logger konfiguriert ist.
    /// </summary>
    public static bool IsConfigured
    {
        get
        {
            lock (Gate)
                return _configuration is not null && !_shutdown;
        }
    }

    /// <summary>
    /// Gibt eine Kopie der aktuellen Logger-Konfiguration zurück.
    /// </summary>
    public static LoggerConfig Configuration
    {
        get
        {
            ConfigureLogger();

            lock (Gate)
                return _configuration!.Clone();
        }
    }

    /// <summary>
    /// Gibt den vollständigen Pfad der Logdatei zurück.
    /// </summary>
    public static string LogFilePath => Path.GetFullPath(Configuration.LogFilePath);

    /// <summary>
    /// Gibt den aktuellen ListView-Appender zurück.
    /// </summary>
    public static LoggerListViewAppender LiveAppender
    {
        get
        {
            ConfigureLogger();

            lock (Gate)
            {
                return _buffer ??
                    throw new InvalidOperationException(
                        "ListView appender is not enabled.");
            }
        }
    }


    #endregion

    #region Configuration

    /// <summary>
    /// Konfiguriert log4net und erstellt die aktivierten Appender.
    /// </summary>
    public static void ConfigureLogger(LoggerConfig? configuration = null)
    {
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(_shutdown, nameof(LoggerFactory));

            if (_configuration is not null &&
                configuration is null)
                return;

            LoggerConfig options = (configuration ?? new LoggerConfig());

            options.Validate();

            var appenders = new List<IAppender>();

            LoggerListViewAppender? buffer = null;

            Level level = ToLevel(options.MinimumLevel);

            try
            {
                var layout = new PatternLayout(
                    options.LogMessageLayoutPattern.Replace(
                        "%type",
                        "%property{type}",
                        StringComparison.Ordinal));

                layout.ActivateOptions();

                if (options.AppenderTypes.HasFlag(
                    AppenderType.RollingFile))
                {
                    var fileAppender = new LoggerFileAppender();

                    fileAppender.ActivateOptions();

                    appenders.Add(fileAppender);
                }

                if (options.AppenderTypes.HasFlag(
                    AppenderType.ListView))
                {
                    buffer = new LoggerListViewAppender
                    {
                        Name = "LiveEvents",
                        MaxEntries =
                            options.MaximumListViewEntries,
                        Threshold = level
                    };

                    buffer.ActivateOptions();

                    appenders.Add(buffer);
                }

                if (options.AppenderTypes.HasFlag(AppenderType.Console))
                {
                    var console = new ConsoleAppender
                    {
                        Name = "Console",
                        Layout = layout,
                        Threshold = level
                    };

                    console.ActivateOptions();

                    appenders.Add(console);
                }

                DetachSink();

                Repository.ResetConfiguration();

                Repository.Threshold = level;

                ((Hierarchy)Repository).Root.Level = level;

                BasicConfigurator.Configure(Repository, appenders.ToArray());

                _buffer = buffer;
                _configuration = Configuration;
                _lastDelivered = null;

                AttachSink();
            }
            catch
            {
                if (!ReferenceEquals(_buffer, buffer))
                {
                    foreach (IAppender appender in appenders)
                        appender.Close();
                }

                throw;
            }
        }
    }

    #endregion

    #region Logger Creation

    /// <summary>
    /// Erstellt einen Logger für einen generischen Typ.
    /// </summary>
    public static ILog CreateLogger<T>()
    {
        return CreateLogger(typeof(T));
    }

    /// <summary>
    /// Erstellt einen Logger für einen angegebenen Typ.
    /// </summary>
    public static ILog CreateLogger(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return CreateLogger(type.FullName ?? type.Name);
    }

    /// <summary>
    /// Erstellt einen Logger anhand eines Namens.
    /// </summary>
    public static ILog CreateLogger(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ConfigureLogger();

        return LogManager.GetLogger(Repository.Name, name);
    }

    /// <summary>
    /// Konvertiert einen LogLevel-Namen in log4net Level.
    /// </summary>
    public static Level ToLevel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return name.Trim().ToUpperInvariant() switch
        {
            "ALL" => Level.All,
            "DEBUG" => Level.Debug,
            "INFO" => Level.Info,
            "WARN" or "WARNING" => Level.Warn,
            "ERROR" => Level.Error,
            "FATAL" => Level.Fatal,
            "OFF" => Level.Off,

            _ => throw new ArgumentException(
                $"Unsupported log level: {name}",
                nameof(name))
        };
    }

    /// <summary>
    /// Setzt die Kategorie für den aktuellen
    /// logischen Ausführungskontext.
    /// </summary>
    public static void SetCategory(string category)
    {
        log4net.LogicalThreadContext.Properties["type"] = category;
    }


    #endregion






    #region LogEntrySink

    /// <summary>
    /// Registriert einen Empfänger für Logeinträge.
    /// </summary>
    public static void RegisterLogEntrySink(ILogEntrySink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        ConfigureLogger();

        lock (Gate)
        {
            if (ReferenceEquals(_sink, sink) && _handler is not null)
                return;

            DetachSink();

            _sink = sink;
            _lastDelivered = null;

            AttachSink();
        }
    }

    /// <summary>
    /// Entfernt einen registrierten LogEntrySink.
    /// </summary>
    public static void UnregisterLogEntrySink(ILogEntrySink sink)
    {
        lock (Gate)
        {
            if (!ReferenceEquals(_sink, sink))
                return;

            DetachSink();

            _sink = null;
            _lastDelivered = null;
        }
    }

    /// <summary>
    /// Verbindet den Appender mit dem Sink.
    /// </summary>
    private static void AttachSink()
    {
        if (_buffer is null || _sink is null)
            return;

        var buffer = _buffer;

        _handler = (_, _) => Deliver(buffer);

        buffer.EntriesChanged += _handler;

        Deliver(buffer);
    }

    /// <summary>
    /// Trennt die Verbindung zum Appender.
    /// </summary>
    private static void DetachSink()
    {
        if (_buffer is not null &&
            _handler is not null)
        {
            _buffer.EntriesChanged -= _handler;
        }

        _handler = null;
    }

    /// <summary>
    /// Konvertiert LoggingEvents in LogEntryModels
    /// und übergibt sie an den registrierten Sink.
    /// </summary>
    private static void Deliver(LoggerListViewAppender buffer)
    {
        lock (DeliveryGate)
        {
            if (!ReferenceEquals(buffer, _buffer) ||
                _sink is null)
                return;

            LoggingEvent[] events = buffer.GetEvents();

            int start = 0;

            if (_lastDelivered is not null)
            {
                int found = Array.FindLastIndex(
                    events,
                    entry => ReferenceEquals(
                        entry,
                        _lastDelivered));

                start = found < 0 ? 0 : found + 1;
            }

            for (int i = start; i < events.Length; i++)
            {
                LoggingEvent entry = events[i];

                var model = new LogEntryModel
                {
                    Timestamp = entry.TimeStamp,
                    Level = entry.Level?.Name ?? "INFO",
                    Logger = entry.LoggerName ?? "",
                    Thread = entry.ThreadName ?? "",
                    Message = entry.RenderedMessage ?? "",
                    Exception =
                        entry.GetExceptionString() ?? ""
                };

                _sink.AddEntry(model);

                _lastDelivered = entry;
            }
        }
    }

    #endregion

    #region Log File Selection

    /// <summary>
    /// Gibt alle Logdateien sortiert nach
    /// Änderungsdatum zurück.
    /// </summary>
    public static List<string> GetListOfOldestLogfiles()
    {
        lock (Gate)
        {
            LoggerConfig config = Configuration;

            string folder = GetLogFolder(config);

            return GetLogFiles(folder, config.LogFileName);
        }
    }

    /// <summary>
    /// Gibt abgeschlossene Logdateien zurück,
    /// die älter als die angegebene Anzahl Tage sind.
    /// </summary>
    public static IReadOnlyList<string> GetLogFilesOlderThanDays(int days)
    {
        if (days < 0)
            throw new ArgumentOutOfRangeException(
                nameof(days));

        lock (Gate)
        {
            LoggerConfig config = Configuration;

            return GetLogFilesOlderThanDays(
                    GetLogFolder(config),
                    config.LogFileName,
                    days)
                .Where(path =>
                    !IsActiveLogFile(path, config))
                .ToArray();
        }
    }

    /// <summary>
    /// Ermittelt Logdateien einschließlich nummerierter
    /// Sicherungsdateien, sortiert nach Änderungsdatum.
    ///
    /// Unterstützte Formate:
    /// application.log
    /// application.1.log
    /// application.log.1
    /// </summary>
    public static List<string> GetLogFiles(string folder, string logFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFileName);

        if (!Directory.Exists(folder))
            return new List<string>();

        string stem =
            Path.GetFileNameWithoutExtension(logFileName);

        string extension =
            Path.GetExtension(logFileName);

        return Directory.EnumerateFiles(folder)
            .Where(path =>
            {
                string name = Path.GetFileName(path);

                if (string.Equals(
                    name,
                    logFileName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // application.1.log
                if (name.StartsWith(
                    stem + ".",
                    StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(
                        extension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    string number = name.Substring(
                        stem.Length + 1,
                        name.Length - stem.Length -
                        extension.Length - 1);

                    if (int.TryParse(number, out _))
                        return true;
                }

                // application.log.1
                if (name.StartsWith(
                    logFileName + ".",
                    StringComparison.OrdinalIgnoreCase))
                {
                    string number =
                        name[(logFileName.Length + 1)..];

                    return int.TryParse(number, out _);
                }

                return false;
            })
            .OrderBy(File.GetLastWriteTimeUtc)
            .ToList();
    }


    /// <summary>
    /// Ermittelt Logdateien, die älter als
    /// die angegebene Anzahl Tage sind.
    /// </summary>
    public static List<string> GetLogFilesOlderThanDays(string folder, string logFileName, int days)
    {
        if (days < 0)
            throw new ArgumentOutOfRangeException(nameof(days));

        DateTime cutoff = DateTime.UtcNow.AddDays(-days);

        return GetLogFiles(folder, logFileName).Where(
            path => File.GetLastWriteTimeUtc(path) < cutoff)
            .ToList();
    }



    /// <summary>
    /// Gibt das konfigurierte Logverzeichnis zurück.
    /// </summary>
    private static string GetLogFolder(LoggerConfig config)
    {
        return Path.GetDirectoryName(Path.GetFullPath(config.LogFilePath))!;
    }

    /// <summary>
    /// Prüft, ob die Datei dem aktuellen
    /// konfigurierten Logdateipfad entspricht.
    /// </summary>
    private static bool IsActiveLogFile(string path, LoggerConfig config)
    {
        return string.Equals(
            Path.GetFullPath(path),
            Path.GetFullPath(config.LogFilePath),
            StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ZIP Backup

    /// <summary>
    /// Erstellt ein ZIP-Archiv aus einer Dateiliste.
    /// Bereits vorhandene Archive werden nicht überschrieben.
    /// </summary>
    public static string CreateArchive(IEnumerable<string> files, string archivePath)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        string target = Path.GetFullPath(archivePath);
        LoggerConfig config = Configuration;
        string[] sourceFiles = files.Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sourceFiles.Length == 0)
        {
            throw new InvalidOperationException(
                "No log files available for archiving.");
        }

        if (sourceFiles.Any(file =>
            string.Equals(
                file,
                target,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Archive must not replace a source log file.",
                nameof(archivePath));
        }

        if (sourceFiles
            .GroupBy(
                Path.GetFileName,
                StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Multiple source files have the same filename.",
                nameof(files));
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(target)!);

        using var output = new FileStream(
            target,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        using var archive = new ZipArchive(output, ZipArchiveMode.Create);

        foreach (string file in sourceFiles)
        {
            archive.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
        }

        return target;
    }

    /// <summary>
    /// Erstellt ein ZIP-Archiv aller gefundenen
    /// Logdateien eines Verzeichnisses.
    /// </summary>
    public static string CreateArchive(string folder, string logFileName, string archivePath)
    {
        return CreateArchive(GetLogFiles(folder, logFileName), archivePath);
    }


    /// <summary>
    /// Erstellt ein ZIP-Backup aller Logdateien.
    /// Der Logger muss vorher beendet worden sein.
    /// </summary>
    public static string ZipLogFilesArchive(string archivePath)
    {
        lock (Gate)
        {
            if (!_shutdown ||
                _configuration is null)
            {
                throw new InvalidOperationException(
                    "Call Shutdown before archiving.");
            }
            LoggerConfig config = Configuration;
            return CreateArchive(GetLogFolder(config),
                _configuration.LogFileName,
                archivePath);
        }
    }

    /// <summary>
    /// Erstellt ein ZIP-Archiv und überprüft anschließend
    /// sämtliche enthaltenen Dateien anhand ihrer Größe
    /// und ihres SHA-256-Hashwertes.
    /// </summary>
    /// <param name="files">
    /// Liste der zu archivierenden Quelldateien.
    /// </param>
    /// <param name="archivePath">
    /// Vollständiger Pfad des ZIP-Archivs.
    /// Die Zieldatei darf noch nicht existieren.
    /// </param>
    /// <returns>
    /// Vollständiger Pfad des verifizierten ZIP-Archivs.
    /// </returns>
    /// <exception cref="IOException">
    /// Das Archiv konnte nicht erstellt oder verifiziert werden.
    /// </exception>
    private static string CreateVerifiedArchive(IReadOnlyCollection<string> files, string archivePath)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        if (files.Count == 0)
        {
            throw new ArgumentException(
                "No files specified for archiving.",
                nameof(files));
        }

        string fullArchivePath = Path.GetFullPath(archivePath);

        string archiveDirectory =
            Path.GetDirectoryName(fullArchivePath)
            ?? throw new InvalidOperationException(
                "Invalid archive directory.");

        Directory.CreateDirectory(archiveDirectory);

        var sourceFiles = files
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sourceFiles.Any(path =>
            string.Equals(
                path,
                fullArchivePath,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new IOException(
                "The archive cannot contain itself.");
        }

        if (File.Exists(fullArchivePath))
        {
            throw new IOException(
                $"Archive already exists: {fullArchivePath}");
        }

        var sourceHashes =
            new Dictionary<string, byte[]>(
                StringComparer.Ordinal);

        // Eindeutige Eintragsnamen verwenden.
        var entryNames =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        bool archiveCreated = false;

        try
        {
            using (var stream = new FileStream(
                fullArchivePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                archiveCreated = true;

                using var archive = new ZipArchive(
                    stream,
                    ZipArchiveMode.Create,
                    leaveOpen: false);

                int index = 0;

                foreach (string file in sourceFiles)
                {
                    if (!File.Exists(file))
                    {
                        throw new FileNotFoundException(
                            "Source file not found.",
                            file);
                    }

                    string entryName =
                        $"{index:D4}_{Path.GetFileName(file)}";

                    index++;

                    var entry = archive.CreateEntry(
                        entryName,
                        CompressionLevel.Optimal);

                    using var sourceStream = new FileStream(
                        file,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);

                    using var entryStream = entry.Open();

                    using var sha256 = SHA256.Create();

                    byte[] buffer = new byte[81920];
                    int bytesRead;

                    while ((bytesRead = sourceStream.Read(
                        buffer, 0, buffer.Length)) > 0)
                    {
                        entryStream.Write(
                            buffer, 0, bytesRead);

                        sha256.TransformBlock(
                            buffer, 0, bytesRead,
                            null, 0);
                    }

                    sha256.TransformFinalBlock(
                        Array.Empty<byte>(), 0, 0);

                    sourceHashes[entryName] =
                        sha256.Hash
                        ?? throw new IOException(
                            "SHA-256 calculation failed.");

                    entryNames[entryName] = file;
                }
            }

            // Archiv erneut öffnen und Inhalte überprüfen.
            using (var archive = ZipFile.OpenRead(
                fullArchivePath))
            {
                if (archive.Entries.Count != sourceFiles.Length)
                {
                    throw new IOException(
                        "Archive entry count mismatch.");
                }

                foreach (var entry in archive.Entries)
                {
                    if (!sourceHashes.TryGetValue(
                        entry.FullName,
                        out byte[]? expectedHash))
                    {
                        throw new IOException(
                            $"Unexpected archive entry: {entry.FullName}");
                    }

                    using var entryStream = entry.Open();

                    byte[] actualHash =
                        SHA256.HashData(entryStream);

                    if (!CryptographicOperations.FixedTimeEquals(
                        expectedHash,
                        actualHash))
                    {
                        throw new IOException(
                            $"Archive verification failed: {entry.FullName}");
                    }
                }
            }

            return fullArchivePath;
        }
        catch
        {
            if (archiveCreated)
            {
                try
                {
                    File.Delete(fullArchivePath);
                }
                catch (IOException)
                {
                    // Ursprünglichen Fehler beibehalten.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ursprünglichen Fehler beibehalten.
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Erstellt ein ZIP-Backup von Logdateien,
    /// die älter als die angegebene Anzahl Tage sind.
    /// </summary>
    /// <returns>
    /// Archivpfad oder null, wenn keine Dateien
    /// gefunden wurden.
    /// </returns>
    public static string? ZipLogFilesOlderThanDays(int days, string? backupFolder = null)
    {
        lock (Gate)
        {
            LoggerConfig config = Configuration;

            var files = GetLogFilesOlderThanDays(days);

            if (files.Count == 0)
                return null;

            string folder = backupFolder ??
                Path.Combine(
                    GetLogFolder(config),
                    "Backup");

            Directory.CreateDirectory(folder);

            string archivePath = CreateUniqueArchivePath(folder, $"LogBackup_OlderThan_{days}Days");

            return CreateVerifiedArchive(files.ToArray(), archivePath);
        }
    }

    #endregion

    #region Delete Old Log Files

    /// <summary>
    /// Erstellt vor dem Löschen ein ZIP-Backup.
    /// Löscht anschließend abgeschlossene Logdateien,
    /// die älter als die angegebene Anzahl Tage sind.
    /// </summary>
    /// <param name="days">
    /// Maximales Alter in Tagen. Standard: 30.
    /// </param>
    /// <param name="backupFolder">
    /// Optionales Backupverzeichnis.
    /// </param>
    /// <returns>Anzahl gelöschter Dateien.</returns>
    public static int DeleteLogFilesOlderThanDays(int days = 30, string? backupFolder = null)
    {
        lock (Gate)
        {
            LoggerConfig config = Configuration;

            var files = GetLogFilesOlderThanDays(days);

            if (files.Count == 0)
                return 0;

            var snapshots = files
                .Select(path => new
                {
                    Path = path,
                    Length = new FileInfo(path).Length,
                    LastWriteTimeUtc =
                        File.GetLastWriteTimeUtc(path)
                })
                .ToArray();

            string folder = backupFolder ??
                Path.Combine(
                    GetLogFolder(config),
                    "Backup");

            Directory.CreateDirectory(folder);

            string archivePath = CreateUniqueArchivePath(folder, $"LogBackup_BeforeDelete_{days}Days");

            // No deletion if backup creation fails.
            CreateVerifiedArchive(
                files.ToArray(),
                archivePath);

            int deleted = 0;

            foreach (var snapshot in snapshots)
            {
                if (!File.Exists(snapshot.Path))
                    continue;

                var info = new FileInfo(snapshot.Path);

                // Skip files modified after archiving.
                if (info.Length != snapshot.Length ||
                    info.LastWriteTimeUtc !=
                        snapshot.LastWriteTimeUtc)
                {
                    continue;
                }

                File.Delete(snapshot.Path);

                deleted++;
            }

            return deleted;
        }
    }

    #endregion

    #region Move Log Files

    /// <summary>
    /// Verschiebt abgeschlossene Logdateien
    /// in ein angegebenes Zielverzeichnis.
    /// </summary>
    /// <returns>Anzahl verschobener Dateien.</returns>
    public static int MoveLogFiles(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        lock (Gate)
        {
            LoggerConfig config = Configuration;

            string sourceFolder = GetLogFolder(config);

            string destinationFolder =
                Path.GetFullPath(folderPath);

            if (string.Equals(Path.TrimEndingDirectorySeparator(sourceFolder),
                Path.TrimEndingDirectorySeparator(destinationFolder), StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            Directory.CreateDirectory(destinationFolder);

            var files = GetLogFiles(
                sourceFolder,
                config.LogFileName);

            int moved = 0;

            foreach (string file in files)
            {
                if (IsActiveLogFile(file, config))
                    continue;

                string destination = Path.Combine(
                    destinationFolder,
                    Path.GetFileName(file));

                if (File.Exists(destination))
                    continue;

                File.Move(file, destination);

                moved++;
            }

            return moved;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Erstellt einen eindeutigen ZIP-Dateipfad.
    /// </summary>
    private static string CreateUniqueArchivePath(string folder, string prefix)
    {
        string timestamp =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss_fff");

        return Path.Combine(
            folder,
            $"{prefix}_{timestamp}_{Guid.NewGuid():N}.zip");
    }

    #endregion




    #region Shutdown

    /// <summary>
    /// Beendet das LoggerSystem und gibt
    /// die registrierten Appender frei.
    /// </summary>
    public static void Shutdown()
    {
        lock (Gate)
        {
            if (_shutdown)
                return;

            DetachSink();

            _sink = null;

            Repository.Shutdown();

            _buffer = null;
            _shutdown = true;
        }
    }
    #endregion

}
