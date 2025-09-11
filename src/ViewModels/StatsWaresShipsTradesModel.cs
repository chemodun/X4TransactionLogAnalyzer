using System;
using System.Collections.Generic;
using System.Linq;
using X4PlayerShipTradeAnalyzer.Models;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class StatsWaresShipsTradesModel : StatsWaresShipsBaseModel
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
      Reload();
    }
  }

  public StatsWaresShipsTradesModel()
    : base() { }

  protected override List<(int ShipId, string ShipName, string WareId, string WareName, double Profit)> LoadData()
  {
    return MainViewModel
      .AllTrades.Where(ft => WithInternalTrades || !FullTrade.IsInternalTrade(ft))
      .GroupBy(t => (t.Ware, t.Product, t.ShipId, t.ShipFullName))
      .Select(g =>
        (
          ShipId: Convert.ToInt32(g.Key.ShipId),
          ShipName: g.Key.ShipFullName,
          WareId: g.Key.Ware,
          WareName: g.Key.Product,
          Profit: Convert.ToDouble(g.Sum(t => t.Profit))
        )
      )
      .OrderByDescending(r => r.Profit)
      .ToList();
  }
}
