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
    public int StartTime { get; init; }
    public int EndTime { get; init; }
    public string Time { get; init; } = string.Empty;

    // Convenience computed duration (ms) between EndTime and StartTime
    public int SpentTimeRaw => Math.Abs(EndTime - StartTime);
    public string SpentTime => TimeFormatter.FormatHms(SpentTimeRaw);
    public long BoughtVolume { get; init; }
    public long SoldVolume { get; init; }

    // Monetary values in credits (not cents)
    public decimal TotalBuyCost { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal Profit => TotalRevenue - TotalBuyCost;

    // Ordered lists of legs where the ware was bought/sold by this ship during the segment
    public TradeLeg[] Purchases { get; init; } = Array.Empty<TradeLeg>();
    public TradeLeg[] Sales { get; init; } = Array.Empty<TradeLeg>();
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
