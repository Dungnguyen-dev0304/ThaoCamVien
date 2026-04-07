using AppThaoCamVien.Services;
using AppThaoCamVien.Services.Api;
using AppThaoCamVien.ViewModels.Core;

namespace AppThaoCamVien.ViewModels;

public sealed class StoryAudioViewModel : BaseViewModel
{
    private readonly ApiService _api;
    private List<VoiceDto> _voices = [];
    private List<LyricLineDto> _lyrics = [];
    private int _poiId;
    private string? _selectedVoiceId;

    public List<VoiceDto> Voices
    {
        get => _voices;
        private set => SetProperty(ref _voices, value);
    }

    public List<LyricLineDto> Lyrics
    {
        get => _lyrics;
        private set => SetProperty(ref _lyrics, value);
    }

    public string? SelectedVoiceId
    {
        get => _selectedVoiceId;
        set => SetProperty(ref _selectedVoiceId, value);
    }

    public StoryAudioViewModel(ApiService api)
    {
        _api = api;
        EmptyMessage = "Chưa có dữ liệu voice hoặc lời thoại đồng bộ.";
    }

    public void SetPoiContext(int poiId)
    {
        _poiId = poiId;
    }

    protected override async Task LoadAsync()
    {
        var lang = LanguageManager.Current;
        var voices = await _api.GetAsync<List<VoiceDto>>($"{ApiEndpoints.Voices}?lang={lang}") ?? [];
        Voices = voices;

        if (string.IsNullOrEmpty(SelectedVoiceId))
            SelectedVoiceId = voices.Count > 0 ? voices[0].VoiceId : null;

        List<LyricLineDto> lyrics = [];
        if (_poiId > 0 && !string.IsNullOrEmpty(SelectedVoiceId))
        {
            lyrics = await _api.GetAsync<List<LyricLineDto>>(
                          $"{ApiEndpoints.LyricsTimeline}?poiId={_poiId}&voiceId={SelectedVoiceId}")
                      ?? [];
        }

        Lyrics = lyrics;

        if (voices.Count == 0 && lyrics.Count == 0)
        {
            State = UiState.Empty;
            return;
        }

        State = UiState.Success;
    }
}
