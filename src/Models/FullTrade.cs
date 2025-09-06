using System;
using X4PlayerShipTradeAnalyzer.Services;

namespace X4PlayerShipTradeAnalyzer.Models
{
  public sealed class FullTrade
  {
    public long ShipId { get; init; }
    public string ShipCode { get; init; } = string.Empty;
    public string ShipName { get; init; } = string.Empty;
    public string WareId { get; init; } = string.Empty;
    public string WareName { get; init; } = string.Empty;
    public int StartTime { get; set; }
    public int EndTime { get; set; }
    public string Time { get; set; } = string.Empty;

    // Convenience computed duration (ms) between EndTime and StartTime
    public int SpentTimeRaw => Math.Abs(EndTime - StartTime);
    public string SpentTime => TimeFormatter.FormatHms(SpentTimeRaw);
    public long BoughtVolume { get; set; }
    public long SoldVolume { get; set; }

    // Monetary values in credits (not cents)
    public decimal TotalBuyCost { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal Profit => TotalRevenue - TotalBuyCost;

    // Ordered lists of legs where the ware was bought/sold by this ship during the segment
    public TradeLeg[] Purchases { get; set; } = Array.Empty<TradeLeg>();
    public TradeLeg[] Sales { get; set; } = Array.Empty<TradeLeg>();
  }

  public sealed class TradeLeg
  {
    public long StationId { get; init; }
    public string StationCode { get; init; } = string.Empty;
    public string StationName { get; init; } = string.Empty;
    public string StationOwner { get; init; } = string.Empty; // Faction owning the station (if resolved)

    // Sector human-readable name where the counterpart is located (if resolved)
    public string Sector { get; init; } = string.Empty;
    public long Volume { get; init; }
    public decimal Price { get; init; }
    public int Time { get; init; }
  }
}
