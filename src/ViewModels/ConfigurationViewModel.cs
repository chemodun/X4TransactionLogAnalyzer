using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Threading;
using X4PlayerShipTradeAnalyzer.Services;
using X4PlayerShipTradeAnalyzer.Utils; // added for ResilientFileWatcher
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class ConfigurationViewModel : INotifyPropertyChanged
{
  private readonly ConfigurationService _cfg = ConfigurationService.Instance;
  private ResilientFileWatcher? _saveWatcher; // watcher for auto reload
  private DateTime _lastAutoLoadUtc = DateTime.MinValue; // debounce auto loads
  private string? _lastLoadedFile; // track last loaded save file
  private readonly TimeSpan _autoLoadDebounce = TimeSpan.FromSeconds(2);

  private string? _gameFolderExePath;
  public string? GameFolderExePath
  {
    get => _gameFolderExePath;
    set
    {
      if (_gameFolderExePath == value)
        return;
      _gameFolderExePath = value;
      _cfg.GameFolderExePath = value;
      _cfg.Save();
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanReloadGameData));
    }
  }

  private string? _GameSavePath;
  public string? GameSavePath
  {
    get => _GameSavePath;
    set
    {
      if (_GameSavePath == value)
        return;
      var oldFolder = string.IsNullOrWhiteSpace(_GameSavePath) ? null : Path.GetDirectoryName(_GameSavePath);
      _GameSavePath = value;
      _cfg.GameSavePath = value;
      _cfg.Save();
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanReloadSaveData));
      if (oldFolder != Path.GetDirectoryName(_GameSavePath) && _autoReloadMode != AutoReloadGameSaveMode.None)
      {
        // Save folder changed; restart watcher if needed
        RestartSaveWatcherIfNeeded();
      }
    }
  }

  private bool _loadOnlyGameLanguage;
  public bool LoadOnlyGameLanguage
  {
    get => _loadOnlyGameLanguage;
    set
    {
      if (_loadOnlyGameLanguage == value)
        return;
      _loadOnlyGameLanguage = value;
      _cfg.LoadOnlyGameLanguage = value;
      _cfg.Save();
      OnPropertyChanged();
    }
  }

  private bool _loadRemovedObjects;
  public bool LoadRemovedObjects
  {
    get => _loadRemovedObjects;
    set
    {
      if (_loadRemovedObjects == value)
        return;
      _loadRemovedObjects = value;
      _cfg.LoadRemovedObjects = value;
      _cfg.Save();
      OnPropertyChanged();
      // Visual gating may depend on stats treated differently; update CanReloadSaveData
      OnPropertyChanged(nameof(CanReloadSaveData));
    }
  }

  private string _appTheme;
  public string AppTheme
  {
    get => _appTheme;
    set
    {
      if (_appTheme == value)
        return;
      _appTheme = value;
      _cfg.AppTheme = value;
      _cfg.Save();
      ApplyTheme(value);
      OnPropertyChanged();
    }
  }

  public void RefreshStats()
  {
    TryUpdateStats(MainWindow.GameData);
  }

  public bool CanReloadGameData => !string.IsNullOrWhiteSpace(GameFolderExePath);

  // Enable only if save path is set AND base game data is loaded (key stats > 0)
  public bool CanReloadSaveData => !string.IsNullOrWhiteSpace(GameSavePath) && GameDataStatsReady;

  private bool GameDataStatsReady =>
    WaresCount > 0
    && FactionsCount > 0
    && ClusterSectorNamesCount > 0
    && StoragesCount > 0
    && ShipStoragesCount > 0
    && LanguagesCount > 0
    && CurrentLanguageId > 0
    && CurrentLanguageTextCount > 0;

  // Stats
  private int _waresCount;
  public int WaresCount
  {
    get => _waresCount;
    private set
    {
      if (_waresCount != value)
      {
        _waresCount = value;
        OnPropertyChanged();
      }
    }
  }
  private int _playerShipsCount;
  public int PlayerShipsCount
  {
    get => _playerShipsCount;
    private set
    {
      if (_playerShipsCount != value)
      {
        _playerShipsCount = value;
        OnPropertyChanged();
      }
    }
  }
  private int _stationsCount;
  public int StationsCount
  {
    get => _stationsCount;
    private set
    {
      if (_stationsCount != value)
      {
        _stationsCount = value;
        OnPropertyChanged();
      }
    }
  }
  private int _removedObjectCount;
  public int RemovedObjectCount
  {
    get => _removedObjectCount;
    private set
    {
      if (_removedObjectCount != value)
      {
        _removedObjectCount = value;
        OnPropertyChanged();
      }
    }
  }
  private int _tradesCount;
  public int TradesCount
  {
    get => _tradesCount;
    private set
    {
      if (_tradesCount != value)
      {
        _tradesCount = value;
        OnPropertyChanged();
      }
    }
  }

  private int _factionsCount;
  public int FactionsCount
  {
    get => _factionsCount;
    private set
    {
      if (_factionsCount != value)
      {
        _factionsCount = value;
        OnPropertyChanged();
      }
    }
  }

  private int _gatesCount;
  public int GatesCount
  {
    get => _gatesCount;
    private set
    {
      if (_gatesCount != value)
      {
        _gatesCount = value;
        OnPropertyChanged();
      }
    }
  }

  private int _subordinateCount;
  public int SubordinateCount
  {
    get => _subordinateCount;
    private set
    {
      if (_subordinateCount != value)
      {
        _subordinateCount = value;
        OnPropertyChanged();
      }
    }
  }

  private int _clusterSectorNamesCount;
  public int ClusterSectorNamesCount
  {
    get => _clusterSectorNamesCount;
    private set
    {
      if (_clusterSectorNamesCount != value)
      {
        _clusterSectorNamesCount = value;
        OnPropertyChanged();
      }
    }
  }

  private int _languagesCount;
  public int LanguagesCount
  {
    get => _languagesCount;
    private set
    {
      if (_languagesCount != value)
      {
        _languagesCount = value;
        OnPropertyChanged();
      }
    }
  }

  private int _storagesCount;
  public int StoragesCount
  {
    get => _storagesCount;
    private set
    {
      if (_storagesCount != value)
      {
        _storagesCount = value;
        OnPropertyChanged();
      }
    }
  }

  private int _shipStoragesCount;
  public int ShipStoragesCount
  {
    get => _shipStoragesCount;
    private set
    {
      if (_shipStoragesCount != value)
      {
        _shipStoragesCount = value;
        OnPropertyChanged();
      }
    }
  }

  private int _currentLanguageTextCount;
  public int CurrentLanguageTextCount
  {
    get => _currentLanguageTextCount;
    private set
    {
      if (_currentLanguageTextCount != value)
      {
        _currentLanguageTextCount = value;
        OnPropertyChanged();
      }
    }
  }

  private int _currentLanguageId;
  public int CurrentLanguageId
  {
    get => _currentLanguageId;
    private set
    {
      if (_currentLanguageId != value)
      {
        _currentLanguageId = value;
        OnPropertyChanged();
      }
    }
  }

  public ConfigurationViewModel()
  {
    GameFolderExePath = _cfg.GameFolderExePath;
    GameSavePath = _cfg.GameSavePath;
    LoadOnlyGameLanguage = _cfg.LoadOnlyGameLanguage;
    LoadRemovedObjects = _cfg.LoadRemovedObjects;
    _appTheme = _cfg.AppTheme;
    _autoReloadMode = _cfg.AutoReloadMode;
    // apply saved theme on startup
    ApplyTheme(_appTheme);
    // Setup watcher if needed based on loaded config
    RestartSaveWatcherIfNeeded();
  }

  // Helpers invoked from code-behind button clicks
  public void ReloadGameData(GameData gameData, Action<ProgressUpdate>? progress = null)
  {
    if (!CanReloadGameData || string.IsNullOrWhiteSpace(GameFolderExePath))
      return;
    try
    {
      gameData.LoadGameXmlFiles(progress);
      TryUpdateStats(gameData);
    }
    catch
    {
      // swallow for now (preliminary feature)
    }
  }

  public void ReloadSaveData(GameData gameData, Action<ProgressUpdate>? progress = null)
  {
    if (!CanReloadSaveData || string.IsNullOrWhiteSpace(GameSavePath))
      return;
    try
    {
      gameData.ImportSaveGame(progress);
      TryUpdateStats(gameData);
    }
    catch
    {
      // swallow for now (preliminary feature)
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  private void TryUpdateStats(GameData gameData)
  {
    try
    {
      WaresCount = gameData.Stats.WaresCount;
      PlayerShipsCount = gameData.Stats.PlayerShipsCount;
      StationsCount = gameData.Stats.StationsCount;
      RemovedObjectCount = gameData.Stats.RemovedObjectCount;
      TradesCount = gameData.Stats.TradesCount;
      GatesCount = gameData.Stats.GatesCount;
      SubordinateCount = gameData.Stats.SubordinateCount;
      FactionsCount = gameData.Stats.FactionsCount;
      ClusterSectorNamesCount = gameData.Stats.ClusterSectorNamesCount;
      StoragesCount = gameData.Stats.StoragesCount;
      ShipStoragesCount = gameData.Stats.ShipStoragesCount;
      LanguagesCount = gameData.Stats.LanguagesCount;
      CurrentLanguageTextCount = gameData.Stats.CurrentLanguageTextCount;
      CurrentLanguageId = gameData.Stats.CurrentLanguageId;
      // Stats affect CanReloadSaveData; notify binding to re-evaluate
      OnPropertyChanged(nameof(CanReloadSaveData));
    }
    catch
    { /* ignore */
    }
  }

  private static void ApplyTheme(string theme)
  {
    // Valid: "System", "Light", "Dark" mapped to ThemeVariant
    var app = Application.Current;
    if (app is null)
      return;

    if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
    {
      app.RequestedThemeVariant = ThemeVariant.Light;
    }
    else if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
    {
      app.RequestedThemeVariant = ThemeVariant.Dark;
    }
    else // System
    {
      app.RequestedThemeVariant = ThemeVariant.Default; // follow OS
    }

    // After switching theme, refresh README to ensure viewer picks up the new theme
    try
    {
      if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life && life.MainWindow is MainWindow win)
      {
        // Ensure on UI thread
        Dispatcher.UIThread.Post(win.LoadReadme);
        Dispatcher.UIThread.Post(win.ApplyThemeOnCharts);
      }
    }
    catch { }
  }

  // Auto reload game save mode
  private AutoReloadGameSaveMode _autoReloadMode;
  public AutoReloadGameSaveMode AutoReloadMode
  {
    get => _autoReloadMode;
    set
    {
      if (_autoReloadMode == value)
        return;
      var oldMode = _autoReloadMode;
      _autoReloadMode = value;
      _cfg.AutoReloadMode = value;
      _cfg.Save();
      OnPropertyChanged();
      // update convenience properties
      OnPropertyChanged(nameof(AutoReloadNone));
      OnPropertyChanged(nameof(AutoReloadSelectedFile));
      OnPropertyChanged(nameof(AutoReloadAnyFile));
      // Restart watcher if needed based on new mode
      if ((oldMode == AutoReloadGameSaveMode.None || value == AutoReloadGameSaveMode.None) && _GameSavePath != null)
      {
        RestartSaveWatcherIfNeeded();
      }
    }
  }

  // Convenience boolean properties for RadioButtons binding
  public bool AutoReloadNone
  {
    get => AutoReloadMode == AutoReloadGameSaveMode.None;
    set
    {
      if (value)
        AutoReloadMode = AutoReloadGameSaveMode.None;
    }
  }
  public bool AutoReloadSelectedFile
  {
    get => AutoReloadMode == AutoReloadGameSaveMode.SelectedFile;
    set
    {
      if (value)
        AutoReloadMode = AutoReloadGameSaveMode.SelectedFile;
    }
  }
  public bool AutoReloadAnyFile
  {
    get => AutoReloadMode == AutoReloadGameSaveMode.AnyFile;
    set
    {
      if (value)
        AutoReloadMode = AutoReloadGameSaveMode.AnyFile;
    }
  }

  private void RestartSaveWatcherIfNeeded()
  {
    // Stop existing always first
    _saveWatcher?.Stop();
    _saveWatcher?.Dispose();
    _saveWatcher = null;

    if (_autoReloadMode == AutoReloadGameSaveMode.None)
      return; // nothing to watch

    if (string.IsNullOrWhiteSpace(GameSavePath))
      return; // no path

    try
    {
      var dir = Path.GetDirectoryName(GameSavePath);
      if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        return;

      // watch *.xml.gz in save folder
      _saveWatcher = new ResilientFileWatcher(dir, "*.xml.gz", includeSubdirectories: false, debounceMilliseconds: 500);
      _saveWatcher.Renamed += OnSaveFileChanged;
      // _saveWatcher.Created += OnSaveFileChanged;
      // _saveWatcher.Changed += OnSaveFileChanged; // some systems modify in-place
      _saveWatcher.Start();
    }
    catch
    {
      // swallow - watcher optional
    }
  }

  private void OnSaveFileChanged(object? sender, FileSystemEventArgs e)
  {
    // Ensure we are allowed to reload and avoid rapid re-imports
    if (!CanReloadSaveData)
      return;

    // We only care about .xml.gz already by filter, but double-check
    if (!e.FullPath.EndsWith(".xml.gz", StringComparison.OrdinalIgnoreCase))
      return;

    var fileName = Path.GetFileName(e.FullPath);

    // Determine if this file should trigger reload
    bool shouldReload = false;

    if (_autoReloadMode == AutoReloadGameSaveMode.SelectedFile)
    {
      var target = Path.GetFileName(GameSavePath ?? string.Empty);
      if (!string.IsNullOrEmpty(target) && string.Equals(target, fileName, StringComparison.OrdinalIgnoreCase))
        shouldReload = true;
    }
    else if (_autoReloadMode == AutoReloadGameSaveMode.AnyFile)
    {
      // quicksave.xml.gz OR autosave_01..03.xml.gz OR save_001..010.xml.gz
      if (string.Equals(fileName, "quicksave.xml.gz", StringComparison.OrdinalIgnoreCase))
      {
        shouldReload = true;
      }
      else if (
        fileName.StartsWith("autosave_", StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(".xml.gz", StringComparison.OrdinalIgnoreCase)
      )
      {
        // extract number between autosave_ and .xml.gz
        var middle = fileName.Substring(9, fileName.Length - 9 - 7); // autosave_ = 9 chars, .xml.gz = 7
        if (int.TryParse(middle, out var autoNum) && autoNum >= 1 && autoNum <= 3)
          shouldReload = true;
      }
      else if (
        fileName.StartsWith("save_", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".xml.gz", StringComparison.OrdinalIgnoreCase)
      )
      {
        var middle = fileName.Substring(5, fileName.Length - 5 - 7); // save_ = 5
        if (int.TryParse(middle, out var saveNum) && saveNum >= 1 && saveNum <= 10)
          shouldReload = true;
      }

      if (shouldReload)
      {
        // Update GameSavePath if a different file triggered it
        if (!string.Equals(GameSavePath, e.FullPath, StringComparison.OrdinalIgnoreCase))
        {
          // Set without recursion causing duplicate watcher restart (watcher already will be recreated but safe)
          GameSavePath = e.FullPath; // this will persist config
        }
      }
    }

    if (!shouldReload)
      return;

    var now = DateTime.UtcNow;
    if (_lastLoadedFile == e.FullPath && (now - _lastAutoLoadUtc) < _autoLoadDebounce)
      return; // debounce same file rapid events

    _lastLoadedFile = e.FullPath;
    _lastAutoLoadUtc = now;

    // Perform reload on UI thread
    Dispatcher.UIThread.Post(() =>
    {
      try
      {
        if (!CanReloadSaveData)
          return; // re-check after dispatch delay
        // Call the MainWindow handler to reuse progress UI logic
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life && life.MainWindow is MainWindow win)
        {
          // Simulate button click pathway (sender null, event args empty)
          var mi = typeof(MainWindow).GetMethod(
            "ReloadSaveData_Click",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
          );
          mi?.Invoke(win, new object?[] { null, new Avalonia.Interactivity.RoutedEventArgs() });
        }
        else
        {
          // Fallback: direct reload (should rarely execute)
          ReloadSaveData(MainWindow.GameData, null);
        }
      }
      catch { }
    });
  }
}
