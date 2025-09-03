namespace X4PlayerShipTradeAnalyzer.Models;

public class ShipInfo
{
  public int ShipId { get; set; }
  public string? ShipName { get; set; }
}

public class ShipTransaction
{
  public int ShipId { get; set; }

  // raw numeric fields for aggregation
  public long RawTimeMs { get; set; }
  public int VolumeValue { get; set; }
  public decimal ProfitValue { get; set; }
  public string? Time { get; set; }
  public string? Station { get; set; }
  public string? Operation { get; set; }
  public string? Product { get; set; }
  public string? Price { get; set; }
  public string? Quantity { get; set; }
  public string? Total { get; set; }
  public string? EstimatedProfit { get; set; }
}
