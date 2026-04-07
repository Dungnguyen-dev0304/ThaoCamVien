using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services;

public static class OnboardingNavigationHelper
{
    public const string PrefOnboardingDone = "OnboardingCompleted";

    public static void CompleteToShell(IServiceProvider sp)
    {
        Preferences.Set(PrefOnboardingDone, true);
        Application.Current!.MainPage = new AppShell(sp);
    }
}
