using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using X4PlayerShipTradeAnalyzer.Services;
using X4PlayerShipTradeAnalyzer.ViewModels;

namespace X4PlayerShipTradeAnalyzer.Views;

public partial class MainWindow : Window
{
  private static GameData? _gameData;
  public static GameData GameData => _gameData ??= new GameData();
  private TabItem? _currentTab;
  private bool _didStartupStatsCheck;
  private TabItem? _shipsTransactionsTab;
  private TabItem? _shipsGraphsTab;
  private TabItem? _waresStatsTab;
  private TabItem? _configurationTab;
  private TabItem? _readmeTab;

  public MainWindow()
  {
    InitializeComponent();
#if DEBUG
    this.AttachDevTools();
#endif

    DataContext = new MainViewModel();

    // Set base title from assembly metadata (Product + Version)
    try
    {
      var asm = Assembly.GetExecutingAssembly();
      var product = asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
      var rawInfoVersion =
        asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? asm.GetName().Version?.ToString();
      var infoVersion = SanitizeVersion(rawInfoVersion);
      if (!string.IsNullOrWhiteSpace(product))
      {
        Title = string.IsNullOrWhiteSpace(infoVersion) ? product : $"{product} v{infoVersion}";
      }
    }
    catch { }

    // Run a one-time check on first open to direct user to Configuration if stats look empty
    this.Opened += MainWindow_Opened;

    _currentTab = this.FindControl<TabItem>("ShipsTransactionsTab");
    _shipsTransactionsTab = this.FindControl<TabItem>("ShipsTransactionsTab");
    _shipsGraphsTab = this.FindControl<TabItem>("ShipsGraphsTab");
    _waresStatsTab = this.FindControl<TabItem>("WaresStatsTab");
    _configurationTab = this.FindControl<TabItem>("ConfigurationTab");
    _readmeTab = this.FindControl<TabItem>("ReadmeTab");
    this.Opened += (_, __) => LoadReadme();
  }

  private void InitializeComponent()
  {
    AvaloniaXamlLoader.Load(this);
  }

  private void MainWindow_Opened(object? sender, System.EventArgs e)
  {
    if (_didStartupStatsCheck)
      return;
    _didStartupStatsCheck = true;

    if (DataContext is not MainViewModel vm)
      return;

    try
    {
      GameData.RefreshStats();
    }
    catch { }
    vm.Configuration?.RefreshStats();

    bool anyZero = AnyStatsMissing();

    UpdateTabsEnabled(!anyZero);

    if (anyZero)
    {
      var tabs = this.FindControl<TabControl>("MainTabs");
      if (tabs != null && _configurationTab != null)
      {
        tabs.SelectedItem = _configurationTab;
      }
    }
  }

  private static bool AnyStatsMissing()
  {
    var s = GameData.Stats;
    return s.WaresCount == 0
      || s.FactionsCount == 0
      || s.ClusterSectorNamesCount == 0
      || s.PlayerShipsCount == 0
      || s.StationsCount == 0
      || s.TradesCount == 0
      || s.LanguagesCount == 0
      || s.CurrentLanguageId == 0
      || s.CurrentLanguageTextCount == 0;
  }

  private void UpdateTabsEnabled(bool dataReady)
  {
    // Keep Configuration always enabled; toggle other tabs
    if (_shipsTransactionsTab != null)
      _shipsTransactionsTab.IsEnabled = dataReady;
    if (_shipsGraphsTab != null)
      _shipsGraphsTab.IsEnabled = dataReady;
    if (_waresStatsTab != null)
      _waresStatsTab.IsEnabled = dataReady;
    if (_configurationTab != null)
      _configurationTab.IsEnabled = true;
    if (_readmeTab != null)
      _readmeTab.IsEnabled = true;
  }

  // Toggle a ship's series on double-click in Ships Graphs list
  private void ShipGraphsList_DoubleTapped(object? sender, RoutedEventArgs e)
  {
    if (sender is not ListBox lb || lb.DataContext is not ShipsGraphsModel model)
      return;
    if (e is not TappedEventArgs tea)
      return;

    if (tea.Source is Control c)
    {
      var container = c as ListBoxItem ?? c.FindAncestorOfType<ListBoxItem>();
      if (container?.DataContext is ShipsGraphsModel.ShipListItem ship)
      {
        model.ToggleShip(ship);
      }
    }
  }

  // Configuration: Set Game Folder (select X4.exe)
  private async void SetGameFolder_Click(object? sender, RoutedEventArgs e)
  {
    if (DataContext is not MainViewModel vm)
      return;

    var options = new FilePickerOpenOptions
    {
      Title = "Select X4.exe",
      AllowMultiple = false,
      FileTypeFilter = new[] { new FilePickerFileType("X4 Executable") { Patterns = new[] { "X4.exe", "X4" } } },
    };

    var files = await this.StorageProvider.OpenFilePickerAsync(options);
    if (files.Count > 0)
    {
      var selectedFile = files[0];
      var folderPath = Path.GetDirectoryName(selectedFile.Path.LocalPath);
      if (!string.IsNullOrWhiteSpace(folderPath) && vm.Configuration != null)
      {
        vm.Configuration.GameFolderExePath = folderPath;
      }
    }
  }

