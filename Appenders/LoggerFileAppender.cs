using log4net.Appender;
using log4net.Core;
using log4net.Layout;

using LoggerSystem.Models;

using System.IO;
using System.Text;

using static log4net.Appender.RollingFileAppender;

namespace LoggerSystem.Appenders;

/// <summary>AppenderSkeleton facade owning a rolling file appender.</summary>
public sealed class LoggerFileAppender : AppenderSkeleton
{
    private readonly object _fileGate = new();
    private RollingFileAppender? _rolling;
    private bool _closed;

    public string File { get; set; } = "";
    public bool AppendToFile { get; set; } = true;

    public RollingMode RollingStyle { get; set; } = RollingMode.Size;
    public string MaximumFileSize { get; set; } = "5MB";
    public int MaxSizeRollBackups { get; set; } = 10;
    public bool StaticLogFileName { get; set; } = true;
    public bool PreserveLogFileNameExtension { get; set; } = true;
    public FileAppender.MinimalLock LockingModel { get; private set; }
    public Encoding Encoding { get; set; } = new UTF8Encoding(false);
    public bool ImmediateFlush { get; set; } = true;
    protected override bool RequiresLayout => true;

    public LoggerFileAppender()
         : this(new LoggerConfig())
    {
    }


    public LoggerFileAppender(LoggerConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.Validate();

        Configure(configuration);
    }

    /// <summary>
    /// Übernimmt die Einstellungen aus der LoggerConfig
    /// und initialisiert den Datei-Appender.
    /// </summary>
    /// <param name="configuration">
    /// Zu verwendende Logger-Konfiguration.
    /// </param>
    /// 
    //public void Configure(LoggerConfig configuration)
    //{
    //    ArgumentNullException.ThrowIfNull(configuration);

    //    configuration.Validate();

    //    string fullPath = Path.GetFullPath(
    //        configuration.LogFilePath);

    //    string? directory = Path.GetDirectoryName(fullPath);

    //    if (!string.IsNullOrWhiteSpace(directory))
    //    {
    //        Directory.CreateDirectory(directory);
    //    }

    //    Name = "LoggerFileAppender";

    //    File = fullPath;

    //    AppendToFile = configuration.AppendToFile;

    //    rollingMode = RollingFileAppender.RollingMode.Size;

    //    MaximumFileSize = configuration.MaximumFileSizeBytes.ToString(
    //        System.Globalization.CultureInfo.InvariantCulture);

    //    MaxSizeRollBackups = configuration.MaximumBackupFiles;

    //    StaticLogFileName = true;

    //    PreserveLogFileNameExtension = true;

    //    LockingModel = new FileAppender.MinimalLock();

    //    Threshold = GetLevel(configuration.MinimumLevel);

    //    Layout = new PatternLayout(
    //        configuration.LogMessageLayoutPattern.Replace(
    //            "%type",
    //            "%property{type}",
    //            StringComparison.Ordinal));

    //    ActivateOptions();
    //}


    /// <summary>
    /// Übernimmt die Einstellungen aus der LoggerConfig
    /// und initialisiert den Datei-Appender.
    /// </summary>
    /// <param name="configuration">
    /// Zu verwendende Logger-Konfiguration.
    /// </param>
    public void Configure(LoggerConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.Validate();

        string fullPath = Path.GetFullPath(
            configuration.LogFilePath);

        string? directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Name = "LoggerFileAppender";

        File = fullPath;

        AppendToFile = configuration.AppendToFile;

        RollingStyle = RollingFileAppender.RollingMode.Size;

        MaximumFileSize =
            configuration.MaximumFileSizeBytes.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        MaxSizeRollBackups =
            configuration.MaximumBackupFiles;

        StaticLogFileName = true;

        PreserveLogFileNameExtension = true;

        LockingModel = new FileAppender.MinimalLock();

        Threshold = GetLevel(configuration.MinimumLevel);

        Layout = new PatternLayout(
            configuration.LogMessageLayoutPattern.Replace(
                "%type",
                "%property{type}",
                StringComparison.Ordinal));

        ActivateOptions();
    }



    #region Helpers

    /// <summary>
    /// Konvertiert den LogLevel-Namen in
    /// einen log4net-Level.
    /// </summary>
    /// <param name="levelName">
    /// Name des LogLevels.
    /// </param>
    /// <returns>
    /// Entsprechender log4net-Level.
    /// </returns>
    private static Level GetLevel(string levelName)
    {
        return levelName.Trim().ToUpperInvariant() switch
        {
            "ALL" => Level.All,
            "TRACE" => Level.Debug,
            "DEBUG" => Level.Debug,
            "INFO" => Level.Info,
            "WARN" or "WARNING" => Level.Warn,
            "ERROR" => Level.Error,
            "FATAL" => Level.Fatal,
            "OFF" => Level.Off,

            _ => throw new ArgumentException(
                $"Unsupported log level: {levelName}",
                nameof(levelName))
        };
    }

    #endregion


    /// <summary>Apply property changes before logging starts.</summary>
    public override void ActivateOptions()
    {
        lock (_fileGate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(File);
            if (MaxSizeRollBackups < 0) throw new ArgumentOutOfRangeException(nameof(MaxSizeRollBackups));
            var maximumFileSize = MaximumFileSize;
            ArgumentException.ThrowIfNullOrWhiteSpace(maximumFileSize);
            if (!System.Text.RegularExpressions.Regex.IsMatch(maximumFileSize,
                @"^[1-9]\d*(B|KB|MB|GB)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new ArgumentException("Use a positive size with B, KB, MB or GB.", nameof(MaximumFileSize));
            ArgumentNullException.ThrowIfNull(Layout);
            ArgumentNullException.ThrowIfNull(Encoding);
            File = Path.GetFullPath(File);
            Directory.CreateDirectory(Path.GetDirectoryName(File)!);
            _rolling?.Close();
            _rolling = null;
            using (new FileStream(File, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) { }
            var candidate = new RollingFileAppender
            {
                Name = Name + ".Rolling",
                File = File,
                AppendToFile = AppendToFile,
                RollingStyle = RollingFileAppender.RollingMode.Size,
                MaximumFileSize = maximumFileSize,
                MaxSizeRollBackups = MaxSizeRollBackups,
                StaticLogFileName = StaticLogFileName,
                PreserveLogFileNameExtension = PreserveLogFileNameExtension,
                Encoding = Encoding,
                ImmediateFlush = ImmediateFlush,
                Layout = Layout,
                ErrorHandler = ErrorHandler
            };
            try
            {
                candidate.ActivateOptions();
                base.ActivateOptions();
                _rolling = candidate;
            }
            catch { candidate.Close(); throw; }
        }
    }

    protected override void Append(LoggingEvent loggingEvent)
    {
        lock (_fileGate)
        {
            if (_rolling is null)
            {
                ErrorHandler.Error("LoggerFileAppender has not been activated.");
                return;
            }
            _rolling.DoAppend(loggingEvent);
        }
    }

    protected override void OnClose()
    {
        lock (_fileGate)
        {
            _closed = true;
            _rolling?.Close();
            _rolling = null;
        }
        base.OnClose();
    }
}
