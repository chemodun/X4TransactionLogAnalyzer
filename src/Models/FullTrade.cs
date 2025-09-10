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

    public static void GetFullTrades(ref List<FullTrade> fullTrades, List<Transaction> allTransactions)
    { //MIC-510
      List<FullTrade> trades = fullTrades;
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
        if (trade != null && trade.BoughtQuantity > 0 && trade.SoldQuantity > 0)
        {
          int boughtIndex = purchases.Count - 1;
          if (trade.BoughtQuantity > trade.SoldQuantity)
          {
            var purchasesNew = new List<TradeLeg>();
            trade.BoughtQuantity = 0;
            trade.TotalBuyCost = 0m;
            while (boughtIndex >= 0 && trade.BoughtQuantity != trade.SoldQuantity)
            {
              var leg = purchases[boughtIndex];
              purchasesNew.Insert(0, leg);
              trade.BoughtQuantity += leg.Volume;
              trade.TotalBuyCost += leg.Volume * leg.Price;
              boughtIndex--;
            }
            if (trade.BoughtQuantity != trade.SoldQuantity)
            {
              // Should not happen, but just in case
              Reset();
              return;
            }
            purchases = purchasesNew;
            trade.StartTime = purchases.First().Time;
            trade.Time = Services.TimeFormatter.FormatHms(trade.StartTime);
          }
          if (trade.BoughtQuantity == trade.SoldQuantity)
          {
            trade.Purchases = purchases.ToArray();
            trade.Sales = sales.ToArray();
            trades.Add(trade);
          }
        }
        Reset();
      }

      foreach (var trans in allTransactions.OrderBy(t => t.ShipId).ThenBy(t => t.RawTime))
      {
        var shipId = trans.ShipId;
        var code = trans.StationCode;
        var fullName = trans.FullName;
        var time = (int)trans.RawTime;
        var ware = trans.Ware;
        var wareName = trans.Product;
        var operation = trans.Operation;
        var price = trans.Price;
        var volume = trans.Quantity;
        var maxQuantity = trans.MaxQuantity;
        // No counterpart_id in the view
        var stationOwner = trans.StationOwner;
        var stationSector = trans.Sector;
        var stationName = trans.Station;
        var stationCode = trans.StationCode;

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
              ShipFullName = fullName,
              Ware = currentWare,
              Product = wareName,
              StartTime = time,
              Time = Services.TimeFormatter.FormatHms(time),
              EndTime = time,
              BoughtQuantity = volume,
              SoldQuantity = 0,
              TotalBuyCost = price * volume,
              TotalRevenue = 0,
              MaxQuantity = maxQuantity,
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
            trade.BoughtQuantity += volume;
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
              trade.SoldQuantity += soldNow;
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
          if (cumVolume == 0 && trade != null && (trade.BoughtQuantity > 0) && (trade.SoldQuantity > 0))
          {
            SaveAndReset();
          }
        }
      }
    }

    public long ShipId { get; init; }
    public string ShipFullName { get; init; } = string.Empty;
    public string Ware { get; init; } = string.Empty;
    public string Product { get; init; } = string.Empty;
    public int StartTime { get; set; }
    public int EndTime { get; set; }
    public string Time { get; set; } = string.Empty;

    // Convenience computed duration (ms) between EndTime and StartTime
    public int SpentTimeRaw => Math.Abs(EndTime - StartTime);
    public string SpentTime => TimeFormatter.FormatHms(SpentTimeRaw);
    public long BoughtQuantity { get; set; }
    public long SoldQuantity { get; set; }

    // Monetary values in credits (not cents)
    public decimal TotalBuyCost { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal Profit => TotalRevenue - TotalBuyCost;
    public int MaxQuantity { get; set; }

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
