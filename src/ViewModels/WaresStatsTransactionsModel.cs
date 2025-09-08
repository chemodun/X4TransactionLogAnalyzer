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
using X4PlayerShipTradeAnalyzer.Views;
using RectGeometry = LiveChartsCore.SkiaSharpView.Drawing.Geometries.RectangleGeometry;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public sealed class WaresStatsTransactionsModel : WaresStatsBaseModel
{
  private bool _isContainerChecked = true;
  public bool IsContainerChecked
  {
    get => _isContainerChecked;
    set
    {
      if (_isContainerChecked == value)
        return;
      if (!value && !_isSolidChecked)
      {
        IsSolidChecked = true; // enforce at least one
      }
      _isContainerChecked = value;
      OnPropertyChanged();
      Reload();
    }
  }

  private bool _isSolidChecked;
  public bool IsSolidChecked
  {
    get => _isSolidChecked;
    set
    {
      if (_isSolidChecked == value)
        return;
      if (!value && !_isContainerChecked)
      {
        IsContainerChecked = true; // enforce at least one
      }
      _isSolidChecked = value;
      OnPropertyChanged();
      Reload();
    }
  }

  public WaresStatsTransactionsModel()
    : base() { }

  protected override List<(string WareId, string WareName, double Profit)> LoadData()
  {
    return MainViewModel
      .AllTransactions.Where(t => (IsContainerChecked && t.Transport == "container") || (IsSolidChecked && t.Transport == "solid"))
      .GroupBy(t => (t.Ware, t.Product))
      .Select(g => (WareId: g.Key.Ware, WareName: g.Key.Product, Profit: Convert.ToDouble(g.Sum(t => t.EstimatedProfit))))
      .OrderByDescending(r => r.Profit)
      .ToList();
  }
}
