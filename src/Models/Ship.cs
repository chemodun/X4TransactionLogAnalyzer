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
  public string? Time { get; set; }
  public string? Station { get; set; }
  public string? Operation { get; set; }
  public string? Product { get; set; }
  public decimal? Price { get; set; }
  public int? Quantity { get; set; }
  public decimal? Total { get; set; }
  public decimal? EstimatedProfit { get; set; }
}
