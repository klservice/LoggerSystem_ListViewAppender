using log4net.Core;

using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace LoggerSystem.Appenders;

/// <summary>Optional direct ListView attachment; the existing MVVM API remains available.</summary>
public sealed partial class LoggerListViewAppender
{
    private ListViewAttachment? _attachment;
    private int _uiClosed;

    /// <summary>Call on the ListView UI thread. Use an empty, unbound ListView.</summary>
    public void Attach(ListView listView, bool autoScroll = true)
    {
        ArgumentNullException.ThrowIfNull(listView);
        listView.Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _uiClosed) != 0, this);
        if (BindingOperations.IsDataBound(listView, ItemsControl.ItemsSourceProperty)
            || listView.ItemsSource is not null || listView.Items.Count > 0)
            throw new InvalidOperationException("Use an empty, unbound ListView, or bind EntriesView through LogViewModel.");
        var attachment = new ListViewAttachment(this, listView, autoScroll);
        Interlocked.Exchange(ref _attachment, attachment)?.Dispose();
        if (Volatile.Read(ref _uiClosed) != 0)
        {
            Detach();
            throw new ObjectDisposedException(nameof(LoggerListViewAppender));
        }
        attachment.Schedule();
    }

    /// <summary>Restores an empty ItemsSource; safe to call from another thread.</summary>
    public void Detach() => Interlocked.Exchange(ref _attachment, null)?.Dispose();

    partial void OnBufferChanged() => Volatile.Read(ref _attachment)?.Schedule();
    partial void OnUiClosed()
    {
        Interlocked.Exchange(ref _uiClosed, 1);
        Detach();
    }

    private sealed class ListViewAttachment : IDisposable
    {
        private readonly LoggerListViewAppender _owner;
        private readonly WeakReference<ListView> _view;
        private readonly Dispatcher _dispatcher;
        private readonly ObservableCollection<LoggingEvent> _items = new();
        private readonly ReadOnlyObservableCollection<LoggingEvent> _source;
        private readonly bool _autoScroll;
        private int _pending;
        private int _disposed;

        public ListViewAttachment(LoggerListViewAppender owner, ListView view, bool autoScroll)
        {
            _owner = owner;
            _view = new WeakReference<ListView>(view);
            _dispatcher = view.Dispatcher;
            _autoScroll = autoScroll;
            _source = new ReadOnlyObservableCollection<LoggingEvent>(_items);
            view.ItemsSource = _source;
        }

        public void Schedule()
        {
            if (Volatile.Read(ref _disposed) != 0 || _dispatcher.HasShutdownStarted
                || Interlocked.Exchange(ref _pending, 1) != 0) return;
            try
            {
                _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    Interlocked.Exchange(ref _pending, 0);
                    if (Volatile.Read(ref _disposed) != 0) return;
                    if (!_view.TryGetTarget(out var view)) { Dispose(); return; }
                    if (!ReferenceEquals(view.ItemsSource, _source)) { Dispose(); return; }
                    var snapshot = _owner.GetEvents();
                    // Retain overlapping rows and append new ones instead of rebuilding on each event.
                    int start = snapshot.Length == 0 ? -1 : _items.IndexOf(snapshot[0]);
                    if (start < 0) _items.Clear();
                    else for (int i = 0; i < start; i++) _items.RemoveAt(0);
                    int common = 0;
                    while (common < _items.Count && common < snapshot.Length
                        && ReferenceEquals(_items[common], snapshot[common])) common++;
                    while (_items.Count > common) _items.RemoveAt(_items.Count - 1);
                    for (int i = common; i < snapshot.Length; i++) _items.Add(snapshot[i]);
                    if (_autoScroll && _items.Count > 0) view.ScrollIntoView(_items[^1]);
                }));
            }
            catch (InvalidOperationException) { Interlocked.Exchange(ref _pending, 0); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            void Cleanup()
            {
                if (_view.TryGetTarget(out var view) && ReferenceEquals(view.ItemsSource, _source))
                    view.ItemsSource = null;
                _items.Clear();
            }
            // Never block a logging thread waiting for the UI thread.
            if (_dispatcher.CheckAccess()) Cleanup();
            else if (!_dispatcher.HasShutdownStarted)
            {
                try { _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Cleanup)); }
                catch (InvalidOperationException) { }
            }
        }
    }
}
