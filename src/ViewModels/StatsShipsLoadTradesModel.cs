using System;
using System.Collections.Generic;
using System.Linq;
using X4PlayerShipTradeAnalyzer.Models;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class StatsShipsLoadTradesModel : StatsShipsLoadBaseModel
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

  protected override IEnumerable<(int ShipId, string ShipName, int BucketIndex)> LoadEntries()
  {
    IEnumerable<FullTrade> q = MainViewModel.AllTrades;
    if (SelectedShipClass != "All")
      q = q.Where(ft => string.Equals(ft.ShipClass, SelectedShipClass, StringComparison.OrdinalIgnoreCase));
    if (!WithInternalTrades)
      q = q.Where(ft => !FullTrade.IsInternalTrade(ft));
    foreach (var t in q)
    {
      int bucket = (int)Math.Min(9, Math.Floor((double)t.LoadPercent / 10.0));
      yield return ((int)t.ShipId, t.ShipFullName, bucket);
    }
  }
}
