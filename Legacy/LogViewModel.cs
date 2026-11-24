using log4net.Core;

using LoggerSystem.Appenders;
using LoggerSystem.Commands;
using LoggerSystem.Core;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Threading;

namespace LoggerSystem.Legacy;

public sealed class LogViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly LoggerListViewAppender _appender;
    private readonly ObservableCollection<LoggingEvent> _entries = new();
    private readonly DispatcherTimer _searchTimer;
    private int _refreshPending;
    private volatile bool _disposed;
    private bool _autoScroll = true;
    private string _searchText = "";
    private string? _lastExportError;
    private bool _showDebug = true, _showInfo = true, _showWarn = true, _showError = true, _showFatal = true;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ReadOnlyObservableCollection<LoggingEvent> Entries { get; }
    public ICollectionView EntriesView { get; }
    public RelayCommand ToggleAutoScrollCommand { get; }
    public RelayCommand RefreshFiltersCommand { get; }
    public RelayCommand ClearCommand { get; }
    public AsyncRelayCommand ExportCsvCommand { get; }
    public AsyncRelayCommand ExportJsonCommand { get; }
    public bool AutoScroll { get => _autoScroll; set => Set(ref _autoScroll, value); }
    public string? LastExportError { get => _lastExportError; private set => Set(ref _lastExportError, value); }
    public string SearchText
    {
        get => _searchText;
        set { if (Set(ref _searchText, value ?? "")) { _searchTimer.Stop(); _searchTimer.Start(); } }
    }
    public bool ShowDebug { get => _showDebug; set { if (Set(ref _showDebug, value)) RefreshView(); } }
    public bool ShowInfo { get => _showInfo; set { if (Set(ref _showInfo, value)) RefreshView(); } }
    public bool ShowWarn { get => _showWarn; set { if (Set(ref _showWarn, value)) RefreshView(); } }
    public bool ShowError { get => _showError; set { if (Set(ref _showError, value)) RefreshView(); } }
    public bool ShowFatal { get => _showFatal; set { if (Set(ref _showFatal, value)) RefreshView(); } }



    /// <summary>Create/dispose on the WPF UI thread. Use the overload with an explicit UI dispatcher when needed.</summary>
    public LogViewModel() : this(System.Windows.Application.Current?.Dispatcher
        ?? throw new InvalidOperationException("A WPF Application or explicit UI Dispatcher is required."))
    { }

    public LogViewModel(Dispatcher dispatcher, LoggerListViewAppender? appender = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _dispatcher.VerifyAccess();
        _appender = appender ?? LoggerFactory.LiveAppender;
        Entries = new ReadOnlyObservableCollection<LoggingEvent>(_entries);
        EntriesView = new ListCollectionView(_entries) { Filter = Matches };
        _searchTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(180) };
        _searchTimer.Tick += OnSearchTick;
        ToggleAutoScrollCommand = new RelayCommand(() => AutoScroll = !AutoScroll);
        RefreshFiltersCommand = new RelayCommand(RefreshView);
        ClearCommand = new RelayCommand(_appender.Clear);
        ExportCsvCommand = new AsyncRelayCommand(p => ExportToCsvAsync(p?.ToString() ?? "logs_export.csv"));
        ExportJsonCommand = new AsyncRelayCommand(p => ExportToJsonAsync(p?.ToString() ?? "logs_export.json"));
        ExportCsvCommand.ExecutionFailed += OnExportFailed;
        ExportJsonCommand.ExecutionFailed += OnExportFailed;
        _appender.EntriesChanged += OnEntriesChanged;
        SynchronizeEntries();
    }
    private void OnExportFailed(object? sender, EventArgs e)
    {
        if (sender is AsyncRelayCommand command) LastExportError = command.LastException?.Message;
    }
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
        using (EntriesView.DeferRefresh())
        {
            _entries.Clear();
            foreach (var entry in snapshot) _entries.Add(entry);
        }
    }
    public void RefreshView()
    {
        _dispatcher.VerifyAccess();
        if (!_disposed) EntriesView.Refresh();
    }
    private bool Matches(object value)
    {
        if (value is not LoggingEvent e) return false;
        int level = e.Level?.Value ?? Level.Debug.Value;
        bool visible = level >= Level.Fatal.Value ? ShowFatal : level >= Level.Error.Value ? ShowError :
            level >= Level.Warn.Value ? ShowWarn : level >= Level.Info.Value ? ShowInfo : ShowDebug;
        return visible && (string.IsNullOrWhiteSpace(SearchText) ||
            (e.RenderedMessage + " " + e.LoggerName + " " + e.GetExceptionString())
                .Contains(SearchText, StringComparison.OrdinalIgnoreCase));
    }
    private ExportEntry[] Snapshot()
    {
        _dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Export precisely the currently visible rows, materialized before awaiting I/O.
        return EntriesView.Cast<LoggingEvent>().Select(e => new ExportEntry(
            e.TimeStamp.ToString("O", CultureInfo.InvariantCulture), e.Level?.Name ?? "",
            e.LoggerName ?? "", e.RenderedMessage ?? "", e.GetExceptionString() ?? "")).ToArray();
    }
    public async Task ExportToCsvAsync(string path)
    {
        var rows = Snapshot();
        LastExportError = null;
        var text = new StringBuilder("Time,Level,Logger,Message,Exception\r\n");
        foreach (var e in rows)
            text.AppendLine(string.Join(",", new[] { e.Time, e.Level, e.Logger, e.Message, e.Exception }.Select(Csv)));
        await File.WriteAllTextAsync(PrepareExportPath(path), text.ToString(), new UTF8Encoding(true));
    }
    public async Task ExportToJsonAsync(string path)
    {
        var rows = Snapshot();
        LastExportError = null;
        await using var stream = new FileStream(PrepareExportPath(path), FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(stream, rows, new JsonSerializerOptions { WriteIndented = true });
    }
    private static string PrepareExportPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return full;
    }
    private static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        _dispatcher.VerifyAccess();
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
    public void Dispose()
    {
        _dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        _searchTimer.Stop();
        _searchTimer.Tick -= OnSearchTick;
        _appender.EntriesChanged -= OnEntriesChanged;
        ExportCsvCommand.ExecutionFailed -= OnExportFailed;
        ExportJsonCommand.ExecutionFailed -= OnExportFailed;
    }
    private sealed record ExportEntry(string Time, string Level, string Logger, string Message, string Exception);
}
