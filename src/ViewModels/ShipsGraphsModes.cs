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

public class ShipsGraphsModel : INotifyPropertyChanged
{
  public class ShipListItem : INotifyPropertyChanged
  {
    public int ShipId { get; init; }
    public string ShipName { get; init; } = string.Empty;

    private bool _isActive;
    public bool IsActive
    {
      get => _isActive;
      set
      {
        if (_isActive != value)
        {
          _isActive = value;
          OnPropertyChanged(nameof(IsActive));
        }
      }
    }

    private IBrush? _graphBrush;
    public IBrush? GraphBrush
    {
      get => _graphBrush;
      set
      {
        if (_graphBrush != value)
        {
          _graphBrush = value;
          OnPropertyChanged(nameof(GraphBrush));
        }
      }
    }

    private FontWeight _fontWeight = FontWeight.Normal;
    public FontWeight FontWeight
    {
      get => _fontWeight;
      set
      {
        if (_fontWeight != value)
        {
          _fontWeight = value;
          OnPropertyChanged(nameof(FontWeight));
        }
      }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }

  public ObservableCollection<ShipListItem> ShipList { get; } = new();
  public ObservableCollection<ISeries> Series { get; } = new();

  public Axis[] XAxes { get; }
  public Axis[] YAxes { get; }

  private readonly HashSet<int> _activeShipIds = new();
  private readonly Dictionary<int, ISeries> _seriesByShipId = new();
  private readonly Dictionary<int, SKColor> _colorByShipId = new();
  private readonly Dictionary<int, ShipListItem> _shipItemsById = new();

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
      ReloadShipsAndRebuildActiveSeries();
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
      ReloadShipsAndRebuildActiveSeries();
    }
  }

  public ShipsGraphsModel()
  {
    XAxes = new[]
    {
      new Axis
      {
        Name = "Time",
        Labeler = value => FormatTimeLabel(value),
        LabelsRotation = 0,
        SeparatorsPaint = new SolidColorPaint(new SKColor(220, 220, 220)) { StrokeThickness = 1 },
      },
    };
    YAxes = new[]
    {
      new Axis
      {
        Name = "Cumulative Profit",
        Labeler = value => value.ToString("N2"),
        SeparatorsPaint = new SolidColorPaint(new SKColor(220, 220, 220)) { StrokeThickness = 1 },
      },
    };

    LoadShips();
  }

  private static string FormatTimeLabel(double value)
  {
    var ms = Math.Abs(value);
    var ts = TimeSpan.FromMilliseconds(ms);
    return $"-{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
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

  private void LoadShips()
  {
    using var conn = MainWindow.GameData.Connection;
    ShipList.Clear();
    _shipItemsById.Clear();
    var shipCmd = new SQLiteCommand(
      AppendWhereOnFilters("SELECT DISTINCT id, full_name FROM player_ships_transactions_log") + " ORDER BY full_name",
      conn
    );
    using var shipReader = shipCmd.ExecuteReader();
    while (shipReader.Read())
    {
      var item = new ShipListItem
      {
        ShipId = Convert.ToInt32(shipReader["id"]),
        ShipName = shipReader["full_name"].ToString() ?? string.Empty,
      };
      // default (inactive) ships: GraphBrush left null to use theme default
      ShipList.Add(item);
      _shipItemsById[item.ShipId] = item;
    }
  }

  public void Refresh()
  {
    ReloadShipsAndRebuildActiveSeries();
  }

  private void ReloadShipsAndRebuildActiveSeries()
  {
    LoadShips();
    var ids = _activeShipIds.ToList();
    Series.Clear();
    _seriesByShipId.Clear();
    _activeShipIds.Clear();
    foreach (var id in ids)
    {
      if (_shipItemsById.TryGetValue(id, out var ship))
      {
        ToggleShip(ship);
      }
    }
  }

  public void ToggleShip(ShipListItem ship)
  {
    if (_activeShipIds.Contains(ship.ShipId))
    {
      if (_seriesByShipId.TryGetValue(ship.ShipId, out var s))
      {
        Series.Remove(s);
        _seriesByShipId.Remove(ship.ShipId);
      }
      _activeShipIds.Remove(ship.ShipId);
      ship.IsActive = false;
      ship.GraphBrush = null;
      ship.FontWeight = FontWeight.Normal;
      return;
    }

    var points = LoadCumulativeProfitPoints(ship.ShipId);
    // use the dot geometry color as the semantic color for the ship
    var dotColor = GetColorForShip(ship.ShipId);
    var lineColor = new SKColor(dotColor.Red, dotColor.Green, dotColor.Blue, 180);
    var line = new LineSeries<LiveChartsCore.Defaults.ObservablePoint>
    {
      Name = ship.ShipName,
      Values = points,
      GeometrySize = 6,
      Stroke = new SolidColorPaint(lineColor, 2),
      GeometryStroke = new SolidColorPaint(dotColor, 2),
      GeometryFill = new SolidColorPaint(new SKColor(dotColor.Red, dotColor.Green, dotColor.Blue, 160)),
      Fill = null,
    };
    Series.Add(line);
    _seriesByShipId[ship.ShipId] = line;
    _activeShipIds.Add(ship.ShipId);
    ship.IsActive = true;
    ship.GraphBrush = new SolidColorBrush(Color.FromRgb(dotColor.Red, dotColor.Green, dotColor.Blue));
    ship.FontWeight = FontWeight.Bold;
  }

  private List<LiveChartsCore.Defaults.ObservablePoint> LoadCumulativeProfitPoints(int shipId)
  {
    var list = new List<LiveChartsCore.Defaults.ObservablePoint>();
    using var conn = MainWindow.GameData.Connection;
    var cmd = new SQLiteCommand(
      AppendWhereOnFilters("SELECT time, profit FROM player_ships_transactions_log") + " AND id = @id ORDER BY time ASC",
      conn
    );
    cmd.Parameters.AddWithValue("@id", shipId);
    using var reader = cmd.ExecuteReader();
    decimal sum = 0m;
    while (reader.Read())
    {
      var t = Convert.ToInt64(reader["time"]); // negative ms
      var p = Convert.ToDecimal(reader["profit"]);
      sum += p;
      list.Add(new LiveChartsCore.Defaults.ObservablePoint(t, (double)sum));
    }
    return list;
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
  };

  private SKColor GetColorForShip(int shipId)
  {
    if (_colorByShipId.TryGetValue(shipId, out var color))
      return color;
    var idx = Math.Abs(shipId.GetHashCode()) % Palette.Length;
    color = Palette[idx];
    _colorByShipId[shipId] = color;
    return color;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
