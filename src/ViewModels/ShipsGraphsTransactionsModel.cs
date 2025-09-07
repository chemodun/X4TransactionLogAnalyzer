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

  protected string AppendWhereOnFilters(string baseQuery)
  {
    var filters = new System.Collections.Generic.List<string>();
    if (IsContainerChecked)
      filters.Add("transport == 'container'");
    if (IsSolidChecked)
      filters.Add("transport == 'solid'");
    if (filters.Count > 0)
      return $"{baseQuery} WHERE {string.Join(" OR ", filters)}";
    return baseQuery;
  }

  protected override void LoadShips()
  {
    using var conn = MainWindow.GameData.Connection;
    ShipList.Clear();
    _shipItemsById.Clear();
    // Include EstimatedProfit per ship for sorting/display
    var shipCmd = new SQLiteCommand(
      AppendWhereOnFilters("SELECT id, full_name, SUM(profit) AS est_profit FROM player_ships_transactions_log")
        + " GROUP BY id, full_name ORDER BY full_name",
      conn
    );
    using var shipReader = shipCmd.ExecuteReader();
    while (shipReader.Read())
    {
      var item = new GraphShipItem
      {
        ShipId = Convert.ToInt32(shipReader["id"]),
        ShipName = shipReader["full_name"].ToString() ?? string.Empty,
        EstimatedProfit = shipReader.IsDBNull(2) ? 0m : Convert.ToDecimal(shipReader.GetDouble(2)),
      };
      // default (inactive) ships: GraphBrush left null to use theme default
      ShipList.Add(item);
      _shipItemsById[item.ShipId] = item;
    }
    // Apply current sort
    ResortShips();
  }

  protected override List<LiveChartsCore.Defaults.ObservablePoint> LoadCumulativeProfitPoints(int shipId)
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
}
