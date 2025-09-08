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
  public static List<Transaction> AllTransactions = new();
  public static List<FullTrade> AllTrades = new();
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
    LoadData();
    ShipsTransactionsData = new ShipsDataTransactionsModel();
    ShipsTransactionsGraphs = new ShipsGraphTransactionsModel();
    ShipsTransactionsWaresStats = new WaresStatsTransactionsModel();
    ShipsTradesData = new ShipsDataTradesModel();
    ShipsTradesGraphs = new ShipsGraphTradesModel();
    ShipsTradesWaresStats = new WaresStatsTradesModel();
    Configuration = new ConfigurationViewModel();
  }

  public static void LoadData()
  {
    Transaction.GetAllTransactions(ref AllTransactions);
    FullTrade.GetFullTrades(ref AllTrades);
  }

  public void Refresh()
  {
    // Reload data for all models
    LoadData();
    ShipsTransactionsData?.Refresh();
    ShipsTransactionsGraphs?.Refresh();
    ShipsTransactionsWaresStats?.Refresh();
    ShipsTradesGraphs?.Refresh();
    ShipsTradesData?.Refresh();
    ShipsTradesWaresStats?.Refresh();
    Configuration?.RefreshStats();
  }

  // minimal INotifyPropertyChanged
  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
