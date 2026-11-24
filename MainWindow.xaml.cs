using log4net;
using log4net.Core;

using LoggerSystem.Core;
using LoggerSystem.Integration;
using LoggerSystem.Services;
using LoggerSystem.ViewModels;

using System.Windows;

namespace LoggerSystem;

public partial class MainWindow : Window
{
    private readonly LoggerSettingsManager _settingsManager = new();
    private readonly LogViewModel _logViewModel;
    private readonly LoggerRuntime _runtime;
    private readonly LoggerSettingsViewModel _settingsViewModel;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        _logViewModel = new LogViewModel(Dispatcher, new LogExportService());
        _runtime = new LoggerRuntime(_logViewModel);
        _settingsViewModel = new LoggerSettingsViewModel(_settingsManager, _runtime, _logViewModel);
        LogViewer.DataContext = _logViewModel;
        LoggerSettingsView.DataContext = _settingsViewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var config = await _settingsManager.LoadAsync();
            _settingsViewModel.SetConfig(config);
            _logViewModel.MaxEntries = config.MaximumListViewEntries;
            _logViewModel.AutoScroll = config.AutoScroll;

            await _runtime.ApplyAsync(config);

            // The existing legacy Logger API and the new MVVM LogViewer share one repository.
            ILog log = Logger.GetLog<MainWindow>();
            LegacyLoggerBridge.SetCategory("STARTUP");
            log.Info("BewerbungsAssiLogger initialized successfully.");
            log.Debug("Legacy LoggerFactory and MVVM LogViewModel are connected.");
            log.Info("BewerbungsAssiLogger initialized successfully.");
            log.Info("BewerbungsAssiLogger initialized successfully.");
            log.Info("BewerbungsAssiLogger initialized successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Logger initialization error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnTestLog(object sender, RoutedEventArgs e)
    {
        ILog log = Logger.GetLog<MainWindow>();
        log.Info("Test message from legacy LoggerFactory.");
        log.Warn("Test warning from legacy LoggerFactory.");
        LegacyLoggerBridge.LogToViewer("Manual ListView-only event", Level.Info);
    }

    protected override void OnClosed(EventArgs e)
    {
        _runtime.Dispose();
        Logger.Shutdown();
        base.OnClosed(e);
    }
}
