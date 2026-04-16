using System.Diagnostics;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Regex nhận diện mã QR của Thảo Cầm Viên.
    /// Hỗ trợ các format: "TCV-1", "TCV-001", "tcv-12", "TCV1", "TCV001"
    /// Cũng hỗ trợ QR chứa số thuần (ví dụ: "1", "001", "12").
    /// </summary>
    private static readonly Regex QrPoiIdRegex = new(
        @"^(?:TCV[- ]?)?(\d{1,4})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

    /// <summary>
    /// Resolve mã QR → POI.
    /// Chiến lược offline-first:
    ///   1. Parse mã QR lấy POI ID (format: "TCV-1", "TCV-001", "1", "001")
    ///   2. Tìm trong SQLite local trước (nhanh, offline)
    ///   3. Nếu không tìm thấy → thử API lookup (online, hỗ trợ qr_codes table)
    /// </summary>
    public async Task ResolveQrAsync(string code)
    {
        LastCode = code?.Trim() ?? "";
        if (string.IsNullOrEmpty(LastCode))
        {
            State = UiState.Empty;
            return;
        }

        try
        {
            State = UiState.Loading;
            ResolvedPoi = null;
            ResolvedPoiModel = null;

            var lang = LanguageManager.Current;
            _db.CurrentLanguage = lang;

            // ── Bước 1: Parse mã QR → POI ID (local, instant) ──────────
            var match = QrPoiIdRegex.Match(LastCode);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int poiId) && poiId > 0)
            {
                Debug.WriteLine($"[QR] Parsed POI ID = {poiId} from code '{LastCode}'");

                var poi = await _db.GetPoiByIdAsync(poiId);
                if (poi?.IsActive == true)
                {
                    ResolvedPoiModel = poi;
                    ResolvedPoi = new NearbyPoiDto
                    {
                        PoiId = poi.PoiId,
                        Name = poi.Name ?? "",
                        ThumbnailUrl = poi.ImageThumbnail ?? ""
                    };
                    State = UiState.Success;
                    Debug.WriteLine($"[QR] Resolved locally: '{poi.Name}'");
                    return;
                }
            }

            // ── Bước 2: Thử lookup qua qr_codes table (local DB) ───────
            var poiByQr = await _db.GetPoiByQrDataAsync(LastCode);
            if (poiByQr?.IsActive == true)
            {
                ResolvedPoiModel = poiByQr;
                ResolvedPoi = new NearbyPoiDto
                {
                    PoiId = poiByQr.PoiId,
                    Name = poiByQr.Name ?? "",
                    ThumbnailUrl = poiByQr.ImageThumbnail ?? ""
                };
                State = UiState.Success;
                Debug.WriteLine($"[QR] Resolved via qr_codes table: '{poiByQr.Name}'");
                return;
            }

            // ── Bước 3: Fallback → API lookup (online) ──────────────────
            try
            {
                var dto = await _api.PostAsync<QrLookupRequest, NearbyPoiDto>(
                    ApiEndpoints.QrLookup,
                    new QrLookupRequest { Code = LastCode });

                if (dto != null)
                {
                    ResolvedPoi = dto;
                    ResolvedPoiModel = await _db.GetPoiByIdAsync(dto.PoiId);

                    if (ResolvedPoiModel != null)
                    {
                        State = UiState.Success;
                        Debug.WriteLine($"[QR] Resolved via API: '{ResolvedPoiModel.Name}'");
                        return;
                    }
                }
            }
            catch (Exception apiEx)
            {
                Debug.WriteLine($"[QR] API lookup failed (offline?): {apiEx.Message}");
                // Không throw — đã thử local rồi, chỉ log
            }

            // Không tìm thấy ở đâu cả
            State = UiState.Empty;
            Debug.WriteLine($"[QR] Code '{LastCode}' not found anywhere.");
        }
        catch (Exception ex)
        {
            State = UiState.Error;
            ErrorMessage = ex.Message;
            Debug.WriteLine($"[QR] ResolveQrAsync error: {ex.Message}");
        }
    }
}
