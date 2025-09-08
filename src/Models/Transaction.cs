using System;
using X4PlayerShipTradeAnalyzer.Services;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.Models;

public class Transaction
{
  public int ShipId { get; set; }
  public long RawTime { get; set; }
  public string FullName { get; set; } = string.Empty; // for display only
  public string Operation { get; set; } = string.Empty;
  public string Ware { get; set; } = string.Empty;
  public string Product { get; set; } = string.Empty;
  public string Transport { get; set; } = string.Empty;
  public string Station { get; set; } = string.Empty;
  public string Sector { get; set; } = string.Empty;
  public decimal Price { get; set; }
  public int Quantity { get; set; }
  public decimal Total { get; set; }
  public decimal EstimatedProfit { get; set; } // sign shows direction
  public string Time => TimeFormatter.FormatHms(RawTime);

  public static void GetAllTransactions(ref List<Transaction> transactions)
  { //MIC-510
    var conn = MainWindow.GameData.Connection;
    List<Transaction> trans = transactions;
    if (conn == null)
      return;
    using var cmd = conn.CreateCommand();
    cmd.CommandText =
      "SELECT id, full_name, time, sector, station, operation, ware, ware_name, transport, price, volume, trade_sum, profit FROM player_ships_transactions_log";
    using var rdr = cmd.ExecuteReader();
    trans.Clear();
    while (rdr.Read())
    {
      trans.Add(
        new Transaction
        {
          ShipId = Convert.ToInt32(rdr["id"]),
          RawTime = Convert.ToInt64(rdr["time"]),
          FullName = rdr["full_name"].ToString() ?? string.Empty,
          Sector = rdr["sector"].ToString() ?? string.Empty,
          Station = rdr["station"].ToString() ?? string.Empty,
          Operation = rdr["operation"].ToString() ?? string.Empty,
          Ware = rdr["ware"].ToString() ?? string.Empty,
          Product = rdr["ware_name"].ToString() ?? string.Empty,
          Transport = rdr["transport"].ToString() ?? string.Empty,
          Price = Convert.ToDecimal(rdr["price"]),
          Quantity = Convert.ToInt32(rdr["volume"]),
          Total = Convert.ToDecimal(rdr["trade_sum"]),
          EstimatedProfit = Convert.ToDecimal(rdr["profit"]),
        }
      );
    }
  }
}

public enum TransportFilter
{
  All,
  Container,
  Solid,
  Liquid,
}
