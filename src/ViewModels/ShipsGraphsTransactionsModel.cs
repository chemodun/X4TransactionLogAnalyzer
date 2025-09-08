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

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public class ShipsGraphTransactionsModel : ShipsGraphsBaseModel
{
  // Transport filters
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
      ReloadShipsAndRebuildActiveSeries();
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
      ReloadShipsAndRebuildActiveSeries();
    }
  }

  public ShipsGraphTransactionsModel()
    : base() { }

  protected override void LoadShips()
  {
    ShipList = new ObservableCollection<GraphShipItem>(
      MainViewModel
        .AllTransactions.Where(t => (IsContainerChecked && t.Transport == "container") || (IsSolidChecked && t.Transport == "solid"))
        .GroupBy(t => (t.ShipId, t.FullName))
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
    return MainViewModel
      .AllTransactions.Where(t =>
        ((IsContainerChecked && t.Transport == "container") || (IsSolidChecked && t.Transport == "solid")) && t.ShipId == shipId
      )
      .OrderBy(t => t.RawTime)
      .Select(t =>
      {
        sum += t.EstimatedProfit;
        return new LiveChartsCore.Defaults.ObservablePoint(t.RawTime, (double)sum);
      })
      .ToList();
  }
}
