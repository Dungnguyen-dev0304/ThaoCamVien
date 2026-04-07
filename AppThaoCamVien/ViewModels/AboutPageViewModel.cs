using AppThaoCamVien.Services;
using AppThaoCamVien.Services.Api;
using AppThaoCamVien.ViewModels.Core;

namespace AppThaoCamVien.ViewModels;

public sealed class AboutPageViewModel : BaseViewModel
{
    private readonly ApiService _api;
    private List<AboutSectionDto> _sections = [];

    public List<AboutSectionDto> Sections
    {
        get => _sections;
        private set => SetProperty(ref _sections, value);
    }

    public AboutPageViewModel(ApiService api)
    {
        _api = api;
        EmptyMessage = "Chưa có thông tin giới thiệu từ CMS.";
    }

    protected override async Task LoadAsync()
    {
        var sections = await _api.GetAsync<List<AboutSectionDto>>(ApiEndpoints.AboutSections) ?? [];
        Sections = sections;

        if (sections.Count == 0)
        {
            State = UiState.Empty;
            return;
        }

        State = UiState.Success;
    }
}
