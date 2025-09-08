using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using X4PlayerShipTradeAnalyzer.Utils;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public abstract class WaresShipsStatsBaseModel : INotifyPropertyChanged
{
  public ObservableCollection<ISeries> Series { get; } = new();
  public ObservableCollection<LegendItem> Legend { get; } = new();

  public Axis[] XAxes { get; }
  public Axis[] YAxes { get; }

  protected List<string> _labels = new();
  public IReadOnlyList<string> Labels => _labels;

#pragma warning disable CA1822
  public IEnumerable<TopNFilter> TopNOptions => Enum.GetValues(typeof(TopNFilter)).Cast<TopNFilter>();
#pragma warning restore CA1822

  private TopNFilter _topN = TopNFilter.Top25;
  public TopNFilter TopN
  {
    get => _topN;
    set
    {
      if (_topN == value)
        return;
      _topN = value;
      OnPropertyChanged();
      Reload();
    }
  }

  public double ChartMinWidth => Labels.Count * 56 + 600;
  public double ChartMinHeight => Legend.Count * 32;

  protected WaresShipsStatsBaseModel()
  {
    XAxes = new[]
    {
      new Axis
      {
        Name = "Wares",
        LabelsPaint = new SolidColorPaint(SKColors.Black),
        SeparatorsPaint = new SolidColorPaint(new SKColor(220, 220, 220)) { StrokeThickness = 1 },
        LabelsRotation = 90,
      },
    };

    YAxes = new[]
    {
      new Axis
      {
        Name = "Total Profit",
        Labeler = v => v.ToString("N2"),
        SeparatorsPaint = new SolidColorPaint(new SKColor(220, 220, 220)) { StrokeThickness = 1 },
      },
    };

    Reload();
  }

  public void Refresh() => Reload();

  protected abstract List<(int ShipId, string ShipName, string WareId, string WareName, double Profit)> LoadData();

  protected void Reload()
  {
    var rows = LoadData();

    // Group by ware and sort by total profit
    var wareOrder = rows.GroupBy(r => (r.WareId, r.WareName))
      .Select(g => new
      {
        g.Key.WareId,
        g.Key.WareName,
        TotalProfit = g.Sum(x => x.Profit),
      })
      .OrderByDescending(x => x.TotalProfit)
      .Take((int)TopN)
      .ToList();

    var topWares = wareOrder.Select(x => x.WareName).ToList();

    // Filter rows to only those wares
    rows = rows.Where(r => topWares.Contains(r.WareName))
      .OrderBy(r => topWares.IndexOf(r.WareName))
      .ThenBy(r => r.ShipName, StringComparer.OrdinalIgnoreCase)
      .ToList();

    var wares = rows.Select(r => r.WareName).Distinct().ToList();
    var ships = rows.Select(r => (r.ShipId, r.ShipName)).Distinct().ToList();

    _labels = wares;

    Series.Clear();
    Legend.Clear();

    foreach (var (shipId, shipName) in ships)
    {
      var values = new List<double?>(wares.Count);

      foreach (var ware in wares)
      {
        var profit = rows.Where(r => r.ShipName == shipName && r.WareName == ware).Sum(r => r.Profit);

        values.Add(profit == 0 ? null : profit);
      }

      // Skip ship if it contributes nothing
      if (values.All(v => v is null))
        continue;

      var sk = GetColorForShip(shipId);

      var series = new StackedColumnSeries<double?>
      {
        Name = shipName,
        Values = values,
        Stroke = null,
        Fill = new SolidColorPaint(sk),
        MaxBarWidth = 48,

        XToolTipLabelFormatter = cp =>
        {
          var wareIdx = (int)Math.Round(cp.Coordinate.SecondaryValue);
          return (wareIdx >= 0 && wareIdx < wares.Count) ? wares[wareIdx] : string.Empty;
        },
        YToolTipLabelFormatter = cp => cp.Coordinate.PrimaryValue.ToString("N2"),
      };

      Series.Add(series);
      Legend.Add(new LegendItem(shipName, new SolidColorBrush(Color.FromArgb(sk.Alpha, sk.Red, sk.Green, sk.Blue))));
    }

    XAxes[0].Labels = wares;

    OnPropertyChanged(nameof(ChartMinWidth));
    OnPropertyChanged(nameof(ChartMinHeight));
    OnPropertyChanged(nameof(Series));
    OnPropertyChanged(nameof(Legend));
    OnPropertyChanged(nameof(Labels));
  }

  protected static SKColor GetColorForShip(int shipId)
  {
    var baseColor = ChartPalette.PickForInt(shipId);
    return ChartPalette.WithAlpha(baseColor, 200);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  public sealed record LegendItem(string Name, ISolidColorBrush Brush);
}
