namespace LocalWallet.Controls;

/// <summary>
/// App-specific tab bar: pre-populates the five routes, listens to
/// <see cref="Shell.Current"/> navigations to sync the selected index,
/// and performs navigation when a tab is tapped.
///
/// This lets the ControlTemplate reuse the generic <see cref="LiquidGlassTabBar"/>
/// while keeping Shell routing knowledge out of the generic component.
/// </summary>
public class AppTabBar : LiquidGlassTabBar
{
    private static readonly (string Glyph, string Title, string Route)[] DefaultTabs =
    {
        ("\ue88a", "Главная",    "//dashboard"),
        ("\ue8b0", "Операции",   "//transactions"),
        ("\ue8d4", "Валюты",     "//multicurrency"),
        ("\ue0b7", "Чат",        "//chat"),
        ("\ue8b8", "Настройки",  "//settings"),
    };

    public AppTabBar()
    {
        // Side and bottom gutters live in the ControlTemplate's wrapper Grid.
        // Keeping them on this control breaks AbsoluteLayout's proportional
        // sizing (margin isn't subtracted from WidthProportional=1).

        Items = DefaultTabs.Select(t => new TabBarItem
        {
            Glyph = t.Glyph,
            Title = t.Title,
            Route = t.Route
        }).ToList();

        TabTappedCommand = new Command<TabBarItem>(OnTabSelected);

        Loaded += OnAttached;
        Unloaded += OnDetached;
    }

    private void OnAttached(object? sender, EventArgs e)
    {
        if (Shell.Current is { } shell)
        {
            shell.Navigated += OnShellNavigated;
            SyncSelectedFromShell();
        }
    }

    private void OnDetached(object? sender, EventArgs e)
    {
        if (Shell.Current is { } shell)
            shell.Navigated -= OnShellNavigated;
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e) => SyncSelectedFromShell();

    private void SyncSelectedFromShell()
    {
        try
        {
            var loc = Shell.Current?.CurrentState?.Location?.OriginalString ?? string.Empty;
            // Normalise "//chat/ChatPage?id=..." -> "chat" so we match the
            // top-level Shell route, not any substring deeper in the URL.
            var firstSegment = loc.TrimStart('/').Split('/', '?').FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrEmpty(firstSegment)) return;

            for (int i = 0; i < Items.Count; i++)
            {
                var route = Items[i].Route.TrimStart('/');
                if (string.Equals(route, firstSegment, StringComparison.OrdinalIgnoreCase))
                {
                    if (i != SelectedIndex) SelectedIndex = i;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppTabBar.Sync] {ex}");
        }
    }

    private static async void OnTabSelected(TabBarItem? item)
    {
        if (item is null) return;
        try
        {
            if (Shell.Current is null) return;
            await Shell.Current.GoToAsync(item.Route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppTabBar.Nav] {ex}");
        }
    }
}
