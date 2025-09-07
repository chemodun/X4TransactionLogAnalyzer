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
  private List<FullTrade> _allFullTrades = new();
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
    if (_allFullTrades.Count == 0)
      FullTrade.GetFullTrades(ref _allFullTrades);

    List<FullTrade> filteredTrades = _allFullTrades.Where(ft => WithInternalTrades || !FullTrade.IsInternalTrade(ft)).ToList();
    var rows = new List<(string WareId, string WareName, double Profit)>();
    filteredTrades.ForEach(ft =>
    {
      var existing = rows.Find(r => r.WareId == ft.WareId);
      if (existing != default)
      {
        rows.Remove(existing);
        rows.Add((existing.WareId, existing.WareName, existing.Profit + decimal.ToDouble(ft.Profit)));
      }
      else
      {
        rows.Add((ft.WareId, ft.WareName, decimal.ToDouble(ft.Profit)));
      }
    });

    return rows.OrderByDescending(r => r.Profit).ToList();
  }
}
