using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace BewerbungsAssiLogger;

internal static class LoggerFactoryHelpers
{
    internal static string CreateArchive(string folder, string logFileName, string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        var target = Path.GetFullPath(archivePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var stem = Path.GetFileNameWithoutExtension(logFileName);
        var ext = Path.GetExtension(logFileName);
        // Restrict the archive to this logger's current and numbered backup files.
        var files = Directory.EnumerateFiles(folder).Where(path =>
        {
            var name = Path.GetFileName(path);
            if (string.Equals(name, logFileName, StringComparison.OrdinalIgnoreCase)) return true;
            return name.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name.Substring(stem.Length + 1, name.Length - stem.Length - 1 - ext.Length), out _);
        }).ToArray();
        if (files.Any(f => string.Equals(Path.GetFullPath(f), target, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Archive must not replace a source log file.", nameof(archivePath));
        // CreateNew avoids overwriting an existing archive.
        using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (var file in files) zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
        return target;
    }
}
