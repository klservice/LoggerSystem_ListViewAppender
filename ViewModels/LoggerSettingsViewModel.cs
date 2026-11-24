using LoggerSystem.Commands;
using LoggerSystem.Core;
using LoggerSystem.Interfaces;
using LoggerSystem.Models;
using LoggerSystem.Services;

using System.Windows.Input;
namespace LoggerSystem.ViewModels;

public sealed class LoggerSettingsViewModel : ViewModelBase
{
    readonly ILogSettingsService _service; readonly LoggerRuntime _runtime; readonly LogViewModel _logView;
    bool _listView = true, _rolling = true, _console, _autoScroll = true, _clean, _autoClean = true, _append = true, _busy, _hasError;
    string _level = "DEBUG", _path = "Logs/application.log", _name = "application.log", _pattern = "%d{HH:mm:ss.ffff} [%level] %type %C:%L - %m%n", _status = "Ready";
    int _entries = 2000, _age = 30, _backups = 10; long _size = 10485760;
    public LoggerSettingsViewModel(ILogSettingsService service, LoggerRuntime runtime, LogViewModel logView)
    {
        _service = service; _runtime = runtime; _logView = logView;
        SaveCommand = new AsyncRelayCommand(() => RunAsync(async () => { await _service.SaveAsync(ToConfig()); StatusMessage = "Settings saved."; }), () => !IsBusy);
        LoadCommand = new AsyncRelayCommand(() => RunAsync(async () => { SetConfig(await _service.LoadAsync()); StatusMessage = "Settings loaded."; }), () => !IsBusy);
        ApplyCommand = new AsyncRelayCommand(() => RunAsync(async () => { var c = ToConfig(); await _runtime.ApplyAsync(c); _logView.MaxEntries = c.MaximumListViewEntries; _logView.AutoScroll = c.AutoScroll; StatusMessage = "Settings applied."; }), () => !IsBusy);
        ResetCommand = new RelayCommand(() => { SetConfig(_service.GetDefaults()); HasError = false; StatusMessage = "Defaults restored (not saved or applied)."; }, () => !IsBusy);
    }
    public IReadOnlyList<string> AvailableLevels { get; } = new[] { "ALL", "DEBUG", "INFO", "WARN", "ERROR", "FATAL", "OFF" };
    public bool UseListView { get => _listView; set => SetProperty(ref _listView, value); }
    public bool UseRollingFile { get => _rolling; set => SetProperty(ref _rolling, value); }
    public bool UseConsole { get => _console; set => SetProperty(ref _console, value); }
    public string MinimumLevel { get => _level; set => SetProperty(ref _level, value); }
    public int MaximumListViewEntries { get => _entries; set => SetProperty(ref _entries, value); }
    public bool AutoScroll { get => _autoScroll; set => SetProperty(ref _autoScroll, value); }
    public bool CleanLogFolder { get => _clean; set => SetProperty(ref _clean, value); }
    public int MaxLogFileAge { get => _age; set => SetProperty(ref _age, value); }
    public bool AutoCleanLogFolder { get => _autoClean; set => SetProperty(ref _autoClean, value); }
    public string LogFilePath { get => _path; set => SetProperty(ref _path, value); }
    public string LogFileName { get => _name; set => SetProperty(ref _name, value); }
    public string LogMessageLayoutPattern { get => _pattern; set => SetProperty(ref _pattern, value); }
    public long MaximumFileSizeBytes { get => _size; set => SetProperty(ref _size, value); }
    public int MaximumBackupFiles { get => _backups; set => SetProperty(ref _backups, value); }
    public bool AppendToFile { get => _append; set => SetProperty(ref _append, value); }
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) { foreach (var c in new[] { SaveCommand, LoadCommand, ApplyCommand, ResetCommand }) { if (c is AsyncRelayCommand a) a.RaiseCanExecuteChanged(); if (c is RelayCommand r) r.RaiseCanExecuteChanged(); } } } }
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public string StatusMessage { get => _status; private set => SetProperty(ref _status, value); }
    public ICommand SaveCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand ResetCommand { get; }
    public LoggerConfig ToConfig()
    {
        var c = new LoggerConfig { AppenderTypes = (UseListView ? AppenderType.ListView : 0) | (UseRollingFile ? AppenderType.RollingFile : 0) | (UseConsole ? AppenderType.Console : 0), MinimumLevel = MinimumLevel, MaximumListViewEntries = MaximumListViewEntries, AutoScroll = AutoScroll, CleanLogFolder = CleanLogFolder, MaxLogFileAge = MaxLogFileAge, AutoCleanLogFolder = AutoCleanLogFolder, LogFilePath = LogFilePath, LogFileName = LogFileName, LogMessageLayoutPattern = LogMessageLayoutPattern, MaximumFileSizeBytes = MaximumFileSizeBytes, MaximumBackupFiles = MaximumBackupFiles, AppendToFile = AppendToFile }; c.Validate(); return c;
    }
    public void SetConfig(LoggerConfig c)
    {
        UseListView = c.AppenderTypes.HasFlag(AppenderType.ListView); UseRollingFile = c.AppenderTypes.HasFlag(AppenderType.RollingFile); UseConsole = c.AppenderTypes.HasFlag(AppenderType.Console);
        MinimumLevel = c.MinimumLevel; MaximumListViewEntries = c.MaximumListViewEntries; AutoScroll = c.AutoScroll; CleanLogFolder = c.CleanLogFolder; MaxLogFileAge = c.MaxLogFileAge; AutoCleanLogFolder = c.AutoCleanLogFolder; LogFilePath = c.LogFilePath; LogFileName = c.LogFileName; LogMessageLayoutPattern = c.LogMessageLayoutPattern; MaximumFileSizeBytes = c.MaximumFileSizeBytes; MaximumBackupFiles = c.MaximumBackupFiles; AppendToFile = c.AppendToFile;
    }
    async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return; IsBusy = true; HasError = false;
        try { await action(); } catch (Exception ex) { HasError = true; StatusMessage = "Error: " + ex.Message; } finally { IsBusy = false; }
    }
}
