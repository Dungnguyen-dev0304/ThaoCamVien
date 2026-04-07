using AppThaoCamVien.Services;
using AppThaoCamVien.Services.Api;
using AppThaoCamVien.ViewModels.Core;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.ViewModels;

public sealed class QrPageViewModel : BaseViewModel
{
    private readonly ApiService _api;
    private readonly DatabaseService _db;
    private string _lastCode = string.Empty;
    private NearbyPoiDto? _resolvedPoi;
    private Poi? _resolvedPoiModel;

    public string LastCode
    {
        get => _lastCode;
        set => SetProperty(ref _lastCode, value);
    }

    public NearbyPoiDto? ResolvedPoi
    {
        get => _resolvedPoi;
        private set => SetProperty(ref _resolvedPoi, value);
    }

    public Poi? ResolvedPoiModel
    {
        get => _resolvedPoiModel;
        private set => SetProperty(ref _resolvedPoiModel, value);
    }

    public QrPageViewModel(ApiService api, DatabaseService db)
    {
        _api = api;
        _db = db;
        EmptyMessage = "Không tìm thấy mã QR trong hệ thống.";
    }

    protected override Task LoadAsync()
    {
        State = UiState.Success;
        return Task.CompletedTask;
    }

    public async Task ResolveQrAsync(string code)
    {
        LastCode = code;
        try
        {
            State = UiState.Loading;
            ResolvedPoi = null;
            ResolvedPoiModel = null;

            var lang = LanguageManager.Current;
            _db.CurrentLanguage = lang;

            var dto = await _api.PostAsync<QrLookupRequest, NearbyPoiDto>(
                ApiEndpoints.QrLookup,
                new QrLookupRequest { Code = code });
            if (dto == null)
            {
                State = UiState.Empty;
                return;
            }

            ResolvedPoi = dto;
            ResolvedPoiModel = await _db.GetPoiByIdAsync(dto.PoiId);

            if (ResolvedPoiModel == null)
            {
                State = UiState.Empty;
                return;
            }

            State = UiState.Success;
        }
        catch (Exception ex)
        {
            State = UiState.Error;
            ErrorMessage = ex.Message;
        }
    }
}
