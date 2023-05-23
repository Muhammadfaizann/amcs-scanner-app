namespace amcs_scanner_app.Helper;
public class BackgroundTask : BindableObject
{
    private readonly TimeSpan _interval;
    private readonly Func<Task> _action;
    private bool _running;
    /// <summary>
    /// 
    /// </summary>
    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(
        nameof(IsRunning),
        typeof(bool),
        typeof(BackgroundTask),
        false,
        BindingMode.TwoWay);

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public BackgroundTask(TimeSpan interval, Func<Task> action)
    {
        _interval = interval;
        _action = action;
    }

    /// <summary>
    /// 
    /// </summary>
    public void Start()
    {
        _running = true;
        IsRunning = true;

        Task.Run(async () =>
        {
            while (_running)
            {
                await _action();
                await Task.Delay(_interval);
            }
        });
    }

    /// <summary>
    /// 
    /// </summary>
    public void Stop()
    {
        _running = false;
        IsRunning = false;
    }
}
