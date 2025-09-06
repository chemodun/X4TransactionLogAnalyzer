using System;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace X4PlayerShipTradeAnalyzer.Converters;

// Binds RadioButton.IsChecked to an enum property by comparing with ConverterParameter.
public sealed class EnumEqualsConverter : IValueConverter
{
  public static readonly EnumEqualsConverter Instance = new();

  public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
  {
    if (value is null || parameter is null)
      return false;
    return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
  {
    // When checked => set enum to parameter value; when unchecked => do nothing
    if (value is bool b && b && parameter is string s && targetType.IsEnum)
    {
      try
      {
        return Enum.Parse(targetType, s);
      }
      catch
      {
        return BindingOperations.DoNothing;
      }
    }
    return BindingOperations.DoNothing;
  }
}
