using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using LiveChartsCore.Measure;

namespace X4PlayerShipTradeAnalyzer.Converters;

// Converts a bool to Hidden (false) or Bottom (true) for either TooltipPosition or LegendPosition.
// Re-usable in XAML: TooltipPosition="{Binding ShowToolTip, Converter={StaticResource BoolToChartPos}}"
//                    LegendPosition="{Binding ShowToolTip, Converter={StaticResource BoolToChartPos}}"
public sealed class BoolToChartPositionConverter : IValueConverter
{
  public static readonly BoolToChartPositionConverter Instance = new();

  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    var flag = value is true;
    // TooltipPosition and LegendPosition both have Hidden and Bottom members.
    if (targetType == typeof(TooltipPosition))
      return flag ? TooltipPosition.Bottom : TooltipPosition.Hidden;
    if (targetType == typeof(LegendPosition))
      return flag ? LegendPosition.Bottom : LegendPosition.Hidden;
    // Fallback: try returning enum names (LiveCharts will parse if bound as object)
    return flag ? "Bottom" : "Hidden";
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    return BindingOperations.DoNothing; // one-way only
  }
}
