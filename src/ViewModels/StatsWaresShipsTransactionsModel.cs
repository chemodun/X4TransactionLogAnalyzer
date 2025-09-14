using System;
using System.Collections.Generic;
using System.Linq;
using X4PlayerShipTradeAnalyzer.Models;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class StatsWaresShipsTransactionsModel : StatsWaresShipsBaseModel
{
  private TransportFilter _transport = TransportFilter.Container;
  public TransportFilter Transport
  {
    get => _transport;
    set
    {
      if (_transport == value)
        return;
      _transport = value;
      OnPropertyChanged();
      Reload();
    }
  }

  public StatsWaresShipsTransactionsModel(SkiaSharp.SKColor? foreground = null, SkiaSharp.SKColor? background = null)
    : base(foreground, background) { }

  protected override List<(long ShipId, string ShipName, string WareId, string WareName, double Profit)> LoadData()
  {
    IEnumerable<Transaction> q = MainViewModel.AllTransactions;
    if (SelectedShipClass != "All")
      q = q.Where(t => string.Equals(t.ShipClass, SelectedShipClass, StringComparison.OrdinalIgnoreCase));
    if (SelectedStation != null && SelectedStation.Id != 0)
    {
      HashSet<long> subordinateIds = Subordinate.GetSubordinateIdsForCommander(SelectedStation.Id);
      q = q.Where(t => subordinateIds.Contains(t.ShipId));
    }
    q = Transport switch
    {
      TransportFilter.Container => q.Where(t => t.Transport == "container"),
      TransportFilter.Solid => q.Where(t => t.Transport == "solid"),
      TransportFilter.Liquid => q.Where(t => t.Transport == "liquid"),
      _ => q,
    };

    // Group by Ship + Ware
    return q.GroupBy(t => (t.Ware, t.Product, t.ShipId, t.FullName))
      .Select(g =>
        (
          ShipId: g.Key.ShipId,
          ShipName: g.Key.FullName,
          WareId: g.Key.Ware,
          WareName: g.Key.Product,
          Profit: Convert.ToDouble(g.Sum(t => t.EstimatedProfit))
        )
      )
      .OrderByDescending(r => r.Profit)
      .ToList();
  }
}
