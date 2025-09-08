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
using RectGeometry = LiveChartsCore.SkiaSharpView.Drawing.Geometries.RectangleGeometry;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class WaresStatsTransactionsModel : WaresStatsBaseModel
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

  public WaresStatsTransactionsModel()
    : base() { }

  protected override List<(string WareId, string WareName, double Profit)> LoadData()
  {
    IEnumerable<Transaction> q = MainViewModel.AllTransactions;
    q = Transport switch
    {
      TransportFilter.Container => q.Where(t => t.Transport == "container"),
      TransportFilter.Solid => q.Where(t => t.Transport == "solid"),
      TransportFilter.Liquid => q.Where(t => t.Transport == "liquid"),
      _ => q,
    };
    return q.GroupBy(t => (t.Ware, t.Product))
      .Select(g => (WareId: g.Key.Ware, WareName: g.Key.Product, Profit: Convert.ToDouble(g.Sum(t => t.EstimatedProfit))))
      .OrderByDescending(r => r.Profit)
      .ToList();
  }
}
