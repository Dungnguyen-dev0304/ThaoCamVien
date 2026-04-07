using System.Windows.Input;
using AppThaoCamVien.ViewModels.Core;

namespace AppThaoCamVien.Controls;

public partial class StateContainer : ContentView
{
    public static readonly BindableProperty StateProperty =
        BindableProperty.Create(nameof(State), typeof(UiState), typeof(StateContainer), UiState.Success, propertyChanged: OnStateChanged);

    public static readonly BindableProperty EmptyMessageProperty =
        BindableProperty.Create(nameof(EmptyMessage), typeof(string), typeof(StateContainer), "Không có dữ liệu", propertyChanged: OnMessageChanged);

    public static readonly BindableProperty ErrorMessageProperty =
        BindableProperty.Create(nameof(ErrorMessage), typeof(string), typeof(StateContainer), "Không tải được dữ liệu", propertyChanged: OnMessageChanged);

    public static readonly BindableProperty RetryCommandProperty =
        BindableProperty.Create(nameof(RetryCommand), typeof(ICommand), typeof(StateContainer));

    public static readonly BindableProperty SuccessContentProperty =
        BindableProperty.Create(nameof(SuccessContent), typeof(View), typeof(StateContainer), propertyChanged: OnSuccessContentChanged);

    public UiState State
    {
        get => (UiState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string EmptyMessage
    {
        get => (string)GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    public View? SuccessContent
    {
        get => (View?)GetValue(SuccessContentProperty);
        set => SetValue(SuccessContentProperty, value);
    }

    public StateContainer()
    {
        InitializeComponent();
        Render();
    }

    private static void OnStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((StateContainer)bindable).Render();
    }

    private static void OnMessageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((StateContainer)bindable).Render();
    }

    private static void OnSuccessContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var container = (StateContainer)bindable;
        container.SuccessPresenter.Content = newValue as View;
        container.Render();
    }

    private void Render()
    {
        SuccessPresenter.IsVisible = State == UiState.Success;
        LoadingPanel.IsVisible = State == UiState.Loading;
        EmptyPanel.IsVisible = State == UiState.Empty;
        ErrorPanel.IsVisible = State == UiState.Error;
        EmptyTextLabel.Text = EmptyMessage;
        ErrorTextLabel.Text = ErrorMessage;
    }

    private void OnRetryClicked(object? sender, EventArgs e)
    {
        if (RetryCommand?.CanExecute(null) == true)
            RetryCommand.Execute(null);
    }
}
