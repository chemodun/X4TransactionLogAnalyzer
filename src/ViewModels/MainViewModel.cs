using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using X4PlayerShipTradeAnalyzer.Models;
using X4PlayerShipTradeAnalyzer.Services;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
  public ObservableCollection<TradeOperation> Trades { get; } = new();
  public ObservableCollection<Ware> Wares { get; } = new();

  private ShipsDataTransactionsModel? _shipsTransactionsData;
  public ShipsDataTransactionsModel? ShipsTransactionsData
  {
    get => _shipsTransactionsData;
    set
    {
      _shipsTransactionsData = value;
      OnPropertyChanged();
    }
  }

  private ShipsGraphTransactionsModel? _shipsTransactionsGraphs;
  public ShipsGraphTransactionsModel? ShipsTransactionsGraphs
  {
    get => _shipsTransactionsGraphs;
    set
    {
      _shipsTransactionsGraphs = value;
      OnPropertyChanged();
    }
  }

  private WaresStatsTransactionsModel? _shipsTransactionsWaresStats;
  public WaresStatsTransactionsModel? ShipsTransactionsWaresStats
  {
    get => _shipsTransactionsWaresStats;
    set
    {
      _shipsTransactionsWaresStats = value;
      OnPropertyChanged();
    }
  }

  private ShipsDataTradesModel? _shipsTradesData;
  public ShipsDataTradesModel? ShipsTradesData
  {
    get => _shipsTradesData;
    set
    {
      _shipsTradesData = value;
      OnPropertyChanged();
    }
  }

  private ShipsGraphTradesModel? _shipsTradesGraphs;
  public ShipsGraphTradesModel? ShipsTradesGraphs
  {
    get => _shipsTradesGraphs;
    set
    {
      _shipsTradesGraphs = value;
      OnPropertyChanged();
    }
  }

  private WaresStatsTradesModel? _shipsTradesWaresStats;
  public WaresStatsTradesModel? ShipsTradesWaresStats
  {
    get => _shipsTradesWaresStats;
    set
    {
      _shipsTradesWaresStats = value;
      OnPropertyChanged();
    }
  }

  private ConfigurationViewModel? _configuration;
  public ConfigurationViewModel? Configuration
  {
    get => _configuration;
    set
    {
      _configuration = value;
      OnPropertyChanged();
    }
  }

  public MainViewModel()
  {
    Console.WriteLine($"Connection state: {MainWindow.GameData.Connection.State}");
    ShipsTransactionsData = new ShipsDataTransactionsModel();
    ShipsTransactionsGraphs = new ShipsGraphTransactionsModel();
    ShipsTransactionsWaresStats = new WaresStatsTransactionsModel();
    ShipsTradesData = new ShipsDataTradesModel();
    ShipsTradesGraphs = new ShipsGraphTradesModel();
    ShipsTradesWaresStats = new WaresStatsTradesModel();
    Configuration = new ConfigurationViewModel();
  }

  public void Refresh()
  {
    // Reload data for all models
    ShipsTransactionsData?.Refresh();
    ShipsTransactionsGraphs?.Refresh();
    ShipsTransactionsWaresStats?.Refresh();
    ShipsTradesGraphs?.Refresh();
    ShipsTradesData?.Refresh();
    ShipsTradesWaresStats?.Refresh();
    Configuration?.RefreshStats();
  }

  public static readonly SKColor[] Palette = new[]
  {
    SKColors.DodgerBlue,
    SKColors.OrangeRed,
    SKColors.MediumSeaGreen,
    SKColors.MediumOrchid,
    SKColors.Goldenrod,
    SKColors.CadetBlue,
    SKColors.Tomato,
    SKColors.DeepSkyBlue,
    SKColors.MediumVioletRed,
    SKColors.SlateBlue,
    SKColors.SteelBlue,
    SKColors.LightSeaGreen,
    SKColors.DarkKhaki,
    SKColors.IndianRed,
    SKColors.Teal,
    // Extra colors
    SKColors.Crimson,
    SKColors.Coral,
    SKColors.DarkCyan,
    SKColors.DarkOrange,
    SKColors.DarkSalmon,
    SKColors.ForestGreen,
    SKColors.HotPink,
    SKColors.Khaki,
    SKColors.LawnGreen,
    SKColors.LightCoral,
    SKColors.LightPink,
    SKColors.LightSkyBlue,
    SKColors.LimeGreen,
    SKColors.MediumAquamarine,
    SKColors.MediumPurple,
    SKColors.Orchid,
    SKColors.PaleVioletRed,
    SKColors.Peru,
    SKColors.Plum,
    SKColors.RosyBrown,
    SKColors.SandyBrown,
    SKColors.SeaGreen,
    SKColors.Sienna,
    SKColors.SpringGreen,
    SKColors.Turquoise,
    SKColors.Violet,
    SKColors.YellowGreen,
  };

  // minimal INotifyPropertyChanged
  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
