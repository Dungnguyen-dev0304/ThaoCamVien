using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class NumpadPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly IServiceProvider _sp;
    private string _input = "";
    private const int MAX = 3;

    public NumpadPage(DatabaseService db, IServiceProvider sp)
    {
        InitializeComponent();
        _db = db;
        _sp = sp;
    }

    protected override void OnAppearing() { base.OnAppearing(); Reset(); }

    private void OnNumberClicked(object s, EventArgs e)
    {
        if (_input.Length >= MAX) return;
        _input += ((Button)s).Text;
        Render(); HideErr();
    }

    private void OnBackspaceClicked(object s, EventArgs e)
    {
        if (_input.Length > 0) { _input = _input[..^1]; Render(); HideErr(); }
    }

    private async void OnConfirmClicked(object s, EventArgs e)
    {
        if (_input.Length == 0) { ShowErr("Vui lòng nhập mã số."); return; }
        if (!int.TryParse(_input, out int id)) { ShowErr("Mã không hợp lệ."); return; }

        try
        {
            var poi = await _db.GetPoiByIdAsync(id);
            if (poi?.IsActive == true)
            {
                await NavigateAsync(poi);
            }
            else
            {
                ShowErr($"Không tìm thấy mã '{_input}'.");
                await ShakeAsync();
                Reset();
            }
        }
        catch (Exception ex) { ShowErr(ex.Message); }
    }

    private async Task NavigateAsync(Poi poi)
    {
        // Phát narration ngầm
        var narration = _sp.GetService<NarrationEngine>();
        _ = narration?.PlayAsync(poi, forcePlay: true);

        var page = _sp.GetService<StoryAudioPage>();
        if (page != null) { page.LoadPoi(poi); await Navigation.PushAsync(page); }
        Reset();
    }

    private void Render()
    {
        if (_input.Length == 0) { CodeDisplay.Text = "_ _ _"; CodeDisplay.TextColor = Color.FromArgb("#BDBDBD"); return; }
        var s = ""; for (int i = 0; i < MAX; i++) { s += i < _input.Length ? _input[i].ToString() : "_"; if (i < MAX - 1) s += " "; }
        CodeDisplay.Text = s; CodeDisplay.TextColor = Color.FromArgb("#2E7D32");
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
