using log4net.Core;

namespace LoggerSystem.Models;

public sealed class LogEntryModel : LoggingEvent
{

    //   public static LoggingEventData _data;
    //    public Type _declaringType = typeof(LogEntryModel);
    //   public string LocationInfo { get; init; } = _data.LocationInfo.FullInfo;
    //public enum IMAGE_TYPE
    //{
    //    DEBUG = 0,
    //    ERROR = 1,
    //    FATAL = 2,
    //    INFO = 3,
    //    WARN = 4,
    //    CUSTOM = 5
    //}

    //public static Dictionary<IMAGE_TYPE, BitmapSource> _ImageList = new Dictionary<IMAGE_TYPE, BitmapSource>()
    //        {
    //            {IMAGE_TYPE.DEBUG, Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Error.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())},
    //            {IMAGE_TYPE.FATAL, Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Hand.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())},
    //            {IMAGE_TYPE.INFO, Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Information.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())},
    //            {IMAGE_TYPE.WARN, Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Warning.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())},
    //        };


    //public string Icon => GetIconFromLevelName(Level);

    //public string Icon => GetIconFromLevelName(Level);
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = "INFO";
    public string Logger { get; init; } = "";
    public string Thread { get; init; } = "";

    //  public string ClassName { get; set; } = _data.LocationInfo.ClassName;
    //   public string MethodeName { get; set; } = _data.LocationInfo.MethodName;
    //  public string LineNumber { get; set; } = _data.LocationInfo.LineNumber;


    public string Message { get; init; } = "";
    public string Exception { get; init; } = "";

    // - {ClassName}.{MethodeName}({LineNumber}
    public string FullMessage => $"{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Logger} - {Message}" + (string.IsNullOrEmpty(Exception) ? "" : Environment.NewLine + Exception);

    //public static ImageSource Icons(string level)
    //{
    //    IMAGE_TYPE type = level.ToUpperInvariant() switch
    //    {
    //        "ERRPR" => IMAGE_TYPE.ERROR,
    //        "WARN" => IMAGE_TYPE.WARN,
    //        "DEBUG" => IMAGE_TYPE.DEBUG,
    //        "FATAL" => IMAGE_TYPE.FATAL,
    //        _ => IMAGE_TYPE.CUSTOM
    //    };

    //    return new BitmapImage(new Uri(_ImageList[type].ToString()));
    //}


    public static string GetIconFromLevelName(string? levelName)
    {
        return levelName?.Trim().ToUpperInvariant() switch
        {
            "ALL" => "📋",
            "TRACE" => "🔎",
            "DEBUG" => "🐛",
            "INFO" => "ℹ",
            "NOTICE" => "📌",
            "WARN" or "WARNING" => "⚠",
            "ERROR" => "❌",
            "FATAL" or "CRITICAL"
                or "ALERT"
                or "EMERGENCY" => "🛑",
            "OFF" => "🔇",
            _ => "📝"
        };
    }
}
