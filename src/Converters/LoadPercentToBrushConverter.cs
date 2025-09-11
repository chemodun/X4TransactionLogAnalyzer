using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace X4PlayerShipTradeAnalyzer.Converters;

public sealed class LoadPercentToBrushConverter : IValueConverter
{
  public static readonly LoadPercentToBrushConverter Instance = new();

  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is decimal percent)
    {
      int bucket = Utils.LoadPercentPalette.ToBucket(percent);
      return Utils.LoadPercentPalette.GetBrush(bucket);
    }

    if (
      Application.Current is { } app
      && app.Resources.TryGetValue("ThemeForegroundBrush", out var brushObj)
      && brushObj is IBrush themeBrush
    )
    {
      return themeBrush;
    }
    return Brushes.Black;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
