using System;
using System.Collections.Generic;
using System.Linq;
using X4PlayerShipTradeAnalyzer.Models;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class StatsShipsLoadTransactionsModel : StatsShipsLoadBaseModel
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

  protected override IEnumerable<(long ShipId, string ShipName, int BucketIndex)> LoadEntries()
  {
    IEnumerable<Transaction> q = MainViewModel.AllTransactions;
    if (SelectedShipClass != "All")
      q = q.Where(t => string.Equals(t.ShipClass, SelectedShipClass, StringComparison.OrdinalIgnoreCase));
    q = Transport switch
    {
      TransportFilter.Container => q.Where(t => t.Transport == "container"),
      TransportFilter.Solid => q.Where(t => t.Transport == "solid"),
      TransportFilter.Liquid => q.Where(t => t.Transport == "liquid"),
      _ => q,
    };

    foreach (var t in q)
    {
      decimal load = t.MaxQuantity > 0 ? (decimal)t.Quantity * 100m / t.MaxQuantity : 100m;
      int bucket = (int)Math.Min(9, Math.Floor((double)load / 10.0));
      yield return (t.ShipId, t.FullName, bucket);
    }
  }
}
