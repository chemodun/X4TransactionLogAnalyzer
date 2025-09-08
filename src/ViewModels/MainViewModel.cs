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

  private ShipsDataTransactionsModel? _transactionsData;
  public ShipsDataTransactionsModel? TransactionsData
  {
    get => _transactionsData;
    set
    {
      _transactionsData = value;
      OnPropertyChanged();
    }
  }

  private ShipsGraphTransactionsModel? _transactionsGraphs;
  public ShipsGraphTransactionsModel? TransactionsGraphs
  {
    get => _transactionsGraphs;
    set
    {
      _transactionsGraphs = value;
      OnPropertyChanged();
    }
  }

  private WaresStatsTransactionsModel? _transactionsWaresStats;
  public WaresStatsTransactionsModel? TransactionsWaresStats
  {
    get => _transactionsWaresStats;
    set
    {
      _transactionsWaresStats = value;
      OnPropertyChanged();
    }
  }

  private ShipsWaresStatsTransactionsModel? _transactionsShipsWaresStats;
  public ShipsWaresStatsTransactionsModel? TransactionsShipsWaresStats
  {
    get => _transactionsShipsWaresStats;
    set
    {
      _transactionsShipsWaresStats = value;
      OnPropertyChanged();
    }
  }

  private ShipsDataTradesModel? _tradesData;
  public ShipsDataTradesModel? TradesData
  {
    get => _tradesData;
    set
    {
      _tradesData = value;
      OnPropertyChanged();
    }
  }

  private ShipsGraphTradesModel? _tradesGraphs;
  public ShipsGraphTradesModel? TradesGraphs
  {
    get => _tradesGraphs;
    set
    {
      _tradesGraphs = value;
      OnPropertyChanged();
    }
  }

  private WaresStatsTradesModel? _tradesWaresStats;
  public WaresStatsTradesModel? TradesWaresStats
  {
    get => _tradesWaresStats;
    set
    {
      _tradesWaresStats = value;
      OnPropertyChanged();
    }
  }

  private ShipsWaresStatsTradesModel? _tradesShipsWaresStats;
  public ShipsWaresStatsTradesModel? TradesShipsWaresStats
  {
    get => _tradesShipsWaresStats;
    set
    {
      _tradesShipsWaresStats = value;
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
    TransactionsData = new ShipsDataTransactionsModel();
    TransactionsGraphs = new ShipsGraphTransactionsModel();
    TransactionsWaresStats = new WaresStatsTransactionsModel();
    TransactionsShipsWaresStats = new ShipsWaresStatsTransactionsModel();
    TradesData = new ShipsDataTradesModel();
    TradesGraphs = new ShipsGraphTradesModel();
    TradesWaresStats = new WaresStatsTradesModel();
    TradesShipsWaresStats = new ShipsWaresStatsTradesModel();
    Configuration = new ConfigurationViewModel();
  }

  public static void LoadData()
  {
    Transaction.GetAllTransactions(ref AllTransactions);
    FullTrade.GetFullTrades(ref AllTrades, AllTransactions);
  }

  public void Refresh()
  {
    // Reload data for all models
    LoadData();
    TransactionsData?.Refresh();
    TransactionsGraphs?.Refresh();
    TransactionsWaresStats?.Refresh();
    TradesGraphs?.Refresh();
    TradesData?.Refresh();
    TradesWaresStats?.Refresh();
    Configuration?.RefreshStats();
  }

  // minimal INotifyPropertyChanged
  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum TopNFilter
{
  Top10 = 10,
  Top25 = 25,
  Top50 = 50,
  Top100 = 100,
}
