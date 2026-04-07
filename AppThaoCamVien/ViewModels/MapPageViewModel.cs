using AppThaoCamVien.Services;
using AppThaoCamVien.ViewModels.Core;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.ViewModels;

public sealed class MapPageViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private List<Poi> _pois = [];

    public List<Poi> Pois
    {
        get => _pois;
        private set => SetProperty(ref _pois, value);
    }

    public MapPageViewModel(DatabaseService db)
    {
        _db = db;
        EmptyMessage = "Không tìm thấy điểm tham quan nào.";
    }

    protected override async Task LoadAsync()
    {
        var lang = LanguageManager.Current;
        _db.CurrentLanguage = lang;
        await _db.SyncDataFromApiAsync();
        Pois = await _db.GetAllPoisAsync();

        if (Pois.Count == 0)
        {
            State = UiState.Empty;
            return;
        }

        State = UiState.Success;
    }
}
