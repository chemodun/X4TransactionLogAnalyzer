using System;

namespace X4PlayerShipTradeAnalyzer.Models;

public class TradeOperation
{
  public double TimeSeconds { get; set; }
  public string ShipCode { get; set; } = string.Empty;
  public string ShipName { get; set; } = string.Empty;
  public string CounterpartCode { get; set; } = string.Empty;
  public string CounterpartName { get; set; } = string.Empty;
  public string CounterpartFaction { get; set; } = string.Empty;
  public string WareId { get; set; } = string.Empty; // references Ware.Id
  public int Volume { get; set; }
  public int Money { get; set; } // sign shows direction

  public string TimeFormatted => TimeSpan.FromSeconds(TimeSeconds).ToString(@"hh\:mm\:ss");
}
