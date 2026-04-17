namespace LocalWallet.Services;

public static class UiAlerts
{
    public static async Task ShowAsync(string title, string message, string accept = "OK")
    {
        try
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null) return;
            await page.DisplayAlertAsync(title, message, accept);
        }
        catch
        {
            // Never let a failed alert take the app down.
        }
    }

    public static async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        try
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null) return false;
            return await page.DisplayAlertAsync(title, message, accept, cancel);
        }
        catch
        {
            return false;
        }
    }

    public static bool TrySetRootPage(Page newPage)
    {
        try
        {
            var window = Application.Current?.Windows?.FirstOrDefault();
            if (window is null) return false;
            window.Page = newPage;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
