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
        Margin = new Thickness(12, 0, 12, 14);

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
            var best = -1;
            for (int i = 0; i < Items.Count; i++)
            {
                var r = Items[i].Route.TrimStart('/');
                if (loc.Contains(r, StringComparison.OrdinalIgnoreCase))
                {
                    best = i;
                    break;
                }
            }
            if (best >= 0 && best != SelectedIndex) SelectedIndex = best;
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
