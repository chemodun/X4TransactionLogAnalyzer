namespace X4PlayerShipTradeAnalyzer.Models;

public class Ware
{
  public string Id { get; set; } = string.Empty; // xml id, db ware
  public string Name { get; set; } = string.Empty; // display name (can be same as Id)
  public string Transport { get; set; } = string.Empty; // Container/Liquid/Solid/...
  public double Volume { get; set; }
  public double PriceMin { get; set; }
  public double PriceAvg { get; set; }
  public double PriceMax { get; set; }
}
