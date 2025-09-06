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

  private ShipsTransactionsModel? _shipsTransactions;
  public ShipsTransactionsModel? ShipsTransactions
  {
    get => _shipsTransactions;
    set
    {
      _shipsTransactions = value;
      OnPropertyChanged();
    }
  }

  private ShipsGraphsModel? _shipsGraphs;
  public ShipsGraphsModel? ShipsGraphs
  {
    get => _shipsGraphs;
    set
    {
      _shipsGraphs = value;
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

  private FullTradesModel? _fullTrades;
  public FullTradesModel? FullTrades
  {
    get => _fullTrades;
    set
    {
      _fullTrades = value;
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
    ShipsTransactions = new ShipsTransactionsModel();
    ShipsGraphs = new ShipsGraphsModel();
    WaresStats = new WaresStatsModel();
    FullTrades = new FullTradesModel();
    Configuration = new ConfigurationViewModel();
  }

  public void Refresh()
  {
    // Reload data for all models
    ShipsTransactions?.Refresh();
    ShipsGraphs?.Refresh();
    WaresStats?.Refresh();
    FullTrades?.Refresh();
    Configuration?.RefreshStats();
  }

  // minimal INotifyPropertyChanged
  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
