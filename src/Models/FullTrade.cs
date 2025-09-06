using System;

namespace X4PlayerShipTradeAnalyzer.Models
{
  public sealed class FullTrade
  {
    public long ShipId { get; init; }
    public string ShipCode { get; init; } = string.Empty;
    public string ShipName { get; init; } = string.Empty;
    public string WareId { get; init; } = string.Empty;
    public int StartTime { get; init; }
    public int EndTime { get; init; }

    // Convenience computed duration (ms) between EndTime and StartTime
    public int SpentTime => EndTime - StartTime;
    public long BoughtVolume { get; init; }
    public long SoldVolume { get; init; }

    // Monetary values are in the same units as stored in trade table (typically cents)
    public long TotalBuyCost { get; init; }
    public long TotalRevenue { get; init; }
    public long Profit => TotalRevenue - TotalBuyCost;

    // Ordered lists of legs where the ware was bought/sold by this ship during the segment
    public TradeLeg[] Purchases { get; init; } = Array.Empty<TradeLeg>();
    public TradeLeg[] Sales { get; init; } = Array.Empty<TradeLeg>();
  }

  public sealed class TradeLeg
  {
    public long CounterpartId { get; init; }
    public string CounterpartCode { get; init; } = string.Empty;
    public string CounterpartName { get; init; } = string.Empty;

    // Sector human-readable name where the counterpart is located (if resolved)
    public string Sector { get; init; } = string.Empty;
    public long Volume { get; init; }
    public long Price { get; init; }
    public int Time { get; init; }
  }
}
