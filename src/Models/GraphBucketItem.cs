using System.ComponentModel;
using Avalonia.Media;

namespace X4PlayerShipTradeAnalyzer.Models;

public class GraphBucketItem : INotifyPropertyChanged
{
  public string Name { get; init; } = string.Empty; // e.g. "0-10%"

  private decimal _percent;
  public decimal Percent
  {
    get => _percent;
    set
    {
      if (_percent != value)
      {
        _percent = value;
        OnPropertyChanged();
      }
    }
  }

  private IBrush? _graphBrush;
  public IBrush? GraphBrush
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

  private FontWeight _fontWeight = FontWeight.Normal;
  public FontWeight FontWeight
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
