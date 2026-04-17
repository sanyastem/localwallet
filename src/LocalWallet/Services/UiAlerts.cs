using System.Diagnostics;

namespace LocalWallet.Services;

public static class UiAlerts
{
    public static async Task ShowAsync(string title, string message, string accept = "OK")
    {
        try
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null)
            {
                Debug.WriteLine($"[UiAlerts.ShowAsync] no page — {title}: {message}");
                return;
            }
            await page.DisplayAlertAsync(title, message, accept);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UiAlerts.ShowAsync] failed ({title}): {ex}");
        }
    }

    public static async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        try
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null)
            {
                Debug.WriteLine($"[UiAlerts.ConfirmAsync] no page — {title}: {message}");
                return false;
            }
            return await page.DisplayAlertAsync(title, message, accept, cancel);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UiAlerts.ConfirmAsync] failed ({title}): {ex}");
            return false;
        }
    }

    public static bool TrySetRootPage(Page newPage)
    {
        try
        {
            var window = Application.Current?.Windows?.FirstOrDefault();
            if (window is null)
            {
                Debug.WriteLine("[UiAlerts.TrySetRootPage] no window");
                return false;
            }
            window.Page = newPage;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UiAlerts.TrySetRootPage] failed: {ex}");
            return false;
        }
    }
}
