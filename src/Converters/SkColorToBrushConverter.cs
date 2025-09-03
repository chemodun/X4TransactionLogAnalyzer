using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SkiaSharp;

namespace X4PlayerShipTradeAnalyzer.Converters;

public sealed class SkColorToBrushConverter : IValueConverter
{
  public static readonly SkColorToBrushConverter Instance = new();

  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is SKColor c)
      return new SolidColorBrush(Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue));
    return AvaloniaProperty.UnsetValue;
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
