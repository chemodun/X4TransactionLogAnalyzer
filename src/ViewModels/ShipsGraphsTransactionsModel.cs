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
  public TransportFilter Transport
  {
    get => _transport;
    set
    {
      if (_transport == value)
        return;
      _transport = value;
      OnPropertyChanged();
      ReloadShipsAndRebuildActiveSeries();
    }
  }

  public ShipsGraphTransactionsModel()
    : base() { }

  protected override void LoadShips()
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

  protected override List<LiveChartsCore.Defaults.ObservablePoint> LoadCumulativeProfitPoints(int shipId)
  {
    decimal sum = 0m;
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
