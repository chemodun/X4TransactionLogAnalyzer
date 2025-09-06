using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using X4PlayerShipTradeAnalyzer.Models;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

/// <summary>
/// Draft view model that materializes full trades from the database.
/// Loads on construction and can be refreshed after save-game imports.
/// </summary>
public sealed class FullTradesModel : INotifyPropertyChanged
{
  // All full trades loaded from analytics
  public ObservableCollection<FullTrade> FullTrades { get; } = new();

  // Ships list populated from FullTrades (distinct ship id/name)
  public ObservableCollection<ShipInfo> ShipList { get; } = new();

  // Filtered by SelectedShip
  public ObservableCollection<FullTrade> FilteredFullTrades { get; } = new();

  // Combined purchase/sale steps of SelectedFullTrade
  public ObservableCollection<TradeStep> TradeSteps { get; } = new();

  private List<FullTrade> _allFullTrades = new();

  private bool _withInternalTrades = true;
  public bool WithInternalTrades
  {
    get => _withInternalTrades;
    set
    {
      if (_withInternalTrades == value)
        return;
      _withInternalTrades = value;
      OnPropertyChanged();
      // Re-apply filters and steps based on new setting
      ApplyTradeFilter();
    }
  }

  private ShipInfo? _selectedShip;
  public ShipInfo? SelectedShip
  {
    get => _selectedShip;
    set
    {
      if (_selectedShip == value)
        return;
      _selectedShip = value;
      OnPropertyChanged();
      ApplyShipFilter();
    }
  }

  private FullTrade? _selectedFullTrade;
  public FullTrade? SelectedFullTrade
  {
    get => _selectedFullTrade;
    set
    {
      if (_selectedFullTrade == value)
        return;
      _selectedFullTrade = value;
      OnPropertyChanged();
      RebuildTradeSteps();
    }
  }

  public FullTradesModel()
  {
    LoadData();
  }

  public void Refresh() => LoadData();

  // Summary fields (similar to ShipsTransactionsModel)
  private string _timeInService = "-";
  public string TimeInService
  {
    get => _timeInService;
    private set
    {
      if (_timeInService != value)
      {
        _timeInService = value;
        OnPropertyChanged();
      }
    }
  }

  private string _itemsTraded = "0";
  public string ItemsTraded
  {
    get => _itemsTraded;
    private set
    {
      if (_itemsTraded != value)
      {
        _itemsTraded = value;
        OnPropertyChanged();
      }
    }
  }

  private string _totalProfit = "0";
  public string TotalProfit
  {
    get => _totalProfit;
    private set
    {
      if (_totalProfit != value)
      {
        _totalProfit = value;
        OnPropertyChanged();
      }
    }
  }

  private string _tradeTimeMin = "-";
  public string TradeTimeMin
  {
    get => _tradeTimeMin;
    private set
    {
      if (_tradeTimeMin != value)
      {
        _tradeTimeMin = value;
        OnPropertyChanged();
      }
    }
  }

  private string _tradeTimeAvg = "-";
  public string TradeTimeAvg
  {
    get => _tradeTimeAvg;
    private set
    {
      if (_tradeTimeAvg != value)
      {
        _tradeTimeAvg = value;
        OnPropertyChanged();
      }
    }
  }

  private string _tradeTimeMax = "-";
  public string TradeTimeMax
  {
    get => _tradeTimeMax;
    private set
    {
      if (_tradeTimeMax != value)
      {
        _tradeTimeMax = value;
        OnPropertyChanged();
      }
    }
  }

  private void LoadData()
  {
    try
    {
      _allFullTrades = MainWindow.GameData.GetFullTrades().ToList();
      ApplyTradeFilter();
    }
    catch
    {
      // swallow for draft; UI can remain empty on errors
    }
  }

  private void ApplyTradeFilter()
  {
    FullTrades.Clear();
    ShipList.Clear();
    FilteredFullTrades.Clear();
    TradeSteps.Clear();
    SelectedShip = null;
    SelectedFullTrade = null;

    var seenShips = new HashSet<long>();
    foreach (var ft in _allFullTrades)
    {
      if (!WithInternalTrades && IsInternalTrade(ft))
        continue;

      FullTrades.Add(ft);

      if (seenShips.Add(ft.ShipId))
      {
        var displayName = $"{ft.ShipName} ({ft.ShipCode})";
        // Insert into ShipList keeping it sorted by display name
        int insertAt = 0;
        while (insertAt < ShipList.Count && string.CompareOrdinal(ShipList[insertAt].ShipName, displayName) < 0)
          insertAt++;
        ShipList.Insert(insertAt, new ShipInfo { ShipId = (int)ft.ShipId, ShipName = displayName });
      }
    }

    OnPropertyChanged(nameof(FullTrades));
    OnPropertyChanged(nameof(ShipList));
  }

