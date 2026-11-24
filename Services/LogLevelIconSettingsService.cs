using LoggerSystem.Interfaces;
using LoggerSystem.Models;

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoggerSystem.Services
{
    /// <summary>
    /// Verwaltet die persistente Speicherung, den Import
    /// und die Auflösung benutzerdefinierter LogLevel-Icons.
    /// </summary>
    public sealed class LogLevelIconSettingsService : ILogLevelIconSettingsService
    {
        #region Constants

        private const string SettingsFileName = "LogLevelIconSettings.json";

        private const string IconFolderName = "Icons";

        private const long MaximumIconFileSize = 10 * 1024 * 1024;

        #endregion

        #region Fields

        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) {

            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
            ".gif",
            ".ico"
            };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            IgnoreReadOnlyProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region Properties

        /// <summary>
        /// Gibt das Basisverzeichnis der Icon-Verwaltung zurück.
        /// </summary>
        public string RootDirectory { get; }

        /// <summary>
        /// Gibt das Verzeichnis zurück, in dem die
        /// benutzerdefinierten Icons gespeichert werden.
        /// </summary>
        public string IconDirectory { get; }

        /// <summary>
        /// Gibt den vollständigen Dateipfad der
        /// JSON-Konfigurationsdatei zurück.
        /// </summary>
        public string SettingsFilePath { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initialisiert den Service mit dem
        /// standardmäßigen Anwendungsverzeichnis.
        /// </summary>
        public LogLevelIconSettingsService() : this(GetDefaultRootDirectory())
        {
        }

        /// <summary>
        /// Initialisiert den Service mit einem
        /// benutzerdefinierten Basisverzeichnis.
        /// </summary>
        /// <param name="rootDirectory">
        /// Basisverzeichnis für Einstellungen und Icons.
        /// </param>
        public LogLevelIconSettingsService(string rootDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                rootDirectory);

            RootDirectory = Path.GetFullPath(rootDirectory);

            IconDirectory = Path.Combine(
                RootDirectory,
                IconFolderName);

            SettingsFilePath = Path.Combine(
                RootDirectory,
                SettingsFileName);

            EnsureDirectories();
        }

        #endregion

        #region Load and Save

        /// <summary>
        /// Lädt die gespeicherten LogLevel-Icon-Einstellungen.
        /// Existiert keine Konfigurationsdatei, wird eine
        /// leere Standardkonfiguration zurückgegeben.
        /// </summary>
        /// <returns>
        /// Geladene LogLevel-Icon-Einstellungen.
        /// </returns>
        public LogLevelIconSettings Load()
        {
            EnsureDirectories();

            if (!File.Exists(SettingsFilePath))
            {
                return LogLevelIconSettings.CreateDefault();
            }

            using var stream = new FileStream(
                SettingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete);

            LogLevelIconSettings? settings =
                JsonSerializer.Deserialize<LogLevelIconSettings>(
                    stream,
                    JsonOptions);

            if (settings is null)
            {
                throw new InvalidDataException(
                    "Die Icon-Konfigurationsdatei enthält " +
                    "keine gültigen Einstellungen.");
            }

            return NormalizeSettings(settings);
        }

        /// <summary>
        /// Speichert die LogLevel-Icon-Einstellungen
        /// dauerhaft als JSON-Datei.
        /// </summary>
        /// <remarks>
        /// Die Einstellungen werden zunächst in eine
        /// temporäre Datei geschrieben. Anschließend wird
        /// die Zieldatei durch diese Datei ersetzt.
        /// </remarks>
        /// <param name="settings">
        /// Die zu speichernden Icon-Einstellungen.
        /// </param>
        public void Save(LogLevelIconSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            EnsureDirectories();

            LogLevelIconSettings snapshot =
                NormalizeSettings(settings);

            string temporaryFilePath =
                CreateTemporaryFilePath(RootDirectory);

            try
            {
                using (var stream = new FileStream(
                    temporaryFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    JsonSerializer.Serialize(
                        stream,
                        snapshot,
                        JsonOptions);

                    stream.Flush(flushToDisk: true);
                }

                File.Move(
                    temporaryFilePath,
                    SettingsFilePath,
                    overwrite: true);
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryFilePath);
            }
        }

        /// <summary>
        /// Prüft, ob eine gespeicherte
        /// Icon-Konfigurationsdatei vorhanden ist.
        /// </summary>
        public bool SettingsFileExists()
        {
            return File.Exists(SettingsFilePath);
        }

        /// <summary>
        /// Speichert eine leere Standardkonfiguration.
        /// Bereits importierte Bilddateien bleiben erhalten.
        /// </summary>
        public LogLevelIconSettings ResetToDefaults()
        {
            LogLevelIconSettings settings = LogLevelIconSettings.CreateDefault();

            Save(settings);

            return settings;
        }

        #endregion

        #region Icon Import

        /// <summary>
        /// Importiert eine Bilddatei in das
        /// anwendungseigene Icon-Verzeichnis.
        /// </summary>
        /// <remarks>
        /// Der Dateiname wird aus dem SHA256-Hash des
        /// Dateiinhalts und der Dateiendung gebildet.
        /// Identische Dateien mit gleicher Dateiendung
        /// werden dadurch nicht mehrfach gespeichert.
        /// </remarks>
        /// <param name="sourcePath">
        /// Vollständiger Pfad der zu importierenden Bilddatei.
        /// </param>
        /// <returns>
        /// Relativer Dateiname des importierten Icons.
        /// </returns>
        public string ImportIcon(string sourcePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                sourcePath);

            EnsureDirectories();

            string fullSourcePath = Path.GetFullPath(sourcePath);

            ValidateSourceFile(fullSourcePath);

            string extension = Path.GetExtension(fullSourcePath).ToLowerInvariant();

            byte[] fileContent = ReadIconFile(fullSourcePath);

            string hash = CalculateSha256(fileContent);

            string fileName = $"{hash}{extension}";

            string destinationPath = Path.Combine(IconDirectory, fileName);

            // Bereits vorhandene Datei überprüfen.

            if (File.Exists(destinationPath))
            {
                VerifyExistingIcon(destinationPath, hash);

                return fileName;
            }

            string temporaryFilePath = CreateTemporaryFilePath(IconDirectory);

            try
            {
                using (var stream = new FileStream(temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(fileContent, 0, fileContent.Length);

                    stream.Flush(flushToDisk: true);
                }

                try
                {
                    File.Move(temporaryFilePath, destinationPath, overwrite: false);
                }
                catch (IOException)
                    when (File.Exists(destinationPath))
                {
                    // Ein paralleler Import könnte dieselbe
                    // Datei inzwischen gespeichert haben.

                    VerifyExistingIcon(destinationPath, hash);
                }

                return fileName;
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryFilePath);
            }
        }

        /// <summary>
        /// Importiert ein Icon und ordnet es einem
        /// bestimmten LogLevel zu.
        /// </summary>
        /// <param name="settings">
        /// Zu aktualisierende Icon-Konfiguration.
        /// </param>
        /// <param name="level">
        /// Name des LogLevels.
        /// </param>
        /// <param name="sourcePath">
        /// Pfad der zu importierenden Bilddatei.
        /// </param>
        /// <param name="saveImmediately">
        /// Gibt an, ob die Einstellungen unmittelbar
        /// gespeichert werden sollen.
        /// </param>
        /// <returns>
        /// Relativer Dateiname des importierten Icons.
        /// </returns>
        public string ImportIcon(LogLevelIconSettings settings, string level, string sourcePath, bool saveImmediately = false)
        {
            ArgumentNullException.ThrowIfNull(settings);

            ArgumentException.ThrowIfNullOrWhiteSpace(level);

            string relativePath = ImportIcon(sourcePath);

            settings.SetIcon(level, relativePath);

            if (saveImmediately)
            {
                Save(settings);
            }

            return relativePath;
        }

        #endregion

        #region Path Handling

        /// <summary>
        /// Ermittelt den vollständigen Dateipfad eines
        /// gespeicherten Icons.
        /// </summary>
        /// <param name="relativePath">
        /// Relativer Dateiname innerhalb des Icon-Verzeichnisses.
        /// </param>
        /// <returns>
        /// Vollständiger Dateipfad oder null, wenn der
        /// Dateiname ungültig ist oder die Datei fehlt.
        /// </returns>
        public string? ResolveIconPath(string? relativePath)
        {
            if (!IsValidRelativeIconPath(relativePath))
                return null;

            string fullPath = Path.GetFullPath(
                Path.Combine(
                    IconDirectory,
                    relativePath!));

            if (!IsPathInsideIconDirectory(fullPath))
                return null;

            if (!File.Exists(fullPath))
                return null;

            try
            {
                // Symbolische Links und andere Reparse Points
                // werden nicht als Icon-Dateien akzeptiert.

                FileAttributes attributes =
                    File.GetAttributes(fullPath);

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    return null;

                return fullPath;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Ermittelt den vollständigen Icon-Dateipfad
        /// für einen bestimmten LogLevel.
        /// </summary>
        public string? ResolveIconPath(LogLevelIconSettings settings, string level)
        {
            ArgumentNullException.ThrowIfNull(settings);

            string? relativePath =
                settings.GetIcon(level);

            return ResolveIconPath(relativePath);
        }

        /// <summary>
        /// Prüft, ob eine gespeicherte Icon-Datei existiert.
        /// </summary>
        public bool IconExists(string? relativePath)
        {
            return ResolveIconPath(relativePath) is not null;
        }

        /// <summary>
        /// Gibt alle unterstützten und vorhandenen
        /// Icon-Dateien des Icon-Verzeichnisses zurück.
        /// </summary>
        public IReadOnlyList<string> GetIconFiles()
        {
            EnsureDirectories();

            return Directory
                .EnumerateFiles(
                    IconDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(IsSupportedExtension)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Select(ResolveIconPath)
                .OfType<string>()
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        #endregion

        #region Validation

        /// <summary>
        /// Prüft, ob die Dateiendung unterstützt wird.
        /// </summary>
        public bool IsSupportedExtension(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string extension =
                Path.GetExtension(filePath);

            return SupportedExtensions.Contains(extension);
        }

        /// <summary>
        /// Prüft, ob ein relativer Icon-Dateiname
        /// gültig ist.
        /// </summary>
        /// <remarks>
        /// Absolute Pfade, Unterverzeichnisse und
        /// Verzeichniswechsel werden nicht akzeptiert.
        /// </remarks>
        public bool IsValidRelativeIconPath(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            if (Path.IsPathRooted(relativePath))
                return false;

            if (relativePath is "." or "..")
                return false;

            if (relativePath.Contains('/') ||
                relativePath.Contains('\\') ||
                relativePath.Contains(':'))
            {
                return false;
            }

            if (relativePath.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            if (!string.Equals(
                    relativePath,
                    Path.GetFileName(relativePath),
                    StringComparison.Ordinal))
            {
                return false;
            }

            return IsSupportedExtension(relativePath);
        }

        /// <summary>
        /// Validiert die Quelldatei vor dem Import.
        /// </summary>
        private void ValidateSourceFile(string sourcePath)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    "Die Icon-Datei wurde nicht gefunden.",
                    sourcePath);
            }

            if (!IsSupportedExtension(sourcePath))
            {
                throw new NotSupportedException(
                    $"Nicht unterstütztes Dateiformat: " +
                    $"{Path.GetExtension(sourcePath)}");
            }

            var fileInfo = new FileInfo(sourcePath);

            if (fileInfo.Length == 0)
            {
                throw new InvalidDataException(
                    "Die Icon-Datei ist leer.");
            }

            if (fileInfo.Length > MaximumIconFileSize)
            {
                throw new InvalidDataException(
                    "Die Icon-Datei überschreitet " +
                    "die maximale Dateigröße von 10 MB.");
            }
        }

        /// <summary>
        /// Prüft, ob sich der angegebene Dateipfad
        /// innerhalb des Icon-Verzeichnisses befindet.
        /// </summary>
        private bool IsPathInsideIconDirectory(string fullPath)
        {
            string? parentDirectory =
                Path.GetDirectoryName(fullPath);

            string expectedDirectory =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(IconDirectory));

            return string.Equals(
                parentDirectory,
                expectedDirectory,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }

        #endregion

        #region SHA256 Handling

        /// <summary>
        /// Berechnet den SHA256-Hash eines Byte-Arrays.
        /// </summary>
        /// <param name="content">
        /// Zu prüfender Dateiinhalt.
        /// </param>
        /// <returns>
        /// SHA256-Hash als hexadezimale Zeichenfolge.
        /// </returns>
        private static string CalculateSha256(byte[] content)
        {
            ArgumentNullException.ThrowIfNull(content);

            byte[] hash = SHA256.HashData(content);

            return Convert
                .ToHexString(hash)
                .ToLowerInvariant();
        }

        /// <summary>
        /// Berechnet den SHA256-Hash einer Datei.
        /// </summary>
        public string CalculateFileSha256(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            byte[] hash = SHA256.HashData(stream);

            return Convert
                .ToHexString(hash)
                .ToLowerInvariant();
        }

        /// <summary>
        /// Prüft, ob eine bereits gespeicherte Icon-Datei
        /// dem erwarteten SHA256-Hash entspricht.
        /// </summary>
        private void VerifyExistingIcon(string filePath, string expectedHash)
        {
            if ((File.GetAttributes(filePath) &
                 FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "Verknüpfte Icon-Dateien werden " +
                    "nicht unterstützt.");
            }

            string actualHash =
                CalculateFileSha256(filePath);

            if (!string.Equals(
                    actualHash,
                    expectedHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Die vorhandene Icon-Datei stimmt " +
                    "nicht mit dem erwarteten SHA256-Hash überein.");
            }
        }

        #endregion

        #region File Handling

        /// <summary>
        /// Liest den vollständigen Inhalt einer Icon-Datei.
        /// Die maximale Dateigröße wird auch während
        /// des Einlesens überprüft.
        /// </summary>
        private static byte[] ReadIconFile(string filePath)
        {
            using var source = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            using var destination = new MemoryStream();

            byte[] buffer = new byte[81920];

            int bytesRead;

            while ((bytesRead = source.Read(
                       buffer,
                       0,
                       buffer.Length)) > 0)
            {
                if (destination.Length + bytesRead >
                    MaximumIconFileSize)
                {
                    throw new InvalidDataException(
                        "Die maximale Icon-Dateigröße " +
                        "wurde überschritten.");
                }

                destination.Write(
                    buffer,
                    0,
                    bytesRead);
            }

            if (destination.Length == 0)
            {
                throw new InvalidDataException(
                    "Die Icon-Datei ist leer.");
            }

            return destination.ToArray();
        }

        /// <summary>
        /// Erstellt einen eindeutigen temporären Dateipfad.
        /// </summary>
        private static string CreateTemporaryFilePath(string directory)
        {
            return Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        }

        /// <summary>
        /// Versucht, eine temporäre Datei zu entfernen,
        /// ohne eine ursprüngliche Ausnahme zu überschreiben.
        /// </summary>
        private static void TryDeleteTemporaryFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException ex)
            {
                System.Diagnostics.Trace.WriteLine(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Trace.WriteLine(ex);
            }
        }

        #endregion

        #region Settings Handling

        /// <summary>
        /// Erstellt eine unabhängige und normalisierte
        /// Kopie der Icon-Einstellungen.
        /// </summary>
        /// <remarks>
        /// Ungültige Dateipfade werden nicht übernommen.
        /// Fehlende Dateien bleiben als Zuordnung erhalten,
        /// sofern ihr relativer Dateiname gültig ist.
        /// </remarks>
        private LogLevelIconSettings NormalizeSettings(LogLevelIconSettings settings)
        {
            var normalizedSettings = LogLevelIconSettings.CreateDefault();

            foreach (var entry in settings.Export())
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                if (!IsValidRelativeIconPath(entry.Value))
                    continue;

                normalizedSettings.SetIcon(
                    entry.Key,
                    entry.Value);
            }

            return normalizedSettings;
        }

        #endregion

        #region Directory Handling

        /// <summary>
        /// Erstellt die benötigten Anwendungsverzeichnisse,
        /// sofern diese noch nicht vorhanden sind.
        /// </summary>
        public void EnsureDirectories()
        {
            Directory.CreateDirectory(RootDirectory);

            Directory.CreateDirectory(IconDirectory);
        }

        /// <summary>
        /// Ermittelt das standardmäßige Basisverzeichnis
        /// für die LogLevel-Icon-Verwaltung.
        /// </summary>
        private static string GetDefaultRootDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),

                "BewerbungsAssi",

                "Logger");
        }

        #endregion
    }
}
