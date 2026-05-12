using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class NumpadPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly IServiceProvider _sp;
    private string _input = "";
    private const int MAX = 3;
    private bool _navigating = false;

    public NumpadPage(DatabaseService db, IServiceProvider sp)
    {
        InitializeComponent();
        _db = db;
        _sp = sp;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _navigating = false;
        Reset();
    }

    private void OnNumberClicked(object s, EventArgs e)
    {
        if (_input.Length >= MAX || _navigating) return;
        _input += ((Button)s).Text;
        Render(); HideErr();
    }

    private void OnBackspaceClicked(object s, EventArgs e)
    {
        if (_input.Length > 0 && !_navigating) { _input = _input[..^1]; Render(); HideErr(); }
    }

    /// <summary>
    /// Nhấn OK → tìm POI theo số đã nhập.
    /// Nhập "1" → tìm POI ID 1, nhập "01" hoặc "001" → cũng tìm POI ID 1.
    /// Nếu không tìm được theo ID → thử tìm theo QR data trong bảng qr_codes.
    /// </summary>
    private async void OnConfirmClicked(object s, EventArgs e)
    {
        if (_navigating) return;
        if (_input.Length == 0) { ShowErr("Vui lòng nhập mã số."); return; }
        if (!int.TryParse(_input, out int id) || id <= 0)
        {
            ShowErr("Mã không hợp lệ.");
            return;
        }

        try
        {
            _navigating = true;

            // Tìm theo POI ID trực tiếp (nhập 1 → POI 1, nhập 001 → POI 1)
            var poi = await _db.GetPoiByIdAsync(id);
            if (poi?.IsActive == true)
            {
                await NavigateAsync(poi);
                return;
            }

            // Fallback: tìm theo QR data (nếu có bản ghi trong bảng qr_codes)
            var padded = _input.PadLeft(3, '0'); // "1" → "001"
            var qrPoi = await _db.GetPoiByQrDataAsync(padded);
            if (qrPoi == null) qrPoi = await _db.GetPoiByQrDataAsync($"TCV-{padded}");
            if (qrPoi == null) qrPoi = await _db.GetPoiByQrDataAsync($"TCV-{id}");

            if (qrPoi?.IsActive == true)
            {
                await NavigateAsync(qrPoi);
                return;
            }

            // Không tìm thấy
            ShowErr($"Không tìm thấy con vật số {id}.");
            await ShakeAsync();
            Reset();
        }
        catch (Exception ex) { ShowErr(ex.Message); }
        finally { _navigating = false; }
    }

    private async Task NavigateAsync(Poi poi)
    {
        // KHÔNG phát narration tại đây — để StoryAudioPage tự kiểm tra Premium
        // gate trước rồi mới quyết định phát hay không. Trước đây gọi
        // narration.PlayAsync(forcePlay:true) ở đây làm audio bypass gate.
        var page = _sp.GetService<StoryAudioPage>();
        if (page != null) { page.LoadPoi(poi); await Navigation.PushAsync(page); }
        Reset();
    }

    private void Render()
    {
        if (_input.Length == 0)
        {
            CodeDisplay.Text = "_ _ _";
            CodeDisplay.TextColor = Color.FromArgb("#BDBDBD");
            return;
        }
        var s = "";
        for (int i = 0; i < MAX; i++)
        {
            s += i < _input.Length ? _input[i].ToString() : "_";
            if (i < MAX - 1) s += " ";
        }
        CodeDisplay.Text = s;
        CodeDisplay.TextColor = Color.FromArgb("#2E7D32");
    }

    private void Reset() { _input = ""; Render(); HideErr(); }
    private void ShowErr(string m) { ErrorLabel.Text = m; ErrorLabel.IsVisible = true; }
    private void HideErr() => ErrorLabel.IsVisible = false;

    private async Task ShakeAsync()
    {
        for (int i = 0; i < 4; i++)
        {
            await CodeDisplay.TranslateTo(-8, 0, 50);
            await CodeDisplay.TranslateTo(8, 0, 50);
        }
        await CodeDisplay.TranslateTo(0, 0, 50);
    }

    private async void OnBackClicked(object s, EventArgs e) => await Navigation.PopAsync();
}
