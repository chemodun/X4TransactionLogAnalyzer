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

  private ShipSortOrder _shipsSortOrder = ShipSortOrder.Name;
  public ShipSortOrder ShipsSortOrder
  {
    get => _shipsSortOrder;
    set
    {
      if (_shipsSortOrder == value)
        return;
      _shipsSortOrder = value;
      OnPropertyChanged();
      ResortShips();
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

  private void LoadData()
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

  private IEnumerable<ShipInfo> SortShips(IEnumerable<ShipInfo> ships)
  {
    return ShipsSortOrder switch
    {
      ShipSortOrder.Profit => ships.OrderByDescending(s => s.EstimatedProfit ?? 0m).ThenBy(s => s.ShipName, StringComparer.Ordinal),
      _ => ships.OrderBy(s => s.ShipName, StringComparer.Ordinal),
    };
  }

  private void ResortShips()
  {
    if (ShipList.Count == 0)
      return;
    var snapshot = ShipList.ToList();
    ShipList.Clear();
    foreach (var ship in SortShips(snapshot))
      ShipList.Add(ship);
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
    cmd.CommandText =
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
FROM player_ships_transactions_log
ORDER BY id, time
";
    using var rdr = cmd.ExecuteReader();
    _allFullTrades.Clear();
    long currentShip = -1;
    string currentWare = string.Empty;
    string currentWareName = string.Empty;
    string shipCode = string.Empty;
    string shipName = string.Empty;
    // State of an accumulating segment
    bool inSegment = false;
    int segmentStartTime = 0;
    long cumVolume = 0; // positive while buying, decreases during selling
    long boughtVolume = 0;
    long soldVolume = 0;
    decimal totalBuyCost = 0; // sum(price*volume) for buys
    decimal totalRevenue = 0; // sum(price*volume) for sells

    // Ordered legs for this segment
    var purchases = new List<TradeLeg>();
    var sales = new List<TradeLeg>();

    void Reset()
    {
      inSegment = false;
      segmentStartTime = 0;
      cumVolume = 0;
      boughtVolume = 0;
      soldVolume = 0;
      totalBuyCost = 0;
      totalRevenue = 0;
      purchases.Clear();
      sales.Clear();
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

      if (shipChanged || wareChanged)
      {
        // If we switch context and have a completed segment, drop incomplete ones; only emit on zero balance
        Reset();
        currentShip = shipId;
        currentWare = ware;
        currentWareName = wareName;
        shipCode = code;
        shipName = name;
      }

      // We only consider sequences that start with buys
      if (!inSegment)
      {
        if (string.Equals(operation, "buy", StringComparison.OrdinalIgnoreCase))
        {
          inSegment = true;
          segmentStartTime = time;
          cumVolume = volume;
          boughtVolume += volume;
          totalBuyCost += price * volume;
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
      else
      {
        // Inside a segment
        if (string.Equals(operation, "buy", StringComparison.OrdinalIgnoreCase))
        {
          // Another buy
          cumVolume += volume;
          boughtVolume += volume;
          totalBuyCost += price * volume;
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
          long soldNow = Math.Min(volume, Math.Max(0, cumVolume));
          if (soldNow > 0)
          {
            cumVolume -= soldNow;
            soldVolume += soldNow;
            totalRevenue += price * soldNow;
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

        if (cumVolume == 0 && (boughtVolume > 0) && (soldVolume > 0))
        {
          // Segment complete: emit trade
          _allFullTrades.Add(
            new FullTrade
            {
              ShipId = currentShip,
              ShipCode = shipCode,
              ShipName = shipName,
              WareId = currentWare,
              WareName = currentWareName,
              StartTime = segmentStartTime,
              Time = Services.TimeFormatter.FormatHms(segmentStartTime),
              EndTime = time,
              BoughtVolume = boughtVolume,
              SoldVolume = soldVolume,
              TotalBuyCost = totalBuyCost,
              TotalRevenue = totalRevenue,
              Purchases = purchases.ToArray(),
              Sales = sales.ToArray(),
            }
          );
          // Reset to look for next cycle for this ware
          Reset();
        }
      }
    }
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
