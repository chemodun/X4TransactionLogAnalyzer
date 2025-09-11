using System;
using Avalonia.Media;
using SkiaSharp;

namespace X4PlayerShipTradeAnalyzer.Utils;

public static class LoadPercentPalette
{
  private static readonly SKColor[] _skColors = new SKColor[10]
  {
    new SKColor(0x8B, 0x00, 0x00, 200), // 0-10 DarkRed
    new SKColor(0xB2, 0x22, 0x22, 200), // 10-20 Firebrick
    new SKColor(0xFF, 0x00, 0x00, 200), // 20-30 Red
    new SKColor(0xFF, 0x45, 0x00, 200), // 30-40 OrangeRed
    new SKColor(0xFF, 0x8C, 0x00, 200), // 40-50 DarkOrange
    new SKColor(0xDA, 0xA5, 0x20, 200), // 50-60 Goldenrod
    new SKColor(0x6B, 0x8E, 0x23, 200), // 60-70 OliveDrab
    new SKColor(0x9A, 0xCD, 0x32, 200), // 70-80 YellowGreen
    new SKColor(0x22, 0x8B, 0x22, 200), // 80-90 ForestGreen
    new SKColor(0x00, 0x80, 0x00, 200), // 90-100 Green
  };

  private static readonly SolidColorBrush[] _brushes = new SolidColorBrush[10];

  static LoadPercentPalette()
  {
    for (int i = 0; i < 10; i++)
    {
      var c = _skColors[i];
      _brushes[i] = new SolidColorBrush(Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue));
    }
  }

  public static int ToBucket(decimal percent)
  {
    if (percent < 0)
      percent = 0;
    if (percent > 100)
      percent = 100;
    return (int)Math.Min(9, Math.Floor(percent / 10m));
  }

  public static SKColor GetSkColor(int bucket) => _skColors[Math.Clamp(bucket, 0, 9)];

  public static IBrush GetBrush(int bucket) => _brushes[Math.Clamp(bucket, 0, 9)];
}
