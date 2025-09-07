using System;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace X4PlayerShipTradeAnalyzer.Converters;

// Returns Red when value == 0 AND disabled flag is false; otherwise Unset
public sealed class ZeroToRedIfEnabledConverter : IMultiValueConverter
{
  public static readonly ZeroToRedIfEnabledConverter Instance = new();

  public object? Convert(
    System.Collections.Generic.IList<object?> values,
    Type targetType,
    object? parameter,
    System.Globalization.CultureInfo? culture
  )
  {
    try
    {
      if (values == null || values.Count < 2)
        return AvaloniaProperty.UnsetValue;
      var value = values[0];
      var disabled = !(values[1] is bool b && b);
      if (disabled)
        return AvaloniaProperty.UnsetValue;

      double d;
      if (value is null)
        return AvaloniaProperty.UnsetValue;
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
}
