using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AppThaoCamVien.ViewModels.Core;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private UiState _state = UiState.Loading;
    private string _errorMessage = string.Empty;
    private string _emptyMessage = string.Empty;
    private bool _isRefreshing;

    public UiState State
    {
        get => _state;
        protected set => SetProperty(ref _state, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        protected set => SetProperty(ref _errorMessage, value);
    }

    public string EmptyMessage
    {
        get => _emptyMessage;
        protected set => SetProperty(ref _emptyMessage, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public ICommand RetryCommand { get; }

    protected BaseViewModel()
    {
        RetryCommand = new Command(async () => await SafeReloadAsync());
    }

    protected abstract Task LoadAsync();

    public async Task SafeReloadAsync()
    {
        try
        {
            State = UiState.Loading;
            ErrorMessage = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            State = UiState.Error;
            ErrorMessage = ex.Message;
        }
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
