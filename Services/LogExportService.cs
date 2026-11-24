using LoggerSystem.Models;

using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace LoggerSystem.Services;

public sealed class LogExportService
{
    #region Fields

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #endregion

    #region Path Handling

    /// <summary>
    /// Prüft den Zielpfad und erstellt das Verzeichnis,
    /// sofern es noch nicht existiert.
    /// </summary>
    /// <param name="path">Zielpfad der Exportdatei.</param>
    /// <returns>Vollständiger Dateipfad.</returns>
    private static string PreparePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);

        string? directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return fullPath;
    }

    #endregion

    #region TXT Export

    /// <summary>
    /// Exportiert Logeinträge asynchron in eine TXT-Datei.
    /// </summary>
    /// <param name="path">Zielpfad der TXT-Datei.</param>
    /// <param name="entries">Zu exportierende Logeinträge.</param>
    /// <returns>Task des Exportvorgangs.</returns>
    public Task ExportTxtAsync(string path, IEnumerable<LogEntryModel> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        string filePath = PreparePath(path);

        var snapshot = entries.ToArray();

        return File.WriteAllLinesAsync(
            filePath,
            snapshot.Select(entry => entry.FullMessage),
            Utf8);
    }

    #endregion

    #region CSV Export

    /// <summary>
    /// Maskiert einen Wert für die sichere CSV-Ausgabe.
    /// </summary>
    /// <param name="value">Zu maskierender Wert.</param>
    /// <returns>Maskierter CSV-Wert.</returns>
    private static string Csv(string? value)
    {
        string text = value ?? string.Empty;

        string trimmed = text.TrimStart();

        if (trimmed.StartsWith('=') ||
            trimmed.StartsWith('+') ||
            trimmed.StartsWith('-') ||
            trimmed.StartsWith('@'))
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// Erstellt den vollständigen CSV-Inhalt.
    /// </summary>
    /// <param name="entries">Zu exportierende Logeinträge.</param>
    /// <returns>CSV-Inhalt als String.</returns>
    public static string BuildCsv(IEnumerable<LogEntryModel> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var builder = new StringBuilder();

        builder.AppendLine(
            "Timestamp;Level;Logger;Thread;Message;Exception");

        foreach (var entry in entries)
        {
            builder.AppendLine(string.Join(';', new[]
            {
                Csv(entry.Timestamp.ToString(
                    "O",
                    CultureInfo.InvariantCulture)),

                Csv(entry.Level),
                Csv(entry.Logger),
                Csv(entry.Thread),
                Csv(entry.Message),
                Csv(entry.Exception)
            }));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Exportiert Logeinträge asynchron in eine CSV-Datei.
    /// </summary>
    /// <param name="path">Zielpfad der CSV-Datei.</param>
    /// <param name="entries">Zu exportierende Logeinträge.</param>
    /// <returns>Task des Exportvorgangs.</returns>
    public Task ExportCsvAsync(string path, IEnumerable<LogEntryModel> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        string filePath = PreparePath(path);

        var snapshot = entries.ToArray();

        return File.WriteAllTextAsync(
            filePath,
            BuildCsv(snapshot),
            Utf8);
    }

    #endregion

    #region JSON Export

    /// <summary>
    /// Exportiert Logeinträge asynchron in eine JSON-Datei.
    /// </summary>
    /// <param name="path">Zielpfad der JSON-Datei.</param>
    /// <param name="entries">Zu exportierende Logeinträge.</param>
    /// <returns>Task des Exportvorgangs.</returns>
    public async Task ExportJsonAsync(string path, IEnumerable<LogEntryModel> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        string filePath = PreparePath(path);

        var snapshot = entries.ToArray();

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            snapshot,
            JsonOptions);
    }

    #endregion

    #region ZIP Export

    /// <summary>
    /// Exportiert Logeinträge als ZIP-Archiv.
    /// Das Archiv enthält eine TXT- und eine CSV-Datei.
    /// </summary>
    /// <param name="path">Zielpfad des ZIP-Archivs.</param>
    /// <param name="entries">Zu exportierende Logeinträge.</param>
    /// <returns>Task des Exportvorgangs.</returns>
    public async Task ExportZipAsync(string path, IEnumerable<LogEntryModel> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        string filePath = PreparePath(path);

        var snapshot = entries.ToArray();

        await using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        using var archive = new ZipArchive(
            fileStream,
            ZipArchiveMode.Create,
            leaveOpen: true);

        var files = new[]
        {
            (
                Name: "logs.txt",
                Content: string.Join(
                    Environment.NewLine,
                    snapshot.Select(entry => entry.FullMessage))
            ),
            (
                Name: "logs.csv",
                Content: BuildCsv(snapshot)
            )
        };

        foreach (var file in files)
        {
            var zipEntry = archive.CreateEntry(
                file.Name,
                CompressionLevel.Optimal);

            await using var entryStream = zipEntry.Open();

            await using var writer = new StreamWriter(
                entryStream,
                new UTF8Encoding(false));

            await writer.WriteAsync(file.Content);
        }
    }


    #endregion
}
