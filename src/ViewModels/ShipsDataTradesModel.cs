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

  protected override void LoadData()
  {
    try
    {
      FullTrade.GetFullTrades(ref _allFullTrades);
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
      if (!WithInternalTrades && FullTrade.IsInternalTrade(ft))
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

  // INotifyPropertyChanged comes from BaseShipsModel
}
