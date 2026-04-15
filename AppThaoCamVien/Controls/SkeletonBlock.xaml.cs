namespace AppThaoCamVien.Controls;

public partial class SkeletonBlock : ContentView
{
    public static readonly BindableProperty SkeletonWidthProperty =
        BindableProperty.Create(nameof(SkeletonWidth), typeof(double), typeof(SkeletonBlock), -1d);

    public static readonly BindableProperty SkeletonHeightProperty =
        BindableProperty.Create(nameof(SkeletonHeight), typeof(double), typeof(SkeletonBlock), 16d);

    private CancellationTokenSource? _animationCts;

    public double SkeletonWidth
    {
        get => (double)GetValue(SkeletonWidthProperty);
        set => SetValue(SkeletonWidthProperty, value);
    }

    public double SkeletonHeight
    {
        get => (double)GetValue(SkeletonHeightProperty);
        set => SetValue(SkeletonHeightProperty, value);
    }

    public SkeletonBlock()
    {
        InitializeComponent();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is null)
        {
            _animationCts?.Cancel();
            return;
        }

        _animationCts = new CancellationTokenSource();
        _ = AnimateSkeletonAsync(_animationCts.Token);
    }

    private async Task AnimateSkeletonAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    RootBorder.BackgroundColor = Color.FromArgb("#E6E9E6");
                    await RootBorder.FadeTo(0.65, 550, Easing.Linear);
                    await RootBorder.FadeTo(1, 550, Easing.Linear);
                });
            }
            catch
            {
                break;
            }
        }
    }
}
