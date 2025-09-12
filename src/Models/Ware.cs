using System.ComponentModel;
using X4PlayerShipTradeAnalyzer.Services;

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

public class GraphWareItem : INotifyPropertyChanged
{
  public string Name { get; init; } = string.Empty;

  protected decimal _money;
  public decimal Money
  {
    get => _money;
    set
    {
      if (_money != value)
      {
        _money = value;
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
