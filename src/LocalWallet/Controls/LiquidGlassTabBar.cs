using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace LocalWallet.Controls;

/// <summary>
/// Floating Liquid-Glass tab bar inspired by iOS 26.
///
/// Visual layers (back to front):
///   0. RealBlurBackdrop   — a plain Border whose Android handler applies a
///                           real RenderEffect blur at runtime (Android 12+).
///                           Older Android falls back to a static tinted fill.
///   1. TintOverlay        — semi-translucent white/dark that unifies the look
///                           across varied backgrounds.
///   2. SpecularRim        — 1px LinearGradient highlighting the top edge.
///   3. IdleShimmer        — slow linear gradient that sweeps across every
///                           ~7s at ~6% opacity. This is the "drop shimmer".
///   4. ActivePill         — accent-tinted sub-pill that slides with spring
///                           easing when the selected tab changes.
///   5. Tabs               — per-item icon + label, tap → command.
///
/// No SKImageFilter or per-frame blur sampling — blur is a single native
/// call configured once. Animations: spring translate + a single long-lived
/// shimmer animator. Perf target: 60fps idle, no battery hit.
/// </summary>
public class LiquidGlassTabBar : ContentView
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IList<TabBarItem>), typeof(LiquidGlassTabBar),
        defaultValueCreator: _ => new ObservableCollection<TabBarItem>(),
        propertyChanged: (b, _, _) => ((LiquidGlassTabBar)b).Rebuild());

    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex), typeof(int), typeof(LiquidGlassTabBar), 0, BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((LiquidGlassTabBar)b).OnSelectionChanged((int)o, (int)n));

    public static readonly BindableProperty TabTappedCommandProperty = BindableProperty.Create(
        nameof(TabTappedCommand), typeof(ICommand), typeof(LiquidGlassTabBar));

    public IList<TabBarItem> Items
    {
        get => (IList<TabBarItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public ICommand? TabTappedCommand
    {
        get => (ICommand?)GetValue(TabTappedCommandProperty);
        set => SetValue(TabTappedCommandProperty, value);
    }

    public Border BlurBackdropHost { get; private set; } = null!;

    private readonly Border _root;
    private readonly Grid _tabsGrid;
    private readonly Border _activePill;
    private readonly BoxView _shimmerSweep;
    private readonly List<View> _tabViews = new();

    public LiquidGlassTabBar()
    {
        HeightRequest = 64;

        _tabsGrid = new Grid
        {
            ColumnSpacing = 0,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(6, 4)
        };

        _activePill = new Border
        {
            BackgroundColor = Color.FromArgb("#2E7D32"),
            Stroke = Color.FromArgb("#40FFFFFF"),
            StrokeThickness = 1,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Fill,
            WidthRequest = 60,
            Margin = new Thickness(0, 8, 0, 8),
            Opacity = 0.92,
            InputTransparent = true,
            StrokeShape = new RoundRectangle { CornerRadius = 16 }
        };

        // Idle shimmer: a wide soft-white gradient that slowly pans across.
        _shimmerSweep = new BoxView
        {
            Color = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true,
            Opacity = 0
        };

        // Layered root.
        BlurBackdropHost = new Border
        {
            BackgroundColor = Color.FromArgb("#80000000"), // fallback tint when blur unavailable
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 28 }
        };

        var tintOverlay = new Border
        {
            BackgroundColor = Color.FromArgb("#59FFFFFF"), // ~35% white tint
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 28 },
            InputTransparent = true
        };

        var specularRim = new Border
        {
            Stroke = Colors.Transparent,
            BackgroundColor = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 28 },
            InputTransparent = true,
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop { Offset = 0f, Color = Color.FromArgb("#40FFFFFF") },
                    new GradientStop { Offset = 0.08f, Color = Color.FromArgb("#14FFFFFF") },
                    new GradientStop { Offset = 1f, Color = Colors.Transparent }
                },
                new Point(0, 0), new Point(0, 1))
        };

        var layered = new Grid();
        layered.Children.Add(BlurBackdropHost);
        layered.Children.Add(tintOverlay);
        layered.Children.Add(_shimmerSweep);
        layered.Children.Add(_activePill);
        layered.Children.Add(_tabsGrid);
        layered.Children.Add(specularRim);

        _root = new Border
        {
            Stroke = Color.FromArgb("#33FFFFFF"),
            StrokeThickness = 1,
            BackgroundColor = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 28 },
            Content = layered,
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Opacity = 0.25f,
                Radius = 22,
                Offset = new Point(0, 10)
            }
        };

        Content = _root;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Rebuild();
        StartShimmerLoop();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        this.AbortAnimation("shimmer");
    }

    private void Rebuild()
    {
        _tabsGrid.Children.Clear();
        _tabsGrid.ColumnDefinitions.Clear();
        _tabViews.Clear();

        if (Items is null || Items.Count == 0) return;

        for (int i = 0; i < Items.Count; i++)
            _tabsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var capturedIndex = i;
            var tab = BuildTab(item, isSelected: i == SelectedIndex);
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => OnTabTapped(capturedIndex);
            tab.GestureRecognizers.Add(tap);
            Grid.SetColumn(tab, i);
            _tabsGrid.Children.Add(tab);
            _tabViews.Add(tab);
        }

        LayoutActivePill(animate: false);
    }

    private View BuildTab(TabBarItem item, bool isSelected)
    {
        var iconColor = isSelected ? Colors.White : Color.FromArgb("#627267");
        var labelColor = isSelected ? Colors.White : Color.FromArgb("#627267");

        var icon = new Label
        {
            Text = item.Glyph,
            FontFamily = "MatIcon",
            FontSize = 22,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TextColor = iconColor
        };

        var label = new Label
        {
            Text = item.Title,
            FontSize = 10,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TextColor = labelColor,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        return new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill,
            Children = { icon, label }
        };
    }

    private void OnSelectionChanged(int oldIndex, int newIndex)
    {
        if (_tabViews.Count == 0) return;
        RecolorTabs();
        LayoutActivePill(animate: oldIndex != newIndex);
    }

    private void RecolorTabs()
    {
        for (int i = 0; i < _tabViews.Count; i++)
        {
            if (_tabViews[i] is VerticalStackLayout stack)
            {
                var selected = i == SelectedIndex;
                if (stack.Children.Count >= 2 &&
                    stack.Children[0] is Label icon &&
                    stack.Children[1] is Label label)
                {
                    var c = selected ? Colors.White : Color.FromArgb("#627267");
                    icon.TextColor = c;
                    label.TextColor = c;
                }
            }
        }
    }

    private void LayoutActivePill(bool animate)
    {
        if (_tabViews.Count == 0 || SelectedIndex < 0 || SelectedIndex >= _tabViews.Count) return;

        // Position by columns because tabs share equal-width columns.
        var totalWidth = Width > 0 ? Width : 360;
        var tabWidth = totalWidth / _tabViews.Count;
        var pillWidth = Math.Min(tabWidth - 12, 80);
        var targetX = (SelectedIndex * tabWidth) + (tabWidth - pillWidth) / 2;

        _activePill.WidthRequest = pillWidth;

        if (!animate)
        {
            _activePill.TranslationX = targetX;
            return;
        }

        _activePill.AbortAnimation("move");
        var startX = _activePill.TranslationX;
        var anim = new Animation(v => _activePill.TranslationX = v, startX, targetX, Easing.SpringOut);
        anim.Commit(_activePill, "move", length: 320);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        LayoutActivePill(animate: false);
        _shimmerSweep.WidthRequest = Math.Max(120, width * 0.4);
    }

    private void OnTabTapped(int index)
    {
        if (index == SelectedIndex) { PulseSelected(); return; }
        SelectedIndex = index;
        TabTappedCommand?.Execute(Items[index]);
        PulseSelected();
    }

    private void PulseSelected()
    {
        _activePill.AbortAnimation("pulse");
        var pulse = new Animation();
        pulse.Add(0, 0.5, new Animation(v => _activePill.Scale = v, 1.0, 1.08, Easing.CubicOut));
        pulse.Add(0.5, 1, new Animation(v => _activePill.Scale = v, 1.08, 1.0, Easing.CubicIn));
        pulse.Commit(_activePill, "pulse", length: 220);
    }

    private void StartShimmerLoop()
    {
        this.AbortAnimation("shimmer");

        _shimmerSweep.Color = Color.FromArgb("#66FFFFFF");
        _shimmerSweep.Opacity = 0;

        var totalMs = 7000u;
        var anim = new Animation();

        // Fade in -> pan -> fade out.
        anim.Add(0.0, 0.08, new Animation(v => _shimmerSweep.Opacity = v, 0, 0.14));
        anim.Add(0.0, 1.0, new Animation(v =>
        {
            var width = Width > 0 ? Width : 360;
            _shimmerSweep.TranslationX = -_shimmerSweep.Width + v * (width + _shimmerSweep.Width);
        }, 0, 1, Easing.CubicInOut));
        anim.Add(0.85, 1.0, new Animation(v => _shimmerSweep.Opacity = v, 0.14, 0));

        anim.Commit(this, "shimmer", 16, totalMs, repeat: () => true);
    }
}

public class TabBarItem : BindableObject
{
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
        nameof(Glyph), typeof(string), typeof(TabBarItem), string.Empty);
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(TabBarItem), string.Empty);
    public static readonly BindableProperty RouteProperty = BindableProperty.Create(
        nameof(Route), typeof(string), typeof(TabBarItem), string.Empty);

    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Route { get => (string)GetValue(RouteProperty); set => SetValue(RouteProperty, value); }
}