  private void ApplyShipFilter()
  {
    FilteredFullTrades.Clear();
    TradeSteps.Clear();
    SelectedFullTrade = null;
    if (SelectedShip == null)
    {
      // reset summaries
      TimeInService = "-";
      ItemsTraded = "0";
      TotalProfit = "0";
      TradeTimeMin = "-";
      TradeTimeAvg = "-";
      TradeTimeMax = "-";
      return;
    }

    long itemsTotal = 0;
    decimal profitTotal = 0m;
    long spentSum = 0;
    long spentMin = long.MaxValue;
    long spentMax = 0;
    int count = 0;

    foreach (var ft in FullTrades.Where(f => f.ShipId == SelectedShip.ShipId))
    {
      FilteredFullTrades.Add(ft);

      // accumulate summaries inline
      itemsTotal += ft.SoldVolume;
      profitTotal += ft.Profit;
      long spent = (long)ft.SpentTimeRaw;
      spentSum += spent;
      if (spent < spentMin)
        spentMin = spent;
      if (spent > spentMax)
        spentMax = spent;
      count++;
    }

    // Update summaries based on filtered trades
    if (count > 0)
    {
      TimeInService = Services.TimeFormatter.FormatHms(spentSum, groupHours: true);
      ItemsTraded = itemsTotal.ToString("N0");
      TotalProfit = profitTotal.ToString("N2");
      TradeTimeMin = Services.TimeFormatter.FormatHms(spentMin);
      TradeTimeAvg = Services.TimeFormatter.FormatHms(spentSum / count);
      TradeTimeMax = Services.TimeFormatter.FormatHms(spentMax);
    }
    else
    {
      // reset summaries for empty selection
      TimeInService = "-";
      ItemsTraded = "0";
      TotalProfit = "0";
      TradeTimeMin = "-";
      TradeTimeAvg = "-";
      TradeTimeMax = "-";
    }
  }

  private void RebuildTradeSteps()
  {
    TradeSteps.Clear();
    if (SelectedFullTrade == null)
      return;

    // Merge purchases and sales into a single ordered list by time
    var steps = new List<TradeStep>();
    foreach (var p in SelectedFullTrade.Purchases ?? Array.Empty<TradeLeg>())
    {
      steps.Add(
        new TradeStep
        {
          TimeRaw = p.Time,
          Time = Services.TimeFormatter.FormatHms(p.Time, groupHours: true),
          Operation = "buy",
          Station = string.IsNullOrWhiteSpace(p.StationName) ? p.StationCode : p.StationName,
          Sector = p.Sector,
          Price = p.Price,
          Volume = p.Volume,
        }
      );
    }
    foreach (var s in SelectedFullTrade.Sales ?? Array.Empty<TradeLeg>())
    {
      steps.Add(
        new TradeStep
        {
          TimeRaw = s.Time,
          Time = Services.TimeFormatter.FormatHms(s.Time, groupHours: true),
          Operation = "sell",
          Station = string.IsNullOrWhiteSpace(s.StationName) ? s.StationCode : s.StationName,
          Sector = s.Sector,
          Price = s.Price,
          Volume = s.Volume,
        }
      );
    }
    foreach (var step in steps.OrderBy(t => t.TimeRaw))
      TradeSteps.Add(step);
  }

  private static bool IsInternalTrade(FullTrade ft)
  {
    bool onlyInternalBuy = ft.Purchases?.All(l => string.Equals(l.StationOwner, "player", StringComparison.OrdinalIgnoreCase)) ?? false;
    bool onlyInternalSell = ft.Sales?.All(l => string.Equals(l.StationOwner, "player", StringComparison.OrdinalIgnoreCase)) ?? false;
    return onlyInternalBuy && onlyInternalSell;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  public sealed class TradeStep
  {
    public long TimeRaw { get; init; }
    public string Time { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty; // buy/sell
    public string Station { get; init; } = string.Empty;
    public string Sector { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public long Volume { get; init; }
  }
}
