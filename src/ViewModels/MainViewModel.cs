using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
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

  private WaresShipsStatsTransactionsModel? _transactionsWaresShipsStats;
  public WaresShipsStatsTransactionsModel? TransactionsWaresShipsStats
  {
    get => _transactionsWaresShipsStats;
    set
    {
      _transactionsWaresShipsStats = value;
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

  private WaresShipsStatsTradesModel? _tradesWaresShipsStats;
  public WaresShipsStatsTradesModel? TradesWaresShipsStats
  {
    get => _tradesWaresShipsStats;
    set
    {
      _tradesWaresShipsStats = value;
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
    TransactionsShipsWaresStats = new ShipsWaresStatsTransactionsModel();
    TransactionsWaresShipsStats = new WaresShipsStatsTransactionsModel();
    TradesData = new ShipsDataTradesModel();
    TradesGraphs = new ShipsGraphTradesModel();
    TradesShipsWaresStats = new ShipsWaresStatsTradesModel();
    TradesWaresShipsStats = new WaresShipsStatsTradesModel();
    Configuration = new ConfigurationViewModel();
  }

  public static void LoadData()
  {
    Transaction.GetAllTransactions(ref AllTransactions);
    FullTrade.GetFullTrades(ref AllTrades, AllTransactions);
  }

  public void RegisterCharts(Func<string, CartesianChart?> getChart)
  {
    var chart = getChart("TransactionsShipsByWaresChart");
    if (chart is null)
      return;

    chart.PointerPressed += OnChartPointerPressed;
    chart = getChart("TransactionsWaresByShipsChart");
    if (chart != null)
      chart.PointerPressed += OnChartPointerPressed;
    chart = getChart("TradesShipsByWaresChart");
    if (chart != null)
      chart.PointerPressed += OnChartPointerPressed;
    chart = getChart("TradesWaresByShipsChart");
    if (chart != null)
      chart.PointerPressed += OnChartPointerPressed;
  }

  private void OnChartPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
  {
    if (sender is not CartesianChart chart)
      return;

    // Get Avalonia point
    var avaloniaPoint = e.GetPosition(chart);

    // Convert to LvcPointD
    var lvcPoint = new LvcPointD(avaloniaPoint.X, avaloniaPoint.Y);

    // Pass to ScalePixelsToData
    var data = chart.ScalePixelsToData(lvcPoint);

    var itemIndex = (int)Math.Round(data.X);
    switch (chart.Name)
    {
      case "TransactionsShipsByWaresChart":
        if (TransactionsShipsWaresStats != null)
          TransactionsShipsWaresStats.OnChartPointPressed(itemIndex);
        break;
      case "TransactionsWaresByShipsChart":
        if (TransactionsWaresShipsStats != null)
          TransactionsWaresShipsStats.OnChartPointPressed(itemIndex);
        break;
      case "TradesShipsByWaresChart":
        if (TradesShipsWaresStats != null)
          TradesShipsWaresStats.OnChartPointPressed(itemIndex);
        break;
      case "TradesWaresByShipsChart":
        if (TradesWaresShipsStats != null)
          TradesWaresShipsStats.OnChartPointPressed(itemIndex);
        break;
    }
  }

  public void Refresh()
  {
    // Reload data for all models
    LoadData();
    TransactionsData?.Refresh();
    TransactionsGraphs?.Refresh();
    TransactionsShipsWaresStats?.Refresh();
    TransactionsWaresShipsStats?.Refresh();
    TradesGraphs?.Refresh();
    TradesData?.Refresh();
    TradesShipsWaresStats?.Refresh();
    TradesWaresShipsStats?.Refresh();
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
