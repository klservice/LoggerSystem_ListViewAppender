namespace LoggerSystem.Interfaces
{
    /// <summary>
    /// Definiert einen Service zur Auswahl von Dateien.
    /// Ermöglicht eine MVVM-konforme Verwendung von Dateidialogen,
    /// unabhängig von der konkreten Benutzeroberfläche.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Öffnet einen Dateiauswahldialog zur Auswahl
        /// einer Bilddatei.
        /// </summary>
        /// <returns>
        /// Vollständiger Dateipfad der ausgewählten Bilddatei
        /// oder null, wenn der Dialog abgebrochen wurde.
        /// </returns>
        string? SelectImageFile();
    }
}
