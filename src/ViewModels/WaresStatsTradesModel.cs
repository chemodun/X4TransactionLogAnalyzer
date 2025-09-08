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

public sealed class WaresStatsTradesModel : WaresStatsBaseModel
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
      Reload();
    }
  }

  public WaresStatsTradesModel()
    : base() { }

  protected override List<(string WareId, string WareName, double Profit)> LoadData()
  {
    return MainViewModel
      .AllTrades.Where(ft => WithInternalTrades || !FullTrade.IsInternalTrade(ft))
      .GroupBy(t => (t.Ware, t.Product))
      .Select(g => (WareId: g.Key.Ware, WareName: g.Key.Product, Profit: Convert.ToDouble(g.Sum(t => t.Profit))))
      .OrderByDescending(r => r.Profit)
      .ToList();
  }
}