  // Configuration: Set Save Folder (select xml.gz)
  private async void SetSaveFolder_Click(object? sender, RoutedEventArgs e)
  {
    if (DataContext is not MainViewModel vm)
      return;

    var options = new FilePickerOpenOptions
    {
      Title = "Select xml.gz",
      AllowMultiple = false,
      FileTypeFilter = new[] { new FilePickerFileType("X4 Save Game") { Patterns = new[] { "*.xml.gz" } } },
    };

    var files = await this.StorageProvider.OpenFilePickerAsync(options);
    if (files.Count > 0)
    {
      var selectedFile = files[0];
      if (!string.IsNullOrWhiteSpace(selectedFile.Path.LocalPath) && vm.Configuration != null)
      {
        vm.Configuration.GameSavePath = selectedFile.Path.LocalPath;
      }
    }
  }

  // Configuration: Reload Game Data (wares.xml)
  private async void ReloadGameData_Click(object? sender, RoutedEventArgs e)
  {
    if (DataContext is not MainViewModel vm)
      return;
    var progress = new ProgressWindow { Title = "Loading game data...", CanResize = false };
    progress.SetMessage("Loading wares and base data... This may take a minute.");
    progress.WindowStartupLocation = WindowStartupLocation.CenterOwner;
    progress.Show(this);
    try
    {
      this.IsEnabled = false;
      progress.ApplyMode(ProgressWindow.ProgressMode.GameData);
      await Task.Run(() => vm?.Configuration?.ReloadGameData(GameData, u => progress.SetProgress(u)));
      vm.Refresh();
      GameData.RefreshStats();
      UpdateTabsEnabled(!AnyStatsMissing());
    }
    finally
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        progress.Close();
        this.IsEnabled = true;
      });
    }
  }

  // Configuration: Reload Save Data (quicksave)
  private async void ReloadSaveData_Click(object? sender, RoutedEventArgs e)
  {
    if (DataContext is not MainViewModel vm)
      return;
    var progress = new ProgressWindow { Title = "Loading save data...", CanResize = false };
    progress.SetMessage("Importing savegame, please wait...");
    progress.WindowStartupLocation = WindowStartupLocation.CenterOwner;
    progress.Show(this);
    try
    {
      this.IsEnabled = false;
      progress.ApplyMode(ProgressWindow.ProgressMode.SaveData);
      await Task.Run(() => vm?.Configuration?.ReloadSaveData(GameData, u => progress.SetProgress(u)));
      vm?.Refresh();
      GameData.RefreshStats();
      UpdateTabsEnabled(!AnyStatsMissing());
    }
    finally
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        progress.Close();
        this.IsEnabled = true;
      });
    }
  }

  // React on active tab change
  private void MainTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (DataContext is not MainViewModel vm)
      return;
    if (sender is not TabControl tc)
      return;
    if (tc.SelectedItem is not TabItem tab)
      return;

    if (_currentTab == tab)
      return;
    _currentTab = tab;
    var header = tab.Header?.ToString() ?? string.Empty;
    var name = tab.Name ?? string.Empty;

    // Keep base title (Product + Version) and append current tab
    string baseTitle = Title ?? "";
    int idx = baseTitle.IndexOf(" — ");
    if (idx >= 0)
      baseTitle = baseTitle.Substring(0, idx);
    Title = string.IsNullOrEmpty(header) ? baseTitle : $"{baseTitle} — {header}";

    switch (name)
    {
      case "ShipsTransactionsTab":
        // do not refresh
        break;
      case "ShipsGraphsTab":
        // do not refresh
        break;
      case "WaresStatsTab":
        // do not refresh
        break;
      case "ConfigurationTab":
        vm?.Configuration?.RefreshStats();
        break;
      case "ReadmeTab":
        // ensure content is present
        LoadReadme();
        break;
    }
  }

  private void LoadReadme()
  {
    try
    {
      var md = this.FindControl<Control>("ReadmeViewer");
      if (md is null)
        return;
      var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
      var readmePath = System.IO.Path.Combine(exeDir, "README.md");
      if (System.IO.File.Exists(readmePath))
      {
        // MarkdownViewer.Core exposes MarkdownText (string). Use reflection to avoid hard ref.
        if (md is Avalonia.Controls.Control c)
        {
          var prop = c.GetType().GetProperty("MarkdownText");
          prop?.SetValue(c, System.IO.File.ReadAllText(readmePath));
        }
      }
    }
    catch { }
  }

  private static string? SanitizeVersion(string? version)
  {
    if (string.IsNullOrWhiteSpace(version))
      return version;

    // Remove build metadata after '+' and any parenthetical suffix like " (abcdefg)"
    var v = version;
    var plus = v.IndexOf('+');
    if (plus >= 0)
      v = v.Substring(0, plus);
    var paren = v.IndexOf('(');
    if (paren > 0)
      v = v.Substring(0, paren).Trim();

    // Extract a SemVer-like pattern, optionally with a prerelease (but not build metadata)
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    var m = Regex.Match(v, "\\d+\\.\\d+(?:\\.\\d+)?(?:-[0-9A-Za-z.-]+)?");
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    return m.Success ? m.Value : v;
  }
}
