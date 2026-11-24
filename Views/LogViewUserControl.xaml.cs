using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace LoggerSystem.Views;

public partial class LogViewUserControl : UserControl
{
    public LogViewUserControl() { InitializeComponent(); Loaded += (_, _) => Attach(); Unloaded += (_, _) => Detach(); }
    public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.RegisterAttached("AutoScroll", typeof(bool), typeof(LogViewUserControl), new PropertyMetadata(true));
    public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);
    public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);
    INotifyCollectionChanged? _source;
    void Attach()
    {
        Detach();
        _source = LogList.ItemsSource as INotifyCollectionChanged;
        if (_source != null) _source.CollectionChanged += OnChanged;
    }
    void Detach() { if (_source != null) _source.CollectionChanged -= OnChanged; _source = null; }
    void OnChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (!GetAutoScroll(LogList)) return;
        LogList.Dispatcher.BeginInvoke(new Action(() => { if (LogList.Items.Count > 0) LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]); }), DispatcherPriority.Background);
    }
}
