using log4net;
using log4net.Core;

using LoggerSystem.Core;

namespace LoggerSystem.Integration;

/// <summary>Use this API from existing BewerbungsAssi code without replacing its log calls.</summary>
public static class LegacyLoggerBridge
{
    public static ILog GetLogger<T>() => Logger.GetLog<T>();
    public static ILog GetLogger(Type type) => Logger.GetLog(type);

    public static void LogToViewer(string message, Level? level = null) =>
        Logger.AddLogEntryToListView(message, level ?? Level.Info);

    public static IReadOnlyList<LoggingEvent> GetBufferedEvents() => Logger.GetLoggingEvents();

    public static string CurrentLogPath => LoggerFactory.LogFilePath;

    public static void SetCategory(string category) =>
        log4net.LogicalThreadContext.Properties["type"] = category;

    //public static LoggerConfig CreateSettingsFromLegacy(LoggerConfigurator legacy)
    //{
    //    ArgumentNullException.ThrowIfNull(legacy);
    //    var types = AppenderType.None;
    //    if (legacy.AppenderTypes.HasFlag(AppenderType.Memory))
    //        types |= AppenderType.ListView;
    //    if (legacy.AppenderTypes.HasFlag(AppenderType.RollingFile))
    //        types |= AppenderType.RollingFile;
    //    if (legacy.AppenderTypes.HasFlag(AppenderType.Console))
    //        types |= AppenderType.Console;
    //    var config = new LoggerConfig
    //    {
    //        AppenderTypes = types,
    //        MinimumLevel = legacy.MinimumLevel.Name,
    //        MaximumListViewEntries = legacy.MaxEntries,
    //        LogFileName = legacy.FileName,
    //        LogFilePath = Path.Combine(legacy.FolderName, legacy.FileName),
    //        LogMessageLayoutPattern = legacy.LayoutPattern,
    //        MaximumBackupFiles = legacy.AmountFilesBeforeBackup,
    //        AppendToFile = legacy.IsAppendToFile,
    //        MaximumFileSizeBytes = ParseSize(legacy.MaximumFileSize)
    //    };
    //    config.Validate();
    //    return config;
    //}

    private static long ParseSize(string size)
    {
        string text = size.Trim().ToUpperInvariant();
        long factor = 1;
        if (text.EndsWith("GB")) { factor = 1024L * 1024 * 1024; text = text[..^2]; }
        else if (text.EndsWith("MB")) { factor = 1024L * 1024; text = text[..^2]; }
        else if (text.EndsWith("KB")) { factor = 1024; text = text[..^2]; }
        else if (text.EndsWith('B')) text = text[..^1];
        return checked(long.Parse(text, System.Globalization.CultureInfo.InvariantCulture) * factor);
    }
}
