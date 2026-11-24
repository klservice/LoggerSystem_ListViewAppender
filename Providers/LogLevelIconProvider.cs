using LoggerSystem.Models;

using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LoggerSystem.Providers
{
    public sealed class LogLevelIconProvider : IValueConverter
    {
        private readonly Dictionary<string, ImageSource> _customIcons = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _iconPaths = new(StringComparer.OrdinalIgnoreCase);

        private bool _useCustomIcons = true;

        /// <summary>
        /// Occurs when icon settings have changed.
        /// </summary>
        public event EventHandler? IconsChanged;

        /// <summary>
        /// Enables or disables custom icon files.
        /// </summary>
        public bool UseCustomIcons
        {
            get => _useCustomIcons;
            set
            {
                if (_useCustomIcons == value)
                    return;

                _useCustomIcons = value;
                OnIconsChanged();
            }
        }

        private static readonly Dictionary<string, ImageSource> DefaultIcons = CreateDefaultIcons();

        private static readonly ImageSource FallbackIcon = CreateCircle(Color.FromRgb(105, 105, 105));

        /// <summary>
        /// Returns the configured icon for a log level.
        /// </summary>
        public ImageSource GetIcon(string? level)
        {
            string key = NormalizeLevel(level);

            if (UseCustomIcons &&
                _customIcons.TryGetValue(key, out var customIcon))
            {
                return customIcon;
            }

            return DefaultIcons.TryGetValue(key, out var icon)
                ? icon
                : FallbackIcon;
        }




        /// <summary>
        /// Sets a custom image file for a log level.
        /// </summary>
        public void SetIcon(string level, string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(level);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string key = NormalizeLevel(level);
            string fullPath = Path.GetFullPath(filePath);

            ImageSource image = LoadImage(fullPath);

            _iconPaths[key] = fullPath;
            _customIcons[key] = image;

            OnIconsChanged();
        }

        /// <summary>
        /// Removes a custom icon and restores the default icon.
        /// </summary>
        public void ResetIcon(string level)
        {
            string key = NormalizeLevel(level);

            bool changed = _iconPaths.Remove(key);
            changed |= _customIcons.Remove(key);

            if (changed)
                OnIconsChanged();
        }

        /// <summary>
        /// Removes all custom icon assignments.
        /// </summary>
        public void ResetAllIcons()
        {
            _iconPaths.Clear();
            _customIcons.Clear();

            OnIconsChanged();
        }

        /// <summary>
        /// Returns the configured file path for a log level.
        /// </summary>
        public string? GetIconPath(string level)
        {
            string key = NormalizeLevel(level);

            return _iconPaths.TryGetValue(key, out var path)
                ? path
                : null;
        }

        /// <summary>
        /// Applies saved icon settings.
        /// Missing or invalid images fall back to vector icons.
        /// </summary>
        public void ApplySettings(LogLevelIconSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            _iconPaths.Clear();
            _customIcons.Clear();

            _useCustomIcons = settings.UseCustomIcons;

            foreach (var pair in settings.IconPaths)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) ||
                    string.IsNullOrWhiteSpace(pair.Value))
                    continue;

                try
                {
                    string key = NormalizeLevel(pair.Key);
                    string path = Path.GetFullPath(pair.Value);

                    _iconPaths[key] = path;
                    _customIcons[key] = LoadImage(path);
                }
                catch (Exception ex) when (
                    ex is IOException ||
                    ex is UnauthorizedAccessException ||
                    ex is ArgumentException ||
                    ex is NotSupportedException)
                {
                    // Invalid files use the default icon.
                }
            }

            OnIconsChanged();
        }

        /// <summary>
        /// Exports the current icon assignments.
        /// </summary>
        public LogLevelIconSettings ExportSettings()
        {
            return new LogLevelIconSettings
            {
                UseCustomIcons = UseCustomIcons,
                IconPaths = new Dictionary<string, string>(
                    _iconPaths,
                    StringComparer.OrdinalIgnoreCase)
            };
        }

        /// <summary>
        /// Loads an image completely into memory.
        /// The source file is not kept locked.
        /// </summary>
        private static ImageSource LoadImage(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "Icon file not found.", path);

            var extension = Path.GetExtension(path);

            if (!new[]
            {
            ".png", ".jpg", ".jpeg",
            ".bmp", ".gif", ".ico"
        }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"Unsupported icon format: {extension}");
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            var bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 32;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }


        /// <summary>
        /// Converts a log level into an ImageSource.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return GetIcon(value?.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }

        private static string NormalizeLevel(string? level)
        {
            return string.IsNullOrWhiteSpace(level)
                ? "DEFAULT"
                : level.Trim().ToUpperInvariant();
        }

        private void OnIconsChanged()
        {
            IconsChanged?.Invoke(this, EventArgs.Empty);
        }

        private static Dictionary<string, ImageSource> CreateDefaultIcons()
        {
            return new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase)
            {
                ["DEBUG"] = CreateCircle(Color.FromRgb(105, 105, 105)),

                ["INFO"] = CreateCircle(Color.FromRgb(65, 105, 225)),

                ["WARN"] = CreateTriangle(Color.FromRgb(255, 140, 0)),

                ["WARNING"] = CreateTriangle(Color.FromRgb(255, 140, 0)),

                ["ERROR"] = CreateSquare(Color.FromRgb(178, 34, 34)),

                ["FATAL"] = CreateDiamond(Color.FromRgb(139, 0, 0)),

                ["TRACE"] = CreateCircle(Color.FromRgb(128, 128, 128)),

                ["SUCCESS"] = CreateCircle(Color.FromRgb(34, 139, 34))
            };
        }

        private static ImageSource CreateIconDebug(Color color)
        {
            return CreateImage(
                Geometry.Parse(
                    "M18, 26 C18, 8 46, 8 46, 26 Z" +
                    "M16, 29 C16, 23 48, 23 48, 29" +
                    "L48, 42 C48, 66 16, 66 16, 42 Z" +
                    "M20, 13 L13, 5 Q10, 1 7, 5 L6, 7 L15, 18 Z" +
                    "M44, 13 L51, 5 Q54, 1 57, 5 L58, 7 L49, 18 Z" +
                    "M17, 29 L8, 22 L1, 29 L4, 33 L9, 28 L16, 34 Z" +
                    "M16, 38 L7, 34 L0, 43 L4, 46 L9, 40 L17, 44 Z" +
                    "M17, 47 L8, 45 L2, 57 L7, 60 L12, 51 L21, 53 Z" +
                    "M47, 29 L56, 22 L63, 29 L60, 33 L55, 28 L48, 34 Z" +
                    "M48, 38 L57, 34 L64, 43 L60, 46 L55, 40 L47, 44 Z" +
                    "M47, 47 L56, 45 L62, 57 L57, 60 L52, 51 L43, 53 Z"),
                color);
        }

        private static ImageSource CreateCircle(Color color)
        {
            return CreateImage(
                        Geometry.Parse(
                            "M 8,1 A 7,7 0 1 1 8,15 A 7,7 0 1 1 8,1 Z"),
                        color);
        }

        private static ImageSource CreateTriangle(Color color)
            => CreateImage(
                Geometry.Parse("M 8,1 L 15,14 L 1,14 Z"),
                color);

        private static ImageSource CreateSquare(Color color)
            => CreateImage(
                Geometry.Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z"),
                color);

        private static ImageSource CreateDiamond(Color color)
            => CreateImage(
                Geometry.Parse("M 8,0 L 16,8 L 8,16 L 0,8 Z"),
                color);

        private static ImageSource CreateImage(Geometry geometry, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            geometry.Freeze();

            var drawing = new GeometryDrawing
            {
                Brush = brush,
                Geometry = geometry
            };

            drawing.Freeze();

            var image = new DrawingImage(drawing);
            image.Freeze();

            return image;
        }
    }
}
