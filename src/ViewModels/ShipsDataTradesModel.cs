using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Generators;
using X4PlayerShipTradeAnalyzer.Models;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

/// <summary>
/// Draft view model that materializes full trades from the database.
/// Loads on construction and can be refreshed after save-game imports.
/// </summary>
public sealed class ShipsDataTradesModel : ShipsDataBaseModel
{
  // All full trades loaded from analytics
  public ObservableCollection<FullTrade> FullTrades { get; } = new();

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

  public ShipsDataTradesModel()
  {
    LoadData();
  }

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

  private string _timeMin = "-";
  public string TimeMin
  {
    get => _timeMin;
    private set
    {
      if (_timeMin != value)
      {
        _timeMin = value;
        OnPropertyChanged();
      }
    }
  }

  private string _timeAvg = "-";
  public string TimeAvg
  {
    get => _timeAvg;
    private set
    {
      if (_timeAvg != value)
      {
        _timeAvg = value;
        OnPropertyChanged();
      }
    }
  }

  private string _timeMax = "-";
  public string TimeMax
  {
    get => _timeMax;
    private set
    {
      if (_timeMax != value)
      {
        _timeMax = value;
        OnPropertyChanged();
      }
    }
  }

  protected override void LoadData()
  {
    try
    {
      GetFullTrades();
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

    var ships = new Dictionary<long, ShipInfo>();
    foreach (var ft in _allFullTrades)
    {
      if (!WithInternalTrades && IsInternalTrade(ft))
        continue;

      FullTrades.Add(ft);

      if (!ships.TryGetValue(ft.ShipId, out var info))
      {
        info = new ShipInfo
        {
          ShipId = (int)ft.ShipId,
          ShipName = $"{ft.ShipName} ({ft.ShipCode})",
          EstimatedProfit = 0m,
        };
        ships.Add(ft.ShipId, info);
      }
      info.EstimatedProfit = (info.EstimatedProfit ?? 0m) + ft.Profit;
    }

    // Fill ShipList sorted according to current sort order
    ShipList.Clear();
    foreach (var ship in SortShips(ships.Values))
      ShipList.Add(ship);

    OnPropertyChanged(nameof(FullTrades));
    OnPropertyChanged(nameof(ShipList));
  }

  protected override void ApplyShipFilter()
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
      TimeMin = "-";
      TimeAvg = "-";
      TimeMax = "-";
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
      TimeMin = Services.TimeFormatter.FormatHms(spentMin);
      TimeAvg = Services.TimeFormatter.FormatHms(spentSum / count);
      TimeMax = Services.TimeFormatter.FormatHms(spentMax);
    }
    else
    {
      // reset summaries for empty selection
      TimeInService = "-";
      ItemsTraded = "0";
      TotalProfit = "0";
      TimeMin = "-";
      TimeAvg = "-";
      TimeMax = "-";
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

  private void GetFullTrades()
  { //MIC-510
    var conn = MainWindow.GameData.Connection;
    if (conn == null)
      return;
    // Use the pre-resolved player_ships_transactions_log view for ship trades; it already resolves station and sector.
    using var cmd = conn.CreateCommand();
    var baseSelect =
      @"
SELECT
  id,
  code,
  name,
  time,
  ware,
  ware_name,
  operation,
  price,
  volume,
  counterpart_faction,
  sector,
  station,
  counterpart_code
FROM player_ships_transactions_log";
    cmd.CommandText = AppendWhereOnFilters(baseSelect) + " ORDER BY id, time";
    using var rdr = cmd.ExecuteReader();
    _allFullTrades.Clear();
    long currentShip = -1;
    string currentWare = string.Empty;
    // State of an accumulating segment
    bool inSegment = false;
    bool sellingStarted = false;
    long cumVolume = 0; // positive while buying, decreases during selling
    FullTrade? trade = null;

    // Ordered legs for this segment
    var purchases = new List<TradeLeg>();
    var sales = new List<TradeLeg>();

    void Reset()
    {
      inSegment = false;
      sellingStarted = false;
      cumVolume = 0;
      purchases.Clear();
      sales.Clear();
      trade = null;
    }

    void SaveAndReset()
    {
      // Segment complete: emit trade
      if (trade != null && trade.BoughtVolume > 0 && trade.SoldVolume > 0)
      {
        int boughtIndex = purchases.Count - 1;
        if (trade.BoughtVolume > trade.SoldVolume)
        {
          var purchasesNew = new List<TradeLeg>();
          trade.BoughtVolume = 0;
          trade.TotalBuyCost = 0m;
          while (boughtIndex >= 0 && trade.BoughtVolume != trade.SoldVolume)
          {
            var leg = purchases[boughtIndex];
            purchasesNew.Insert(0, leg);
            trade.BoughtVolume += leg.Volume;
            trade.TotalBuyCost += leg.Volume * leg.Price;
            boughtIndex--;
          }
          if (trade.BoughtVolume != trade.SoldVolume)
          {
            // Should not happen, but just in case
            Reset();
            return;
          }
          purchases = purchasesNew;
          trade.StartTime = purchases.First().Time;
          trade.Time = Services.TimeFormatter.FormatHms(trade.StartTime);
        }
        if (trade.BoughtVolume == trade.SoldVolume)
        {
          trade.Purchases = purchases.ToArray();
          trade.Sales = sales.ToArray();
          _allFullTrades.Add(trade);
        }
      }
      Reset();
    }

    while (rdr.Read())
    {
      var shipId = rdr.GetInt64(0);
      var code = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
      var name = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
      var time = (int)rdr.GetInt64(3);
      var ware = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4);
      var wareName = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5);
      var operation = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6);
      var price = rdr.IsDBNull(7) ? 0m : Convert.ToDecimal(rdr.GetDouble(7));
      var volume = rdr.IsDBNull(8) ? 0L : rdr.GetInt64(8);
      // No counterpart_id in the view
      var stationOwner = rdr.IsDBNull(9) ? string.Empty : rdr.GetString(9);
      var stationSector = rdr.IsDBNull(10) ? string.Empty : rdr.GetString(10);
      var stationName = rdr.IsDBNull(11) ? string.Empty : rdr.GetString(11);
      var stationCode = rdr.IsDBNull(12) ? string.Empty : rdr.GetString(12);

