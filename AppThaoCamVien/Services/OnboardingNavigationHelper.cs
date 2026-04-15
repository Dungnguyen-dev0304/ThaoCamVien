using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services;

public static class OnboardingNavigationHelper
{
    public const string PrefOnboardingDone = "OnboardingCompleted";

    public static void CompleteToShell(IServiceProvider sp)
    {
        Preferences.Set(PrefOnboardingDone, true);

        // MAUI 10: dùng Window.Page thay vì Application.MainPage (obsolete)
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            window.Page = new AppShell(sp);
        }
        else
        {
            // Fallback (không nên xảy ra, nhưng safe)
#pragma warning disable CS0618 // Suppress obsolete warning cho edge case
            Application.Current!.MainPage = new AppShell(sp);
#pragma warning restore CS0618
        }
    }
}
