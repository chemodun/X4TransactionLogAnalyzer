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

  protected override List<(string WareId, string WareName, double Profit)> LoadData()
  {
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

    return rows;
  }
}
