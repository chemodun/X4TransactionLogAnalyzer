using System;
using System.Collections.Generic;
using System.Linq;
using X4PlayerShipTradeAnalyzer.Models;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class StatsShipsWaresTradesModel : StatsShipsWaresBaseModel
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

  public StatsShipsWaresTradesModel()
    : base() { }

  protected override List<(long ShipId, string ShipName, string WareId, string WareName, double Profit)> LoadData()
  {
    var q = MainViewModel.AllTrades.Where(ft => WithInternalTrades || !FullTrade.IsInternalTrade(ft));
    if (SelectedShipClass != "All")
      q = q.Where(ft => string.Equals(ft.ShipClass, SelectedShipClass, StringComparison.OrdinalIgnoreCase));
    if (SelectedStation != null && SelectedStation.Id != 0)
    {
      HashSet<long> subordinateIds = Subordinate.GetSubordinateIdsForCommander(SelectedStation.Id);
      q = q.Where(t => subordinateIds.Contains(t.ShipId));
    }
    return q.GroupBy(t => (t.ShipId, t.ShipFullName, t.Ware, t.Product))
      .Select(g =>
        (
          ShipId: g.Key.ShipId,
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