      bool shipChanged = shipId != currentShip;
      bool wareChanged = !string.Equals(ware, currentWare, StringComparison.OrdinalIgnoreCase);

      if (
        shipChanged
        || wareChanged
        || (inSegment && string.Equals(operation, "buy", StringComparison.OrdinalIgnoreCase) && sellingStarted)
      )
      {
        // If we switch context and have a completed segment, drop incomplete ones; only emit on zero balance
        SaveAndReset();
        currentShip = shipId;
        currentWare = ware;
      }

      // We only consider sequences that start with buys
      if (!inSegment)
      {
        if (string.Equals(operation, "buy", StringComparison.OrdinalIgnoreCase))
        {
          trade = new FullTrade
          {
            ShipId = currentShip,
            ShipCode = code,
            ShipName = name,
            WareId = currentWare,
            WareName = wareName,
            StartTime = time,
            Time = Services.TimeFormatter.FormatHms(time),
            EndTime = time,
            BoughtVolume = volume,
            SoldVolume = 0,
            TotalBuyCost = price * volume,
            TotalRevenue = 0,
          };
          inSegment = true;
          cumVolume = volume;
          if (volume > 0)
          {
            purchases.Add(
              new TradeLeg
              {
                StationCode = stationCode,
                StationOwner = stationOwner,
                StationName = stationName,
                Volume = volume,
                Price = price,
                Time = time,
                Sector = stationSector,
              }
            );
          }
        }
        else
        {
          // Selling without inventory; ignore until a buy happens
        }
      }
      else if (trade != null)
      {
        // Inside a segment
        if (string.Equals(operation, "buy", StringComparison.OrdinalIgnoreCase))
        {
          // Another buy
          cumVolume += volume;
          trade.EndTime = time;
          trade.BoughtVolume += volume;
          trade.TotalBuyCost += price * volume;
          if (volume > 0)
          {
            purchases.Add(
              new TradeLeg
              {
                StationCode = stationCode,
                StationOwner = stationOwner,
                StationName = stationName,
                Volume = volume,
                Price = price,
                Time = time,
                Sector = stationSector,
              }
            );
          }
        }
        else
        {
          // A sell
          // If selling more than available, clamp to available to keep non-negative inventory
          if (!sellingStarted)
            sellingStarted = true;
          long soldNow = Math.Min(volume, Math.Max(0, cumVolume));
          if (soldNow > 0)
          {
            cumVolume -= soldNow;
            trade.EndTime = time;
            trade.SoldVolume += soldNow;
            trade.TotalRevenue += price * soldNow;
            sales.Add(
              new TradeLeg
              {
                StationCode = stationCode,
                StationOwner = stationOwner,
                StationName = stationName,
                Volume = soldNow,
                Price = price,
                Time = time,
                Sector = stationSector,
              }
            );
          }
        }
        if (cumVolume == 0 && trade != null && (trade.BoughtVolume > 0) && (trade.SoldVolume > 0))
        {
          SaveAndReset();
        }
      }
    }
  }

  // INotifyPropertyChanged comes from BaseShipsModel

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
