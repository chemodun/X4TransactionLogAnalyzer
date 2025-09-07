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

  private WaresStatsModel? _waresStats;
  public WaresStatsModel? WaresStats
  {
    get => _waresStats;
    set
    {
      _waresStats = value;
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
    WaresStats = new WaresStatsModel();
    ShipsDataTrades = new ShipsDataTradesModel();
    Configuration = new ConfigurationViewModel();
  }

  public void Refresh()
  {
    // Reload data for all models
    ShipsDataTransactions?.Refresh();
    ShipsGraphs?.Refresh();
    ShipsGraphsTrades?.Refresh();
    WaresStats?.Refresh();
    ShipsDataTrades?.Refresh();
    Configuration?.RefreshStats();
  }

  // minimal INotifyPropertyChanged
  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
