using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using X4PlayerShipTradeAnalyzer.Models;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public class ShipsGraphTransactionsModel : ShipsGraphsBaseModel
{
  // Transport filters
  private TransportFilter _transport = TransportFilter.Container;
  public string Transport
  {
    get => _transport.ToString();
    set
    {
      if (_transport.ToString() == value)
        return;
      _transport = (TransportFilter)Enum.Parse(typeof(TransportFilter), value);
      OnPropertyChanged();
      ReloadShipsAndRebuildActiveSeries();
    }
  }

#pragma warning disable CA1822
  public List<string> TransportOptions => System.Enum.GetNames(typeof(TransportFilter)).ToList();
#pragma warning restore CA1822


  public ShipsGraphTransactionsModel()
    : base() { }

  protected override void LoadShips()
  {
    IEnumerable<Transaction> q = MainViewModel.AllTransactions;
    if (SelectedShipClass != "All")
      q = q.Where(t => string.Equals(t.ShipClass, SelectedShipClass, StringComparison.OrdinalIgnoreCase));

    if (SelectedStation != null && SelectedStation.Id != 0)
    {
      HashSet<long> subordinateIds = Subordinate.GetSubordinateIdsForCommander(SelectedStation.Id);
      q = q.Where(t => subordinateIds.Contains(t.ShipId));
    }

    if (Transport != "All")
      q = q.Where(t => t.Transport.Equals(Transport, StringComparison.InvariantCultureIgnoreCase));

    ShipList = new ObservableCollection<GraphShipItem>(
      q.GroupBy(t => (t.ShipId, t.FullName))
        .Select(g => new GraphShipItem
        {
          ShipId = g.Key.ShipId,
          ShipName = g.Key.FullName,
          EstimatedProfit = g.Sum(t => t.EstimatedProfit),
        })
    );

    // Apply current sort
    ResortShips();
    OnPropertyChanged(nameof(ShipList));
  }

  protected override List<LiveChartsCore.Defaults.ObservablePoint> LoadCumulativeProfitPoints(long shipId)
  {
    decimal sum = 0m;
    IEnumerable<Transaction> q = MainViewModel.AllTransactions;
    if (SelectedShipClass != "All")
      q = q.Where(t => string.Equals(t.ShipClass, SelectedShipClass, StringComparison.OrdinalIgnoreCase));
    if (SelectedStation != null && SelectedStation.Id != 0)
    {
      HashSet<long> subordinateIds = Subordinate.GetSubordinateIdsForCommander(SelectedStation.Id);
      q = q.Where(t => subordinateIds.Contains(t.ShipId));
    }

    if (Transport != "All")
      q = q.Where(t => t.Transport.Equals(Transport, StringComparison.InvariantCultureIgnoreCase));

    return q.Where(t => t.ShipId == shipId)
      .OrderBy(t => t.RawTime)
      .Select(t =>
      {
        sum += t.EstimatedProfit;
        return new LiveChartsCore.Defaults.ObservablePoint(t.RawTime, (double)sum);
      })
      .ToList();
  }
}
