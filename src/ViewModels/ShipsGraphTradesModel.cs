using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using X4PlayerShipTradeAnalyzer.Models;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public class ShipsGraphTradesModel : ShipsGraphsBaseModel
{
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
      ReloadShipsAndRebuildActiveSeries();
    }
  }

  private void ApplyTradeFilter()
  {
    ShipList.Clear();
    GraphShipItem? ship = null;
    foreach (var ft in _allFullTrades)
    {
      if (!WithInternalTrades && FullTrade.IsInternalTrade(ft))
        continue;

      //   FullTrades.Add(ft);

      if (!ShipList.Any(s => s.ShipId == ft.ShipId))
      {
        ship = new GraphShipItem
        {
          ShipId = (int)ft.ShipId,
          ShipName = $"{ft.ShipName} ({ft.ShipCode})",
          EstimatedProfit = 0m,
        };
        ShipList.Add(ship);
      }
      if (ship != null)
      {
        ship.EstimatedProfit += ft.Profit;
      }
    }
    ResortShips();
    OnPropertyChanged(nameof(ShipList));
  }

  public ShipsGraphTradesModel()
    : base() { }

  protected override void LoadShips()
  {
    if (_allFullTrades.Count == 0)
      FullTrade.GetFullTrades(ref _allFullTrades);
    ApplyTradeFilter();
  }

  protected override List<LiveChartsCore.Defaults.ObservablePoint> LoadCumulativeProfitPoints(int shipId)
  {
    // Build cumulative profit using buy/sell operations by time for the ship
    var list = new List<LiveChartsCore.Defaults.ObservablePoint>();
    List<FullTrade> shipTrades = _allFullTrades.Where(ft => ft.ShipId == shipId).OrderBy(ft => ft.EndTime).ToList();
    foreach (var ft in shipTrades)
    {
      var t = ft.EndTime;
      var p = ft.Profit;
      if (list.Count == 0)
        list.Add(new LiveChartsCore.Defaults.ObservablePoint(t, (double)p));
      else
        list.Add(new LiveChartsCore.Defaults.ObservablePoint(t, list.Last().Y + (double)p));
    }
    return list;
  }
}
