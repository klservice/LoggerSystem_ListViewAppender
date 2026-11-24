using System.IO;
using LoggerSystem.Models;

namespace LoggerSystem.Services;

/// <summary>Deletes only numbered rolling backups, never the active log file.</summary>
public sealed class LogFolderCleanupService
{
    public Task<int> CleanAsync(LoggerConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        return Task.Run(() =>
        {
            string path = Path.GetFullPath(config.LogFilePath);
            string folder = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(folder)) return 0;
            string fileName = Path.GetFileName(path);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            DateTime cutoff = DateTime.UtcNow.AddDays(-config.MaxLogFileAge);
            int deleted = 0;
            foreach (string candidate in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileName(candidate);
                // Supports both log4net naming schemes: application.log.1 and application.1.log.
                bool standard = name.StartsWith(fileName + ".", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(name[(fileName.Length + 1)..], out int n1) && n1 > 0;
                bool preserved = name.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                    && name.Length > stem.Length + ext.Length + 1
                    && int.TryParse(name.Substring(stem.Length + 1,
                        name.Length - stem.Length - ext.Length - 1), out int n2) && n2 > 0;
                if (!standard && !preserved) continue;
                try
                {
                    if (File.GetLastWriteTimeUtc(candidate) < cutoff)
                    {
                        File.Delete(candidate);
                        deleted++;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return deleted;
        }, cancellationToken);
    }
}
