using System.ComponentModel;

namespace X4PlayerShipTradeAnalyzer.Models;

public class ShipInfo
{
  public int ShipId { get; set; }
  public string? ShipName { get; set; }
  public decimal? EstimatedProfit { get; set; }
}

public class ShipTransaction
{
  public int ShipId { get; set; }

  // raw numeric fields for aggregation
  public long RawTime { get; set; }
  public string? Time { get; set; }
  public string? Sector { get; set; }
  public string? Station { get; set; }
  public string? Operation { get; set; }
  public string? Product { get; set; }
  public decimal? Price { get; set; }
  public int? Quantity { get; set; }
  public decimal? Total { get; set; }
  public decimal? EstimatedProfit { get; set; }
  public int? MaxQuantity { get; set; }
  public decimal? LoadPercent { get; set; }
}

public class GraphShipItem : INotifyPropertyChanged
{
  public int ShipId { get; init; }
  public string ShipName { get; init; } = string.Empty;

  protected decimal _estimatedProfit;
  public decimal EstimatedProfit
  {
    get => _estimatedProfit;
    set
    {
      if (_estimatedProfit != value)
      {
        _estimatedProfit = value;
        OnPropertyChanged();
      }
    }
  }

  private bool _isActive;
  public bool IsActive
  {
    get => _isActive;
    set
    {
      if (_isActive != value)
      {
        _isActive = value;
        OnPropertyChanged();
      }
    }
  }

  private Avalonia.Media.IBrush? _graphBrush;
  public Avalonia.Media.IBrush? GraphBrush
  {
    get => _graphBrush;
    set
    {
      if (_graphBrush != value)
      {
        _graphBrush = value;
        OnPropertyChanged();
      }
    }
  }

  private Avalonia.Media.FontWeight _fontWeight = Avalonia.Media.FontWeight.Normal;
  public Avalonia.Media.FontWeight FontWeight
  {
    get => _fontWeight;
    set
    {
      if (_fontWeight != value)
      {
        _fontWeight = value;
        OnPropertyChanged();
      }
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum ShipSortOrder
{
  Name,
  Profit,
}

public enum ShipClassFilter
{
  All,
  XL,
  L,
  M,
  S,
}

public static class ShipClassFilterUtil
{
  private static readonly string[] _ordered = new[]
  {
    nameof(ShipClassFilter.All),
    nameof(ShipClassFilter.XL),
    nameof(ShipClassFilter.L),
    nameof(ShipClassFilter.M),
    nameof(ShipClassFilter.S),
  };

  public static IEnumerable<string> GetShipClassOptions() => _ordered;

  public static string Normalize(string raw)
  {
    if (string.IsNullOrWhiteSpace(raw))
      return string.Empty;
    if (raw.StartsWith("ship_", StringComparison.OrdinalIgnoreCase))
      raw = raw[5..];
    var up = raw.ToUpperInvariant();
    // Ensure matches enum names (S,M,L,XL) else fallback to upper raw
    return up switch
    {
      "S" => "S",
      "M" => "M",
      "L" => "L",
      "XL" => "XL",
      _ => up,
    };
  }
}
