using System.ComponentModel;
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
using MarkdownViewer.Core.Controls;
using X4PlayerShipTradeAnalyzer.Services;
using X4PlayerShipTradeAnalyzer.ViewModels;

namespace X4PlayerShipTradeAnalyzer.Views;

public partial class MainWindow : Window
{
  private static GameData? _gameData;
  public static GameData GameData => _gameData ??= new GameData();
  private TabItem? _currentTab;
  private bool _didStartupStatsCheck;
  private TabItem? _byTransactionsTab;
  private TabItem? _transactionsTab;
  private TabItem? _transactionsGraphsTab;
  private TabItem? _transactionsWaresStatsTab;
  private TabItem? _transactionsShipsWaresStatsTab;
  private TabItem? _byTradesTab;
  private TabItem? _tradesTab;
  private TabItem? _tradesGraphsTab;
  private TabItem? _tradesWaresStatsTab;
  private TabItem? _configurationTab;
  private TabItem? _readmeTab;

  public MainWindow()
  {
    InitializeComponent();
#if DEBUG
    this.AttachDevTools();
#endif

    DataContext = new MainViewModel();

    // React to configuration changes affecting tab enablement
    if (DataContext is MainViewModel vm && vm.Configuration != null)
    {
      vm.Configuration.PropertyChanged += Configuration_PropertyChanged;
    }

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

    _currentTab = this.FindControl<TabItem>("ByTransactionsTab");
    _byTransactionsTab = this.FindControl<TabItem>("ByTransactionsTab");
    _transactionsTab = this.FindControl<TabItem>("TransactionsTab");
    _transactionsGraphsTab = this.FindControl<TabItem>("TransactionsGraphsTab");
    _transactionsWaresStatsTab = this.FindControl<TabItem>("TransactionsWaresStatsTab");
    _transactionsShipsWaresStatsTab = this.FindControl<TabItem>("TransactionsShipsWaresStatsTab");
    _byTradesTab = this.FindControl<TabItem>("ByTradesTab");
    _tradesTab = this.FindControl<TabItem>("TradesTab");
    _tradesGraphsTab = this.FindControl<TabItem>("TradesGraphsTab");
    _tradesWaresStatsTab = this.FindControl<TabItem>("TradesWaresStatsTab");
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

    UpdateTabsEnabled();

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
    bool removedRequired = ConfigurationService.Instance.LoadRemovedObjects;
    return s.WaresCount == 0
      || s.FactionsCount == 0
      || s.ClusterSectorNamesCount == 0
      || s.PlayerShipsCount == 0
      || s.StationsCount == 0
      || (removedRequired && s.RemovedObjectCount == 0)
      || s.TradesCount == 0
      || s.LanguagesCount == 0
      || s.CurrentLanguageId == 0
      || s.CurrentLanguageTextCount == 0;
  }

  private void UpdateTabsEnabled()
  {
    bool dataReady = !AnyStatsMissing();
    // Keep Configuration always enabled; toggle other tabs
    if (_byTransactionsTab != null)
      _byTransactionsTab.IsEnabled = dataReady;
    if (_byTradesTab != null)
      _byTradesTab.IsEnabled = dataReady;
    if (_transactionsTab != null)
      _transactionsTab.IsEnabled = dataReady;
    if (_transactionsGraphsTab != null)
      _transactionsGraphsTab.IsEnabled = dataReady;
    if (_tradesGraphsTab != null)
      _tradesGraphsTab.IsEnabled = dataReady;
    if (_tradesTab != null)
      _tradesTab.IsEnabled = dataReady;
    if (_transactionsWaresStatsTab != null)
      _transactionsWaresStatsTab.IsEnabled = dataReady;
    if (_transactionsShipsWaresStatsTab != null)
      _transactionsShipsWaresStatsTab.IsEnabled = dataReady;
    if (_configurationTab != null)
      _configurationTab.IsEnabled = true;
    if (_readmeTab != null)
      _readmeTab.IsEnabled = true;
  }

  private void Configuration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(ConfigurationViewModel.LoadRemovedObjects))
    {
      UpdateTabsEnabled();
    }
  }

  // Toggle a ship's series on double-click in Ships Graphs list
  private void ShipGraphsList_DoubleTapped(object? sender, RoutedEventArgs e)
  {
    if (sender is not ListBox lb)
      return;
    if (lb.DataContext is not ShipsGraphsBaseModel model)
      return;
    if (e is not TappedEventArgs tea)
      return;

    if (tea.Source is Control c)
    {
      var container = c as ListBoxItem ?? c.FindAncestorOfType<ListBoxItem>();
      if (container?.DataContext is ShipsGraphsBaseModel.GraphShipItem ship)
        model.ToggleShip(ship);
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

    // If we have an already set Game Folder, set its folder as the initial location
    if (!string.IsNullOrWhiteSpace(vm.Configuration?.GameFolderExePath))
    {
      var currentFolder = new DirectoryInfo(vm.Configuration.GameFolderExePath);
      if (currentFolder.Exists)
      {
        options.SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(currentFolder.FullName);
      }
    }
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

    // If we have a saved path, set its folder as the initial location
    if (!string.IsNullOrWhiteSpace(vm.Configuration?.GameSavePath))
    {
      var currentFile = new FileInfo(vm.Configuration.GameSavePath);
      if (currentFile.Exists || currentFile.Directory?.Exists == true)
      {
        options.SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(currentFile.Directory!.FullName);
      }
    }

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
      UpdateTabsEnabled();
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
      UpdateTabsEnabled();
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
      case "ShipsTransactionsGraphsTab":
        // do not refresh
        break;
      case "ShipsTransactionsWaresStatsTab":
        // do not refresh
        break;
      case "ShipsTradesTab":
        // do not refresh
        break;
      case "ShipsTradesGraphsTab":
        // do not refresh
        break;
      case "ConfigurationTab":
        vm?.Configuration?.RefreshStats();
        break;
      case "ReadmeTab":
        // ensure content is present
        // LoadReadme();
        break;
    }
  }

  public void LoadReadme()
  {
    try
    {
      // Remove any existing viewer
      var scroll = this.FindControl<ScrollViewer>("ReadmeScrollViewer");
      if (scroll == null)
        return; // UI not ready yet

      var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
      var readmePath = Path.Combine(exeDir, "README.md");

      if (File.Exists(readmePath))
      {
        // Create a new MarkdownViewer instance
        var viewer = new MarkdownViewer.Core.Controls.MarkdownViewer { MarkdownText = File.ReadAllText(readmePath), IsEnabled = true };

        // Insert into the ScrollViewer
        scroll.Content = viewer;
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
