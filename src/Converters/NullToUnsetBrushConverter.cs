using System;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace X4PlayerShipTradeAnalyzer.Converters;

// Returns UnsetValue when the input is null so the target property inherits its default/theme value.
public sealed class NullToUnsetBrushConverter : IValueConverter
{
  public static readonly NullToUnsetBrushConverter Instance = new();

  public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
  {
    return value is null ? AvaloniaProperty.UnsetValue : value;
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
  {
    return value; // one-way usage only
  }
}
