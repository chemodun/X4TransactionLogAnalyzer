using System;
using X4PlayerShipTradeAnalyzer.Services;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.Models
{
  public sealed class FullTrade
  {
    public static bool IsInternalTrade(FullTrade ft)
    {
      bool onlyInternalBuy = ft.Purchases?.All(l => string.Equals(l.StationOwner, "player", StringComparison.OrdinalIgnoreCase)) ?? false;
      bool onlyInternalSell = ft.Sales?.All(l => string.Equals(l.StationOwner, "player", StringComparison.OrdinalIgnoreCase)) ?? false;
      return onlyInternalBuy && onlyInternalSell;
    }

    public static void GetFullTrades(ref List<FullTrade> fullTrades)
    { //MIC-510
      var conn = MainWindow.GameData.Connection;
      List<FullTrade> trades = fullTrades;
      if (conn == null)
        return;
      // Use the pre-resolved player_ships_transactions_log view for ship trades; it already resolves station and sector.
      using var cmd = conn.CreateCommand();
      cmd.CommandText =
        @"
SELECT
  id,
  code,
  name,
  time,
  ware,
  ware_name,
  operation,
  price,
  volume,
  counterpart_faction,
  sector,
  station,
  counterpart_code
FROM player_ships_transactions_log
ORDER BY id, time";
      using var rdr = cmd.ExecuteReader();
      fullTrades.Clear();
      long currentShip = -1;
      string currentWare = string.Empty;
      // State of an accumulating segment
      bool inSegment = false;
      bool sellingStarted = false;
      long cumVolume = 0; // positive while buying, decreases during selling
      FullTrade? trade = null;

      // Ordered legs for this segment
      var purchases = new List<TradeLeg>();
      var sales = new List<TradeLeg>();

      void Reset()
      {
        inSegment = false;
        sellingStarted = false;
        cumVolume = 0;
        purchases.Clear();
        sales.Clear();
        trade = null;
      }

      void SaveAndReset()
      {
        // Segment complete: emit trade
        if (trade != null && trade.BoughtVolume > 0 && trade.SoldVolume > 0)
        {
          int boughtIndex = purchases.Count - 1;
          if (trade.BoughtVolume > trade.SoldVolume)
          {
            var purchasesNew = new List<TradeLeg>();
            trade.BoughtVolume = 0;
            trade.TotalBuyCost = 0m;
            while (boughtIndex >= 0 && trade.BoughtVolume != trade.SoldVolume)
            {
              var leg = purchases[boughtIndex];
              purchasesNew.Insert(0, leg);
              trade.BoughtVolume += leg.Volume;
              trade.TotalBuyCost += leg.Volume * leg.Price;
              boughtIndex--;
            }
            if (trade.BoughtVolume != trade.SoldVolume)
            {
              // Should not happen, but just in case
              Reset();
              return;
            }
            purchases = purchasesNew;
            trade.StartTime = purchases.First().Time;
            trade.Time = Services.TimeFormatter.FormatHms(trade.StartTime);
          }
          if (trade.BoughtVolume == trade.SoldVolume)
          {
            trade.Purchases = purchases.ToArray();
            trade.Sales = sales.ToArray();
            trades.Add(trade);
          }
        }
        Reset();
      }

      while (rdr.Read())
      {
        var shipId = rdr.GetInt64(0);
        var code = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
        var name = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
        var time = (int)rdr.GetInt64(3);
        var ware = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4);
        var wareName = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5);
        var operation = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6);
        var price = rdr.IsDBNull(7) ? 0m : Convert.ToDecimal(rdr.GetDouble(7));
        var volume = rdr.IsDBNull(8) ? 0L : rdr.GetInt64(8);
        // No counterpart_id in the view
        var stationOwner = rdr.IsDBNull(9) ? string.Empty : rdr.GetString(9);
        var stationSector = rdr.IsDBNull(10) ? string.Empty : rdr.GetString(10);
        var stationName = rdr.IsDBNull(11) ? string.Empty : rdr.GetString(11);
        var stationCode = rdr.IsDBNull(12) ? string.Empty : rdr.GetString(12);

        bool shipChanged = shipId != currentShip;
        bool wareChanged = !string.Equals(ware, currentWare, StringComparison.OrdinalIgnoreCase);

        if (
          shipChanged
          || wareChanged
          || (inSegment && string.Equals(operation, "buy", StringComparison.OrdinalIgnoreCase) && sellingStarted)
        )
        {
          // If we switch context and have a completed segment, drop incomplete ones; only emit on zero balance
          SaveAndReset();
          currentShip = shipId;
          currentWare = ware;
        }

        // We only consider sequences that start with buys
        if (!inSegment)
        {
          if (string.Equals(operation, "buy", StringComparison.OrdinalIgnoreCase))
          {
            trade = new FullTrade
            {
              ShipId = currentShip,
              ShipCode = code,
              ShipName = name,
              WareId = currentWare,
              WareName = wareName,
              StartTime = time,
              Time = Services.TimeFormatter.FormatHms(time),
              EndTime = time,
              BoughtVolume = volume,
              SoldVolume = 0,
              TotalBuyCost = price * volume,
              TotalRevenue = 0,
            };
            inSegment = true;
            cumVolume = volume;
            if (volume > 0)
            {
              purchases.Add(
                new TradeLeg
                {
                  StationCode = stationCode,
                  StationOwner = stationOwner,
                  StationName = stationName,
                  Volume = volume,
                  Price = price,
                  Time = time,
                  Sector = stationSector,
                }
              );
            }
          }
          else
          {
            // Selling without inventory; ignore until a buy happens
          }
        }
        else if (trade != null)
        {
          // Inside a segment
          if (string.Equals(operation, "buy", StringComparison.OrdinalIgnoreCase))
          {
            // Another buy
            cumVolume += volume;
            trade.EndTime = time;
            trade.BoughtVolume += volume;
            trade.TotalBuyCost += price * volume;
            if (volume > 0)
            {
              purchases.Add(
                new TradeLeg
                {
                  StationCode = stationCode,
                  StationOwner = stationOwner,
                  StationName = stationName,
                  Volume = volume,
                  Price = price,
                  Time = time,
                  Sector = stationSector,
                }
              );
            }
          }
          else
          {
            // A sell
            // If selling more than available, clamp to available to keep non-negative inventory
            if (!sellingStarted)
              sellingStarted = true;
            long soldNow = Math.Min(volume, Math.Max(0, cumVolume));
            if (soldNow > 0)
            {
              cumVolume -= soldNow;
              trade.EndTime = time;
              trade.SoldVolume += soldNow;
              trade.TotalRevenue += price * soldNow;
              sales.Add(
                new TradeLeg
                {
                  StationCode = stationCode,
                  StationOwner = stationOwner,
                  StationName = stationName,
                  Volume = soldNow,
                  Price = price,
                  Time = time,
                  Sector = stationSector,
                }
              );
            }
          }
          if (cumVolume == 0 && trade != null && (trade.BoughtVolume > 0) && (trade.SoldVolume > 0))
          {
            SaveAndReset();
          }
        }
      }
    }

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

  public sealed class TradeStep
  {
    public long TimeRaw { get; init; }
    public string Time { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty; // buy/sell
    public string Station { get; init; } = string.Empty;
    public string Sector { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public long Volume { get; init; }
  }
}
