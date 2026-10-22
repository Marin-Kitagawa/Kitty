using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Kitty;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private string _world = "riso";
    private bool _syncingWorld;
    private bool _syncingRange;
    private bool _syncingSource;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        ApplyWorld(SettingsService.Instance.Current.World, persist: false);
        if (!string.Equals(SettingsService.Instance.Current.World, _world, StringComparison.Ordinal))
        {
            SettingsService.Instance.Current.World = _world;
            SettingsService.Instance.Save();
        }

        PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty)
                SyncWindowState();
        };

        Opened += (_, _) =>
        {
            // Avalonia 12 only populates generated x:Name fields once the window
            // is attached, so every named control is resolved from here on.
            if (TopLevel.GetTopLevel(this) is { } tl)
                _vm.ClipboardSetter = text =>
                {
                    var data = new DataTransfer();
                    data.Add(DataTransferItem.CreateText(text));
                    _ = tl.Clipboard?.SetDataAsync(data);
                };

            SyncWindowState();
            SyncWorldControls();
            SyncRangeControls();
            SyncSourceRadios();
            UpdateOverlayStates();
            PostRailScroll();
            PlayArrival();
        };

        _vm.ListArrived += PlayArrival;
        _vm.VisibleRepos.CollectionChanged += (_, _) => PostOverlayStates();
        _vm.VisibleLanguages.CollectionChanged += (_, _) => PostRailScroll();
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        KeyDown += OnKeyDown;
    }

    // ── visual worlds: color only ──

    private void ApplyWorld(string id, bool persist)
    {
        var world = Worlds.Apply(Application.Current!, id);
        _world = world.Id;

        if (!persist) return;
        SettingsService.Instance.Current.World = world.Id;
        SettingsService.Instance.Save();
    }

    private void WorldRadioChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingWorld) return;
        if (sender is not RadioButton { Tag: string id, IsChecked: true }) return;

        ApplyWorld(id, persist: true);
        SyncWorldControls();
    }

    private void CycleWorld()
    {
        ApplyWorld(Worlds.Next(_world).Id, persist: true);
        SyncWorldControls();
    }

    private void SyncWorldControls()
    {
        _syncingWorld = true;
        try
        {
            if (WorldRiso is null) return;
            WorldRiso.IsChecked = _world == "riso";
            WorldShelf.IsChecked = _world == "shelf";
            WorldBoard.IsChecked = _world == "board";
            WorldTape.IsChecked = _world == "tape";
            WorldProgramme.IsChecked = _world == "programme";
            WorldCanon.IsChecked = _world == "canon";
        }
        finally { _syncingWorld = false; }
    }

    // ── range + source ──

    private void RangeChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingRange) return;
        if (_vm is null || sender is not RadioButton { Tag: string name, IsChecked: true }) return;
        _vm.Range = Enum.Parse<TrendRange>(name);
    }

    private void SyncRangeControls()
    {
        _syncingRange = true;
        try
        {
            SegDaily.IsChecked = _vm.Range == TrendRange.Daily;
            SegWeekly.IsChecked = _vm.Range == TrendRange.Weekly;
            SegMonthly.IsChecked = _vm.Range == TrendRange.Monthly;
        }
        finally { _syncingRange = false; }
    }

    private void SyncSourceRadios()
    {
        _syncingSource = true;
        try
        {
            var scrape = _vm.ScrapePrimary;
            SrcApi.IsChecked = !scrape;
            SrcScrape.IsChecked = scrape;
            SrcApiSheet.IsChecked = !scrape;
            SrcScrapeSheet.IsChecked = scrape;
        }
        finally { _syncingSource = false; }
    }

    private void SourceModeChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingSource) return;
        if (sender is not RadioButton { Tag: string tag, IsChecked: true }) return;

        var scrape = tag == "scrape";
        if (_vm.ScrapePrimary == scrape) return;

        _vm.ScrapePrimary = scrape;
        _vm.RefreshCommand.Execute(null);
    }

    // ── overlays, rail, arrival ──

    private void PostOverlayStates() => Dispatcher.UIThread.Post(UpdateOverlayStates);

    private void UpdateOverlayStates()
    {
        if (EmptyState is null || LoadingState is null) return;
        EmptyState.IsVisible = _vm.VisibleRepos.Count == 0 && !_vm.IsLoading;
        LoadingState.IsVisible = _vm.IsLoading;
    }

    private void PostRailScroll() => Dispatcher.UIThread.Post(() =>
    {
        if (RailList is null || _vm.SelectedLanguage is not { } language) return;
        RailList.ScrollIntoView(language);
    }, DispatcherPriority.Background);

    private int _arrivalGeneration;

    private void PlayArrival()
    {
        var generation = ++_arrivalGeneration;
        Dispatcher.UIThread.Post(() => PlayArrivalStep(generation), DispatcherPriority.Loaded);
    }

    private void PlayArrivalStep(int generation)
    {
        if (generation != _arrivalGeneration || RepoList?.ItemsPanelRoot is not { } panel) return;

        var containers = panel.Children;
        for (var i = 0; i < containers.Count; i++)
        {
            if (containers[i] is not Control row) continue;

            var delay = Math.Min(i, 14) * 22;
            row.Opacity = 0;
            row.RenderTransform = new TranslateTransform(0, 8);

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(240),
                Easing = new CubicEaseOut(),
                Delay = TimeSpan.FromMilliseconds(delay),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0.0), Setters = {
                        new Setter { Property = Visual.OpacityProperty, Value = 0.0 },
                        new Setter { Property = TranslateTransform.YProperty, Value = 8.0 } } },
                    new KeyFrame { Cue = new Cue(1.0), Setters = {
                        new Setter { Property = Visual.OpacityProperty, Value = 1.0 },
                        new Setter { Property = TranslateTransform.YProperty, Value = 0.0 } } }
                }
            };
            _ = animation.RunAsync(row);
        }
    }

    // ── settings panel ──

    private void OpenSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (SettingsOverlay is null) return;
        SettingsOverlay.IsVisible = true;
        CloseSettingsButton?.Focus();
    }

    private void CloseSettingsClick(object? sender, RoutedEventArgs e) => CloseSettings();

    private void SettingsBackdropClick(object? sender, PointerPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, SettingsOverlay)) return;
        CloseSettings();
    }

    private void CloseSettings()
    {
        if (SettingsOverlay is null) return;
        SettingsOverlay.IsVisible = false;
        FilterBox?.Focus();
    }

    private void ClearFiltersClick(object? sender, RoutedEventArgs e)
    {
        _vm.Filter = "";
        _vm.RailFilter = "";
        FilterBox?.Focus();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Range):
                SyncRangeControls();
                break;
            case nameof(MainViewModel.IsLoading):
                PostOverlayStates();
                break;
            case nameof(MainViewModel.SelectedLanguage):
                PostRailScroll();
                break;
            case nameof(MainViewModel.ScrapePrimary):
                SyncSourceRadios();
                break;
        }
    }

    // ── window chrome ──

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void TitleBar_DoubleTapped(object? sender, TappedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void MinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseClick(object? sender, RoutedEventArgs e) => Close();

    private void SyncWindowState()
    {
        if (MaximizeGlyph is null || RestoreGlyph is null) return;
        var maximized = WindowState == WindowState.Maximized;
        MaximizeGlyph.IsVisible = !maximized;
        RestoreGlyph.IsVisible = maximized;
    }

    // ── keys ──

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SettingsOverlay is { IsVisible: true })
        {
            CloseSettings();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.F5)
        {
            _vm.RefreshCommand.Execute(null);
            e.Handled = true;
        }
        else if ((e.Key is Key.W or Key.D) &&
                 (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) ==
                 (KeyModifiers.Control | KeyModifiers.Shift))
        {
            CycleWorld();
            e.Handled = true;
        }
        else if (e.Key == Key.G &&
                 (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) ==
                 (KeyModifiers.Control | KeyModifiers.Shift))
        {
            _vm.SignInCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            FilterBox.Focus();
            FilterBox.SelectAll();
            e.Handled = true;
        }
    }
}
