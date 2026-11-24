using System.IO;

namespace LoggerSystem.Core;

public class LoggerPath
{
    public static string LogFolderName { get; set; } = "Logs";
    public static string TempFolderName { get; set; } = "temp";
    public static string BackupFolderName { get; set; } = "backup";
    public static string SaveFolderName { get; set; } = "save";
    public static string GetStaticAppPath() => AppContext.BaseDirectory;
    public static string GetStaticPathOf(string folderName) => Path.GetFullPath(folderName, GetStaticAppPath());
    public static string GetStaticFolderPath(string folderName) => GetStaticPathOf(folderName);
    public static string GetStaticLogFolderPath() => GetStaticPathOf(LogFolderName);
    public static string GetStaticTempFolderPath() => GetStaticPathOf(TempFolderName);
    public static string GetStaticBackupFolderPath() => GetStaticPathOf(BackupFolderName);
    public static string GetStaticSaveFolderPath() => GetStaticPathOf(SaveFolderName);
    public static List<string> GetFolderList() => new() { LogFolderName, TempFolderName, BackupFolderName, SaveFolderName };
    public virtual string GetRecusiveFolderPath(string[] subFolderPath, bool returnFullFolderPath)
    {
        ArgumentNullException.ThrowIfNull(subFolderPath);
        var relative = Path.Combine(subFolderPath);
        return returnFullFolderPath ? GetStaticPathOf(relative) : relative;
    }
    public static string GetStaticFullPath(string staticAppPath, string folderName, string[] subfolder) =>
        Path.GetFullPath(Path.Combine(new[] { staticAppPath, folderName }.Concat(subfolder).ToArray()));
    public static void checkIfDirectoryExists(string folderPath) => Directory.CreateDirectory(GetStaticPathOf(folderPath));
    public static string GetFullPathOfFileName(string? fileName = null) =>
        fileName is null ? GetStaticAppPath() : GetStaticPathOf(fileName);
    public static string GetFullDirPathOfFoldername(string folderName = "") =>
        string.IsNullOrWhiteSpace(folderName) ? GetStaticAppPath() : GetStaticPathOf(folderName);

    /// <summary>Nonrecursive files ordered oldest first, using UTC last-write time.</summary>
    public static List<string> GetFilesOlderThan(string path, string pattern, TimeSpan minimumAge)
    {
        if (minimumAge < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(minimumAge));
        var folder = GetStaticPathOf(path);
        if (!Directory.Exists(folder)) return new List<string>();
        var cutoff = DateTime.UtcNow - minimumAge;
        return Directory.EnumerateFiles(folder, pattern).Where(f => File.GetLastWriteTimeUtc(f) < cutoff)
            .OrderBy(File.GetLastWriteTimeUtc).ToList();
    }
    public static List<string> GetOldestFileInPathFormCurrentDate(string path, string fileFilter, long lastModifiedTimeStamp) =>
        GetFilesOlderThan(path, fileFilter, TimeSpan.FromTicks(lastModifiedTimeStamp));
    public static string[] GetArrayOfFilesInPath(string foldername, long lastModifiedTimeStamp) =>
        GetFilesOlderThan(foldername, "*", TimeSpan.FromTicks(lastModifiedTimeStamp)).ToArray();
    /// <summary>Returns the oldest matching file; does not move or delete it.</summary>
    public static string? GetOldestFile(string path, string fileFilter, long lastModifiedTimeStamp) =>
        GetFilesOlderThan(path, fileFilter, TimeSpan.FromTicks(lastModifiedTimeStamp)).FirstOrDefault();
    public static bool BackupOldestFile(string backFilePath, string file)
    {
        string source = GetStaticPathOf(file);
        if (!File.Exists(source)) return false;
        string targetDirectory = GetStaticPathOf(backFilePath);
        Directory.CreateDirectory(targetDirectory);
        File.Copy(source, Path.Combine(targetDirectory, Path.GetFileName(source)), false);
        return true;
    }
    public static void DelOldestFile(string path, string fileFilter, long lastModifiedTimeStamp)
    {
        foreach (var file in GetFilesOlderThan(path, fileFilter, TimeSpan.FromTicks(lastModifiedTimeStamp)).Take(1)) File.Delete(file);
    }
    public static void MoveOldestFile(string path, string newFilePath, string fileFilter, long lastModifiedTimeStamp)
    {
        var target = GetStaticPathOf(newFilePath);
        Directory.CreateDirectory(target);
        foreach (var file in GetFilesOlderThan(path, fileFilter, TimeSpan.FromTicks(lastModifiedTimeStamp)).Take(1))
            File.Move(file, Path.Combine(target, Path.GetFileName(file)), false);
    }
    public static void CopyOldestFile(string path, string targetPath, string fileFilter, long lastModifiedTimeStamp)
    {
        var target = GetStaticPathOf(targetPath);
        Directory.CreateDirectory(target);
        foreach (var file in GetFilesOlderThan(path, fileFilter, TimeSpan.FromTicks(lastModifiedTimeStamp)).Take(1))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), false);
    }
}
