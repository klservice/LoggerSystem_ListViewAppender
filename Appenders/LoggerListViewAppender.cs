using log4net.Appender;
using log4net.Core;

using LoggerSystem.Interfaces;
using LoggerSystem.Models;

namespace LoggerSystem.Appenders;

/// <summary>Bounded live event buffer with optional direct WPF attachment.</summary>
public sealed partial class LoggerListViewAppender : AppenderSkeleton
{
    private readonly object _gate = new();
    public ILogEntrySink? Sink { get; set; }
    private readonly Queue<LoggingEvent> _events = new();
    private int _maxEntries = 1000;
    public event EventHandler? EntriesChanged;
    public int MaxEntries
    {
        get { lock (_gate) return _maxEntries; }
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            lock (_gate)
            {
                _maxEntries = value;
                while (_events.Count > value) _events.Dequeue();
            }
            NotifyChanged();
        }
    }
    //protected override void Append(LoggingEvent loggingEvent)
    //{
    //    // Materialize lazy data before the originating thread leaves its context.
    //    loggingEvent.Fix = FixFlags.All;
    //    lock (_gate)
    //    {
    //        _events.Enqueue(loggingEvent);
    //        while (_events.Count > _maxEntries) _events.Dequeue();
    //    }
    //    NotifyChanged();
    //}

    protected override void Append(LoggingEvent loggingEvent)
    {
        Sink?.AddEntry(new LogEntryModel
        {
            Timestamp = loggingEvent.TimeStamp,
            Level = loggingEvent.Level.Name,
            Logger = loggingEvent.LoggerName,
            Thread = loggingEvent.ThreadName,
            Message = loggingEvent.RenderedMessage,
            Exception = loggingEvent.GetExceptionString() ?? ""
        });
    }

    public LoggingEvent[] GetEvents() { lock (_gate) return _events.ToArray(); }
    public LoggingEvent[] Drain()
    {
        LoggingEvent[] snapshot;
        lock (_gate) { snapshot = _events.ToArray(); _events.Clear(); }
        NotifyChanged();
        return snapshot;
    }
    public void Clear()
    {
        lock (_gate) _events.Clear();
        NotifyChanged();
    }
    public void SetAmountOfMaxLogEntrys(int maxEntries) => MaxEntries = maxEntries;
    private void NotifyChanged()
    {
        OnBufferChanged();
        var handlers = EntriesChanged;
        if (handlers is null) return;
        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try { handler(this, EventArgs.Empty); }
            catch (Exception ex) { ErrorHandler.Error("Live log subscriber failed.", ex); }
        }
    }
    partial void OnBufferChanged();
    partial void OnUiClosed();

    protected override void OnClose()
    {
        OnUiClosed();
        EntriesChanged = null;
        base.OnClose();
    }
}
