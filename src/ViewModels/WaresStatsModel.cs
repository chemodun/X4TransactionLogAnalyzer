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

public sealed class WaresStatsModel : INotifyPropertyChanged
{
  public ObservableCollection<ISeries> Series { get; } = new();
  public ObservableCollection<LegendItem> Legend { get; } = new();
  public Axis[] XAxes { get; }
  public Axis[] YAxes { get; }

  private List<string> _labels = new();
  public IReadOnlyList<string> Labels => _labels;

  private bool isContainerChecked = true;
  public bool IsContainerChecked
  {
    get => isContainerChecked;
    set
    {
      if (!value && !isSolidChecked)
      {
        IsSolidChecked = true; // enforce at least one
        return;
      }
      if (isContainerChecked == value)
        return;
      isContainerChecked = value;
      OnPropertyChanged(nameof(IsContainerChecked));
      Reload();
    }
  }

  private bool isSolidChecked;
  public bool IsSolidChecked
  {
    get => isSolidChecked;
    set
    {
      if (!value && !isContainerChecked)
      {
        IsContainerChecked = true; // enforce at least one
        return;
      }
      if (isSolidChecked == value)
        return;
      isSolidChecked = value;
      OnPropertyChanged(nameof(IsSolidChecked));
      Reload();
    }
  }

  public WaresStatsModel()
  {
    XAxes = new[]
    {
      new Axis
      {
        Name = "Wares",
        // hide labels and title: no label paint and no name paint
        LabelsPaint = null,
        // NamePaint = null,
        SeparatorsPaint = new SolidColorPaint(new SKColor(220, 220, 220)) { StrokeThickness = 1 },
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

  private string AppendWhereOnFilters(string baseQuery)
  {
    var filters = new List<string>();
    if (IsContainerChecked)
      filters.Add("transport == 'container'");
    if (IsSolidChecked)
      filters.Add("transport == 'solid'");
    if (filters.Count > 0)
      return $"{baseQuery} WHERE {string.Join(" OR ", filters)}";
    return baseQuery;
  }

  public void Refresh() => Reload();

  private void Reload()
  {
    var labels = new List<string>();
    var rows = new List<(string WareId, string WareName, double Profit)>();

    using var conn = MainWindow.GameData.Connection;
    // sum profit per ware from the prepared view
    var cmd = new SQLiteCommand(
      AppendWhereOnFilters("SELECT ware, ware_name, SUM(profit) AS total_profit FROM player_ships_transactions_log")
        + " GROUP BY ware, ware_name ORDER BY total_profit DESC",
      conn
    );
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      var id = reader["ware"].ToString() ?? string.Empty;
      var name = reader["ware_name"].ToString() ?? id;
      var profit = Convert.ToDouble(reader["total_profit"]);
      rows.Add((id, name, profit));
    }

    // order already desc by profit; compose labels from rows
    labels.AddRange(rows.Select(r => r.WareName));

    // Build a single series and color columns per-point
    Series.Clear();
    Legend.Clear();

    var values = new List<LiveChartsCore.Defaults.ObservablePoint>(rows.Count);
    for (int i = 0; i < rows.Count; i++)
    {
      var row = rows[i];
      values.Add(new LiveChartsCore.Defaults.ObservablePoint(i, row.Profit));
      // populate our custom legend with the same color used for the column
      var sk = GetColorForWare(row.WareId);
      Legend.Add(new LegendItem(row.WareName, new SolidColorBrush(Color.FromArgb(sk.Alpha, sk.Red, sk.Green, sk.Blue))));
    }

    var colSeries = new ColumnSeries<LiveChartsCore.Defaults.ObservablePoint, RectGeometry>
    {
      Values = values,
      Stroke = null,
      // Make the series Fill transparent to hide the default tooltip color bullet
      Fill = new SolidColorPaint(new SKColor(0, 0, 0, 0)),
      MaxBarWidth = 48,
      // Show ware name for X and profit for Y in tooltip
      XToolTipLabelFormatter = cp =>
      {
        var idx = (int)Math.Round(cp.Coordinate.SecondaryValue);
        return (idx >= 0 && idx < rows.Count) ? rows[idx].WareName : string.Empty;
      },
      YToolTipLabelFormatter = cp => cp.Coordinate.PrimaryValue.ToString("N2"),
    };
    colSeries.PointMeasured += p =>
    {
      // color each rectangle based on its X index
      var idx = (int)Math.Round(p.Coordinate.SecondaryValue);
      if (idx >= 0 && idx < rows.Count && p.Visual is RectGeometry rect)
      {
        var c = GetColorForWare(rows[idx].WareId);
        rect.Fill = new SolidColorPaint(c);
        rect.Stroke = null;
      }
    };

    Series.Add(colSeries);

    OnPropertyChanged(nameof(Series));
    OnPropertyChanged(nameof(Legend));
  }

  private static readonly SKColor[] Palette = new[]
  {
    SKColors.DodgerBlue,
    SKColors.OrangeRed,
    SKColors.MediumSeaGreen,
    SKColors.MediumOrchid,
    SKColors.Goldenrod,
    SKColors.CadetBlue,
    SKColors.Tomato,
    SKColors.DeepSkyBlue,
    SKColors.MediumVioletRed,
    SKColors.SlateBlue,
    SKColors.SteelBlue,
    SKColors.LightSeaGreen,
    SKColors.DarkKhaki,
    SKColors.IndianRed,
    SKColors.Teal,
    // Extra colors
    SKColors.Crimson,
    SKColors.Coral,
    SKColors.DarkCyan,
    SKColors.DarkOrange,
    SKColors.DarkSalmon,
    SKColors.ForestGreen,
    SKColors.HotPink,
    SKColors.Khaki,
    SKColors.LawnGreen,
    SKColors.LightCoral,
    SKColors.LightPink,
    SKColors.LightSkyBlue,
    SKColors.LimeGreen,
    SKColors.MediumAquamarine,
    SKColors.MediumPurple,
    SKColors.Orchid,
    SKColors.PaleVioletRed,
    SKColors.Peru,
    SKColors.Plum,
    SKColors.RosyBrown,
    SKColors.SandyBrown,
    SKColors.SeaGreen,
    SKColors.Sienna,
    SKColors.SpringGreen,
    SKColors.Turquoise,
    SKColors.Violet,
    SKColors.YellowGreen,
  };

  private static SKColor GetColorForWare(string wareId)
  {
    if (string.IsNullOrEmpty(wareId))
      return new SKColor(33, 150, 243, 200);
    unchecked
    {
      int hash = 17;
      foreach (var ch in wareId)
        hash = hash * 31 + ch;
      var idx = Math.Abs(hash) % Palette.Length;
      var baseColor = Palette[idx];
      return new SKColor(baseColor.Red, baseColor.Green, baseColor.Blue, 200);
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  public sealed record LegendItem(string Name, ISolidColorBrush Brush);
}
