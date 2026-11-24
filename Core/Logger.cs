using log4net;
using log4net.Core;

namespace LoggerSystem.Core;

/// <summary>Compatibility facade. Initialize once in App.OnStartup.</summary>
public sealed class Logger
{
    private static readonly Logger Instance = new();
    private Logger() { }
    public static Logger GetInstance() { Configure(); return Instance; }
    public static void Configure(LoggerConfigurator? options = null) => LoggerFactory.ConfigureLogger(options.Configuration);
    public static ILog GetLog(Type type) => LoggerFactory.CreateLogger(type);
    public static ILog GetLogManager(Type type) => GetLog(type);
    public static ILog GetLogViewManager(Type type) => GetLog(type);
    public static ILog GetLog<T>() => LoggerFactory.CreateLogger<T>();
    public static List<LoggingEvent> GetLoggingEvents() => new(LoggerFactory.LiveAppender.GetEvents());
    //public static string GetLogFileName() => LoggerFactory.Con .FileName;
    //   public static string getLogFolderPath() => LoggerFactory.Configuration.FolderName;

    /// <summary>
    /// Adds an event only to the shared live buffer used by log views.
    /// UI updates are asynchronous. This method does not write to log files.
    /// </summary>
    public static void AddLogEntryToListView(LoggingEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        LoggerFactory.LiveAppender.DoAppend(entry);
    }

    /// <summary>Creates a live-view event; safe to call from background threads.</summary>
    public static void AddLogEntryToListView(
        string message,
        Level? level = null,
        string loggerName = "Manual",
        Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(loggerName);

        var logger = LoggerFactory.CreateLogger(loggerName);
        var entry = new LoggingEvent(
            typeof(Logger),
            logger.Logger.Repository,
            loggerName,
            level ?? Level.Info,
            message,
            exception);

        AddLogEntryToListView(entry);
    }

    public static void Shutdown() => LoggerFactory.Shutdown();
}
