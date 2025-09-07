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

  private ShipsDataTransactionsModel? _shipsDataTransactions;
  public ShipsDataTransactionsModel? ShipsDataTransactions
  {
    get => _shipsDataTransactions;
    set
    {
      _shipsDataTransactions = value;
      OnPropertyChanged();
    }
  }

  private ShipsGraphTransactionsModel? _shipsGraphs;
  public ShipsGraphTransactionsModel? ShipsGraphs
  {
    get => _shipsGraphs;
    set
    {
      _shipsGraphs = value;
      OnPropertyChanged();
    }
  }

  private ShipsGraphTradesModel? _shipsGraphsTrades;
  public ShipsGraphTradesModel? ShipsGraphsTrades
  {
    get => _shipsGraphsTrades;
    set
    {
      _shipsGraphsTrades = value;
      OnPropertyChanged();
    }
  }

  private WaresStatsTransactionsModel? _shipsWaresStats;
  public WaresStatsTransactionsModel? ShipsWaresStats
  {
    get => _shipsWaresStats;
    set
    {
      _shipsWaresStats = value;
      OnPropertyChanged();
    }
  }

  private ShipsDataTradesModel? _shipsDataTrades;
  public ShipsDataTradesModel? ShipsDataTrades
  {
    get => _shipsDataTrades;
    set
    {
      _shipsDataTrades = value;
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
    ShipsDataTransactions = new ShipsDataTransactionsModel();
    ShipsGraphs = new ShipsGraphTransactionsModel();
    ShipsGraphsTrades = new ShipsGraphTradesModel();
    ShipsWaresStats = new WaresStatsTransactionsModel();
    ShipsDataTrades = new ShipsDataTradesModel();
    Configuration = new ConfigurationViewModel();
  }

  public void Refresh()
  {
    // Reload data for all models
    ShipsDataTransactions?.Refresh();
    ShipsGraphs?.Refresh();
    ShipsGraphsTrades?.Refresh();
    ShipsWaresStats?.Refresh();
    ShipsDataTrades?.Refresh();
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
