using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class NumpadPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private string _inputCode = "";
    private const int MAX_DIGITS = 3;

    public NumpadPage(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ResetInput();
    }

    private void OnNumberClicked(object sender, EventArgs e)
    {
        if (_inputCode.Length >= MAX_DIGITS) return;
        _inputCode += ((Button)sender).Text;
        UpdateDisplay();
        HideError();
    }

    private void OnBackspaceClicked(object sender, EventArgs e)
    {
        if (_inputCode.Length > 0)
        {
            _inputCode = _inputCode[..^1];
            UpdateDisplay();
            HideError();
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (_inputCode.Length == 0)
        {
            ShowError("Vui lòng nhập mã số.");
            return;
        }

        if (!int.TryParse(_inputCode, out int poiId))
        {
            ShowError("Mã không hợp lệ.");
            return;
        }

        try
        {
            // Tra cứu POI theo PoiId
            var poi = await _databaseService.GetPoiByIdAsync(poiId);
            if (poi != null && poi.IsActive)
            {
                await NavigateToAudioAsync(poi);
            }
            else
            {
                ShowError($"Không tìm thấy mã '{_inputCode}'. Kiểm tra lại!");
                await ShakeDisplay();
                ResetInput();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Lỗi: {ex.Message}");
        }
    }

    private async Task NavigateToAudioAsync(Poi poi)
    {
        var audioPage = IPlatformApplication.Current.Services.GetService<StoryAudioPage>();
        if (audioPage != null)
        {
            audioPage.LoadPoi(poi);
            await Navigation.PushAsync(audioPage);
        }
        ResetInput();
    }

    private void UpdateDisplay()
    {
        if (_inputCode.Length == 0)
        {
            CodeDisplay.Text = "_ _ _";
            CodeDisplay.TextColor = Color.FromArgb("#BDBDBD");
            return;
        }

        var display = "";
        for (int i = 0; i < MAX_DIGITS; i++)
        {
            display += i < _inputCode.Length ? _inputCode[i].ToString() : "_";
            if (i < MAX_DIGITS - 1) display += " ";
        }
        CodeDisplay.Text = display;
        CodeDisplay.TextColor = Color.FromArgb("#2E7D32");
    }

    private void ResetInput()
    {
        _inputCode = "";
        UpdateDisplay();
        HideError();
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.IsVisible = true;
    }

    private void HideError() => ErrorLabel.IsVisible = false;

    private async Task ShakeDisplay()
    {
        for (int i = 0; i < 4; i++)
        {
            await CodeDisplay.TranslateToAsync(-8, 0, 50);
            await CodeDisplay.TranslateToAsync(8, 0, 50);
        }
        await CodeDisplay.TranslateToAsync(0, 0, 50);
    }

    private async void OnBackClicked(object sender, EventArgs e)
        => await Navigation.PopAsync();
}