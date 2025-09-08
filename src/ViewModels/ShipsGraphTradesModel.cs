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
    ShipList = new ObservableCollection<GraphShipItem>(
      MainViewModel
        .AllTrades.Where(ft => WithInternalTrades || !FullTrade.IsInternalTrade(ft))
        .GroupBy(t => (t.ShipId, t.ShipName, t.ShipCode))
        .Select(g => new GraphShipItem
        {
          ShipId = Convert.ToInt32(g.Key.ShipId),
          ShipName = $"{g.Key.ShipName} ({g.Key.ShipCode})",
          EstimatedProfit = g.Sum(t => t.Profit),
        })
    );
    ResortShips();
    OnPropertyChanged(nameof(ShipList));
  }

  public ShipsGraphTradesModel()
    : base() { }

  protected override void LoadShips()
  {
    ApplyTradeFilter();
  }

  protected override List<LiveChartsCore.Defaults.ObservablePoint> LoadCumulativeProfitPoints(int shipId)
  {
    decimal sum = 0m;
    return MainViewModel
      .AllTrades.Where(ft => (WithInternalTrades || !FullTrade.IsInternalTrade(ft)) && ft.ShipId == shipId)
      .OrderBy(t => t.EndTime)
      .Select(t =>
      {
        sum += t.Profit;
        return new LiveChartsCore.Defaults.ObservablePoint(t.EndTime, (double)sum);
      })
      .ToList();
  }
}
