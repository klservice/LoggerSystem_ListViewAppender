using log4net.Core;

using LoggerSystem.Appenders;
using LoggerSystem.Commands;
using LoggerSystem.Core;
using LoggerSystem.Interfaces;
using LoggerSystem.Models;
using LoggerSystem.Services;

using Microsoft.Win32;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LoggerSystem.ViewModels;

public sealed class LogViewModel : ViewModelBase, ILogEntrySink
{
    readonly Dispatcher _dispatcher;
    readonly LogExportService _export;
    private readonly LoggerListViewAppender _appender;
    private readonly ObservableCollection<LoggingEvent> _entries = new();
    readonly ObservableCollection<LogEntryModel> _items = new();
    readonly ReadOnlyObservableCollection<LogEntryModel> _readOnly;
    private readonly DispatcherTimer _searchTimer;
    private int _refreshPending;
    private volatile bool _disposed;

    string _selectedLevel = "ALL";
    string _searchText = "";
    string _status = "Ready";
    int _maxEntries = 2000;
    bool _autoScroll = true;
    bool _paused;
    LogEntryModel? _selectedEntry;
    public ReadOnlyObservableCollection<LoggingEvent> Entries { get; }
    public ReadOnlyObservableCollection<LogEntryModel> Entryes => _readOnly;
    public ICollectionView FilteredEntries { get; }
    public string[] AvailableLevels { get; } = { "ALL", "DEBUG", "INFO", "WARN", "ERROR", "FATAL" };
    public int EntryCount => _items.Count;
    public int MaxEntries { get => _maxEntries; set { if (SetProperty(ref _maxEntries, Math.Max(1, value))) Trim(); } }
    //  public RelayCommand ToggleAutoScrollCommand { get; }
    //  public RelayCommand RefreshFiltersCommand { get; }
    //  public RelayCommand ClearCommand { get; }
    //  public AsyncRelayCommand ExportCsvCommand { get; }
    //  public AsyncRelayCommand ExportJsonCommand { get; }
    public bool AutoScroll { get => _autoScroll; set => SetProperty(ref _autoScroll, value); }
    private string? _lastExportError;
    public string? LastExportError { get => _lastExportError; private set => SetProperty(ref _lastExportError, value); }
    public bool IsPaused { get => _paused; set => SetProperty(ref _paused, value); }
    public string SelectedLevel { get => _selectedLevel; set { if (SetProperty(ref _selectedLevel, value)) FilteredEntries.Refresh(); } }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) FilteredEntries.Refresh(); } }
    public string StatusMessage { get => _status; private set => SetProperty(ref _status, value); }
    public LogEntryModel? SelectedEntry { get => _selectedEntry; set { if (SetProperty(ref _selectedEntry, value)) { ((RelayCommand)CopySelectedCommand).RaiseCanExecuteChanged(); ((RelayCommand)RemoveSelectedCommand).RaiseCanExecuteChanged(); } } }

    public AsyncRelayCommand ExportTxtCommand { get; private set; } = null!;
    public AsyncRelayCommand ExportCsvCommand { get; private set; } = null!;
    public AsyncRelayCommand ExportJsonCommand { get; private set; } = null!;
    public AsyncRelayCommand ExportZipCommand { get; private set; } = null!;

    public ICommand ToggleAutoScrollCommand { get; private set; } = null!;
    public ICommand RefreshFiltersCommand { get; private set; } = null!;

    public ICommand ClearCommand { get; private set; } = null!;
    public ICommand PauseCommand { get; private set; } = null!;
    public ICommand ResumeCommand { get; private set; } = null!;
    public ICommand ClearSearchCommand { get; private set; } = null!;

    public ICommand CopySelectedCommand { get; private set; } = null!;
    public ICommand CopyAllCommand { get; private set; } = null!;
    public ICommand RemoveSelectedCommand { get; private set; } = null!;


    /// <summary>Create/dispose on the WPF UI thread. Use the overload with an explicit UI dispatcher when needed.</summary>
    public LogViewModel() : this(System.Windows.Application.Current?.Dispatcher
        ?? throw new InvalidOperationException("A WPF Application or explicit UI Dispatcher is required."))
    { }

    public LogViewModel(Dispatcher dispatcher, LogExportService export)
    {
        _dispatcher = dispatcher; _export = export; _readOnly = new(_items);
        FilteredEntries = CollectionViewSource.GetDefaultView(_items); FilteredEntries.Filter = Matches;
        _items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EntryCount));
        //  ClearCommand = new RelayCommand(() => _items.Clear());
        //  PauseCommand = new RelayCommand(() => IsPaused = true); ResumeCommand = new RelayCommand(() => IsPaused = false);
        //  ClearSearchCommand = new RelayCommand(() => SearchText = "");
        //   CopySelectedCommand = new RelayCommand(() => { if (SelectedEntry != null) Clipboard.SetText(SelectedEntry.FullMessage); }, () => SelectedEntry != null);
        //  CopyAllCommand = new RelayCommand(() => Clipboard.SetText(string.Join(Environment.NewLine, FilteredEntries.Cast<LogEntryModel>().Select(e => e.FullMessage))));
        //   RemoveSelectedCommand = new RelayCommand(() => { if (SelectedEntry != null) _items.Remove(SelectedEntry); }, () => SelectedEntry != null);
        ExportTxtCommand = new AsyncRelayCommand(() => ExportAsync("Text files|*.txt", "txt", (path, entries) => _export.ExportTxtAsync(path, entries)));
        ExportCsvCommand = new AsyncRelayCommand(() => ExportAsync("CSV files|*.csv", "csv", (p, e) => _export.ExportCsvAsync(p, e)));
        ExportZipCommand = new AsyncRelayCommand(() => ExportAsync("ZIP files|*.zip", "zip", (p, e) => _export.ExportZipAsync(p, e)));
        ExportJsonCommand = new AsyncRelayCommand(() => ExportAsync("Json files|*.json", "json", (p, e) => _export.ExportJsonAsync(p, e)));
    }


    public LogViewModel(Dispatcher dispatcher, LoggerListViewAppender? appender = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _dispatcher.VerifyAccess();
        _appender = appender ?? LoggerFactory.LiveAppender;
        Entries = new ReadOnlyObservableCollection<LoggingEvent>(_entries);
        FilteredEntries = new ListCollectionView(_entries) { Filter = Matches };
        _searchTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(180) };
        _searchTimer.Tick += OnSearchTick;
        ToggleAutoScrollCommand = new RelayCommand(() => AutoScroll = !AutoScroll);
        RefreshFiltersCommand = new RelayCommand(RefreshView);
        // ClearCommand = new RelayCommand(_appender.Clear);
        ExportCsvCommand = new AsyncRelayCommand(() => ExportAsync("CSV files|*.csv", "csv", (p, e) => _export.ExportCsvAsync(p, e)));
        ExportZipCommand = new AsyncRelayCommand(() => ExportAsync("ZIP files|*.zip", "zip", (p, e) => _export.ExportZipAsync(p, e)));
        ExportJsonCommand = new AsyncRelayCommand(() => ExportAsync("JSON files|*.json", "json", (p, e) => _export.ExportJsonAsync(p, e)));


        _appender.EntriesChanged += OnEntriesChanged;
        SynchronizeEntries();
    }


    //private void OnExportFailed(object? sender, EventArgs e)
    //{
    //    if (sender is AsyncRelayCommand command) LastExportError = command.LastException?.Message;
    //}
    private void OnSearchTick(object? sender, EventArgs e) { _searchTimer.Stop(); RefreshView(); }
    private void OnEntriesChanged(object? sender, EventArgs e)
    {
        // Coalesce producer notifications: at most one pending dispatcher operation.
        if (_disposed || _dispatcher.HasShutdownStarted || Interlocked.Exchange(ref _refreshPending, 1) != 0) return;
        try
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                Interlocked.Exchange(ref _refreshPending, 0);
                if (!_disposed) SynchronizeEntries();
            }));
        }
        catch (InvalidOperationException) { Interlocked.Exchange(ref _refreshPending, 0); }
    }
    private void SynchronizeEntries()
    {
        _dispatcher.VerifyAccess();
        var snapshot = _appender.GetEvents();
        using (FilteredEntries.DeferRefresh())
        {
            _entries.Clear();
            foreach (var entry in snapshot) _entries.Add(entry);
        }
    }


    //private ExportEntry[] Snapshot()
    //{
    //    _dispatcher.VerifyAccess();
    //    ObjectDisposedException.ThrowIf(_disposed, this);
    //    // Export precisely the currently visible rows, materialized before awaiting I/O.
    //    return EntriesView.Cast<LoggingEvent>().Select(e => new ExportEntry(
    //        e.TimeStamp.ToString("O", CultureInfo.InvariantCulture), e.Level?.Name ?? "",
    //        e.LoggerName ?? "", e.RenderedMessage ?? "", e.GetExceptionString() ?? "")).ToArray();
    //}

    //public async Task ExportJsonAsync(string path)
    //{
    //    var rows = Snapshot();
    //    LastExportError = null;
    //    await using var stream = new FileStream(PrepareExportPath(path), FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
    //    await JsonSerializer.SerializeAsync(stream, rows, new JsonSerializerOptions { WriteIndented = true });
    //}
    //public void RefreshView()
    //{
    //    _dispatcher.VerifyAccess();
    //    if (!_disposed) EntriesView.Refresh();
    //}

    //private LoggingEvent[] Snapshot()
    //{
    //    _dispatcher.VerifyAccess();
    //    ObjectDisposedException.ThrowIf(_disposed, this);
    //    // Export precisely the currently visible rows, materialized before awaiting I/O.
    //    return FilteredEntries.Cast<LoggingEvent>().Select(e => new LoggingEvent(
    //        e.TimeStamp.ToString("O", CultureInfo.InvariantCulture), e.Level?.Name ?? "",
    //        e.LoggerName ?? "", e.RenderedMessage ?? "", e.ExceptionObject.ToString() ?? "")).ToArray();
    //}



    /// <summary>
    /// Initialisiert sämtliche Commands.
    /// </summary>
    public void InitializeCommands()
    {
        ClearCommand = new RelayCommand(ClearEntries);

        PauseCommand = new RelayCommand(() => IsPaused = true);

        ResumeCommand = new RelayCommand(() => IsPaused = false);

        ClearSearchCommand = new RelayCommand(() => SearchText = "");

        CopySelectedCommand = new RelayCommand(CopySelectedEntry, () => SelectedEntry != null);

        CopyAllCommand = new RelayCommand(CopyAllEntries);

        RemoveSelectedCommand = new RelayCommand(RemoveSelectedEntry, () => SelectedEntry != null);

        ExportTxtCommand = new AsyncRelayCommand(
            () => ExportAsync(
                "Text files|*.txt",
                "txt",
                (path, entries) =>
                    _export.ExportTxtAsync(path, entries)));

        ExportCsvCommand = new AsyncRelayCommand(
            () => ExportAsync(
                "CSV files|*.csv",
                "csv",
                (path, entries) =>
                    _export.ExportCsvAsync(path, entries)));

        ExportJsonCommand = new AsyncRelayCommand(
            () => ExportAsync(
                "JSON files|*.json",
                "json",
                (path, entries) =>
                    _export.ExportJsonAsync(path, entries)));

        ExportZipCommand = new AsyncRelayCommand(
            () => ExportAsync(
                "ZIP files|*.zip",
                "zip",
                (path, entries) =>
                    _export.ExportZipAsync(path, entries)));
    }


    /// <summary>
    /// Entfernt sämtliche Logeinträge.
    /// </summary>
    public void ClearEntries()
    {
        _items.Clear();

        StatusMessage = "Log entries cleared.";
    }

    /// <summary>
    /// Begrenzt die Anzahl gespeicherter Logeinträge.
    /// </summary>
    private void TrimEntries()
    {
        while (_items.Count > MaxEntries)
            _items.RemoveAt(0);
    }


    /// <summary>
    /// Entfernt den ausgewählten Logeintrag.
    /// </summary>
    private void RemoveSelectedEntry()
    {
        if (SelectedEntry is null)
            return;

        _items.Remove(SelectedEntry);

        SelectedEntry = null;
    }

    /// <summary>
    /// Kopiert den ausgewählten Logeintrag.
    /// </summary>
    private void CopySelectedEntry()
    {
        if (SelectedEntry is null)
            return;

        Clipboard.SetText(SelectedEntry.FullMessage);

        StatusMessage = "Log entry copied.";
    }

    /// <summary>
    /// Kopiert alle aktuell gefilterten Logeinträge.
    /// </summary>
    private void CopyAllEntries()
    {
        var text = string.Join(
            Environment.NewLine,
            FilteredEntries
                .Cast<LogEntryModel>()
                .Select(entry => entry.FullMessage));

        Clipboard.SetText(text);

        StatusMessage = "Log entries copied.";
    }

    /// <summary>
    /// Exportiert die gefilterten Logeinträge.
    /// </summary>
    private async Task ExportAsync(string filter, string extension, Func<string, LogEntryModel[], Task> exporter)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension,
            AddExtension = true,
            FileName = "logs." + extension
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var entries = FilteredEntries
                .Cast<LogEntryModel>()
                .ToArray();

            await exporter(dialog.FileName, entries);

            StatusMessage =
                $"Exported {entries.Length} log entries.";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Export failed: " + ex.Message;
        }
    }


    /// <summary>
    /// Aktualisiert die gefilterte Ansicht.
    /// </summary>
    public void RefreshFilter()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(RefreshFilter);
            return;
        }

        FilteredEntries.Refresh();
    }

    /// <summary>
    /// Prüft, ob ein Logeintrag den Filterbedingungen entspricht.
    /// </summary>
    /// <param name="item">Zu prüfender Eintrag.</param>
    /// <returns>True, wenn der Eintrag angezeigt werden soll.</returns>
    private bool Filter(object item)
    {
        if (item is not LogEntryModel entry)
            return false;

        // Log-Level prüfen
        if (!string.IsNullOrWhiteSpace(SelectedLevel) &&
            !string.Equals(
                SelectedLevel,
                "ALL",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                entry.Level,
                SelectedLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Ohne Suchtext alle Einträge des Levels anzeigen
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        // Suchtext in allen relevanten Feldern prüfen
        return new string?[]
        {
            entry.Message,
            entry.Level,
            entry.Logger,
            entry.Thread,
            entry.Exception
        }.Any(value =>
            !string.IsNullOrEmpty(value) &&
            value.Contains(
                SearchText,
                StringComparison.OrdinalIgnoreCase));
    }

    public void RefreshView()
    {
        _dispatcher.VerifyAccess();
        if (!_disposed) FilteredEntries.Refresh();
    }

    bool Matches(object item)
    {
        if (item is not LogEntryModel e) return false;
        if (SelectedLevel != "ALL" && !string.Equals(e.Level, SelectedLevel, StringComparison.OrdinalIgnoreCase)) return false;
        return string.IsNullOrWhiteSpace(SearchText) || new[] { e.Message, e.Level, e.Logger, e.Thread, e.Exception }.Any(s => s.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
    }
    public void AddEntry(LogEntryModel entry)
    {
        if (!_dispatcher.CheckAccess()) { _dispatcher.BeginInvoke(new Action(() => AddEntry(entry))); return; }
        if (IsPaused) return;
        _items.Add(entry); Trim();
    }
    void Trim() { while (_items.Count > MaxEntries) _items.RemoveAt(0); }

    //async Task ExportAsync(string filter, string ext, Func<string, LogEntryModel[], Task> exporter)
    //{
    //    var dialog = new SaveFileDialog { Filter = filter, DefaultExt = ext, AddExtension = true, FileName = "logs." + ext };
    //    if (dialog.ShowDialog() != true) return;
    //    try { var entries = FilteredEntries.Cast<LogEntryModel>().ToArray(); await exporter(dialog.FileName, entries); StatusMessage = $"Exported {entries.Length} entries."; }
    //    catch (Exception ex) { StatusMessage = "Export failed: " + ex.Message; }
    //}
}
