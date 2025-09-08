using System;
using System.Collections.Generic;
using System.Linq;
using X4PlayerShipTradeAnalyzer.Models;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class ShipsWaresStatsTransactionsModel : ShipsWaresStatsBaseModel
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

  public ShipsWaresStatsTransactionsModel()
    : base() { }

  protected override List<(int ShipId, string ShipName, string WareId, string WareName, double Profit)> LoadData()
  {
    IEnumerable<Transaction> q = MainViewModel.AllTransactions;

    q = Transport switch
    {
      TransportFilter.Container => q.Where(t => t.Transport == "container"),
      TransportFilter.Solid => q.Where(t => t.Transport == "solid"),
      TransportFilter.Liquid => q.Where(t => t.Transport == "liquid"),
      _ => q,
    };

    // Group by Ship + Ware
    return q.GroupBy(t => (t.ShipId, t.FullName, t.Ware, t.Product))
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
