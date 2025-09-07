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
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class ConfigurationViewModel : INotifyPropertyChanged
{
  private readonly ConfigurationService _cfg = ConfigurationService.Instance;

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
      _GameSavePath = value;
      _cfg.GameSavePath = value;
      _cfg.Save();
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanReloadSaveData));
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
    _appTheme = _cfg.AppTheme;
    // apply saved theme on startup
    ApplyTheme(_appTheme);
    // Initial stats
    // TryUpdateStats(MainWindow.GameData);
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
      FactionsCount = gameData.Stats.FactionsCount;
      ClusterSectorNamesCount = gameData.Stats.ClusterSectorNamesCount;
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
      }
    }
    catch { }
  }
}
