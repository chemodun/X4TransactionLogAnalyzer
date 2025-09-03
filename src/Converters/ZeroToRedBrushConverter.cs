using System;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace X4PlayerShipTradeAnalyzer.Converters;

// Returns Red brush when the bound numeric value equals zero; otherwise lets the control use its default foreground.
public sealed class ZeroToRedBrushConverter : IValueConverter
{
  public static readonly ZeroToRedBrushConverter Instance = new();

  public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
  {
    try
    {
      if (value is null)
        return AvaloniaProperty.UnsetValue;
      // Try numeric conversions
      double d;
      if (value is int i)
        d = i;
      else if (value is long l)
        d = l;
      else if (value is float f)
        d = f;
      else if (value is double dd)
        d = dd;
      else if (value is string s && double.TryParse(s, out var parsed))
        d = parsed;
      else
        return AvaloniaProperty.UnsetValue;

      return Math.Abs(d) < double.Epsilon ? Brushes.Red : AvaloniaProperty.UnsetValue;
    }
    catch
    {
      return AvaloniaProperty.UnsetValue;
    }
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
  {
    return BindingOperations.DoNothing;
  }
}
