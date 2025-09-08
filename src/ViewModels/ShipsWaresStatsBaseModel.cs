using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using X4PlayerShipTradeAnalyzer.Utils;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public abstract class ShipsWaresStatsBaseModel : INotifyPropertyChanged
{
  public ObservableCollection<ISeries> Series { get; } = new();
  public ObservableCollection<LegendItem> Legend { get; } = new();
  public Axis[] XAxes { get; }
  public Axis[] YAxes { get; }

  protected List<string> _labels = new();
  public IReadOnlyList<string> Labels => _labels;
#pragma warning disable CA1822 // Mark members as static
  public IEnumerable<TopNFilter> TopNOptions => Enum.GetValues(typeof(TopNFilter)).Cast<TopNFilter>();
#pragma warning restore CA1822 // Mark members as static
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
  public double ChartMinWidth => Labels.Count * 56 + 300;
  public double ChartMinHeight => Legend.Count * 32;

  protected ShipsWaresStatsBaseModel()
  {
    XAxes = new[]
    {
      new Axis
      {
        Name = "Ships",
        LabelsPaint = new SolidColorPaint(SKColors.Black), // or any color you want
        SeparatorsPaint = new SolidColorPaint(new SKColor(220, 220, 220)) { StrokeThickness = 1 },
        LabelsRotation = 90, // optional: rotate labels for readability
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
    // Sort ships by total profit desc
    var shipOrder = rows.GroupBy(r => (r.ShipId, r.ShipName))
      .Select(g => new
      {
        g.Key.ShipId,
        g.Key.ShipName,
        TotalProfit = g.Sum(x => x.Profit),
      })
      .OrderByDescending(x => x.TotalProfit)
      .ToList();

    // Apply TopN filter
    var topShips = shipOrder.Take((int)TopN).Select(x => x.ShipName).ToList();

    // Filter rows to only those ships
    rows = rows.Where(r => topShips.Contains(r.ShipName))
      .OrderBy(r => topShips.IndexOf(r.ShipName))
      .ThenBy(r => r.WareName, StringComparer.OrdinalIgnoreCase)
      .ToList();
    // Distinct ship and ware lists
    var ships = rows.Select(r => r.ShipName).Distinct().ToList();
    var wares = rows.Select(r => (r.WareId, r.WareName)).Distinct().ToList();

    _labels = ships;

    Series.Clear();
    Legend.Clear();

    foreach (var (wareId, wareName) in wares)
    {
      var values = new List<double?>(ships.Count);

      foreach (var ship in ships)
      {
        var profit = rows.Where(r => r.ShipName == ship && r.WareId == wareId).Sum(r => r.Profit);

        values.Add(profit == 0 ? null : profit);
      }

      var sk = GetColorForWare(wareId);

      var series = new StackedColumnSeries<double?>
      {
        Name = wareName,
        Values = values,
        Stroke = null,
        Fill = new SolidColorPaint(sk),
        MaxBarWidth = 48,

        // New API: separate formatters for X and Y
        XToolTipLabelFormatter = cp =>
        {
          // cp.Coordinate.SecondaryValue is the X index
          var shipIdx = (int)Math.Round(cp.Coordinate.SecondaryValue);
          return (shipIdx >= 0 && shipIdx < ships.Count) ? ships[shipIdx] : string.Empty;
        },
        YToolTipLabelFormatter = cp =>
        {
          // cp.Coordinate.PrimaryValue is the Y value
          return cp.Coordinate.PrimaryValue.ToString("N2");
        },
      };

      Series.Add(series);
      Legend.Add(new LegendItem(wareName, new SolidColorBrush(Color.FromArgb(sk.Alpha, sk.Red, sk.Green, sk.Blue))));
    }

    // Update X axis labels to ship names
    XAxes[0].Labels = ships;

    OnPropertyChanged(nameof(ChartMinWidth));
    OnPropertyChanged(nameof(ChartMinHeight));
    OnPropertyChanged(nameof(Series));
    OnPropertyChanged(nameof(Legend));
    OnPropertyChanged(nameof(Labels));
  }

  protected static SKColor GetColorForWare(string wareId)
  {
    var baseColor = ChartPalette.PickForString(wareId);
    return ChartPalette.WithAlpha(baseColor, 200);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  public sealed record LegendItem(string Name, ISolidColorBrush Brush);
}
