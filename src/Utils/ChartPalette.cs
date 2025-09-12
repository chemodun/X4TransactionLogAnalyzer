using SkiaSharp;

namespace X4PlayerShipTradeAnalyzer.Utils;

public static class ChartPalette
{
  // Unified palette used across charts (ships graphs, wares stats, etc.)
  public static readonly SKColor[] Colors = new[]
  {
    SKColors.DodgerBlue,
    SKColors.OrangeRed,
    SKColors.MediumSeaGreen,
    SKColors.MediumOrchid,
    SKColors.Goldenrod,
    SKColors.CadetBlue,
    SKColors.Tomato,
    SKColors.DeepSkyBlue,
    SKColors.MediumVioletRed,
    SKColors.SlateBlue,
    SKColors.SteelBlue,
    SKColors.LightSeaGreen,
    SKColors.DarkKhaki,
    SKColors.IndianRed,
    SKColors.Teal,
    // Extra colors
    SKColors.Crimson,
    SKColors.Coral,
    SKColors.DarkCyan,
    SKColors.DarkOrange,
    SKColors.DarkSalmon,
    SKColors.ForestGreen,
    SKColors.HotPink,
    SKColors.Khaki,
    SKColors.LawnGreen,
    SKColors.LightCoral,
    SKColors.LightPink,
    SKColors.LightSkyBlue,
    SKColors.LimeGreen,
    SKColors.MediumAquamarine,
    SKColors.MediumPurple,
    SKColors.Orchid,
    SKColors.PaleVioletRed,
    SKColors.Peru,
    SKColors.Plum,
    SKColors.RosyBrown,
    SKColors.SandyBrown,
    SKColors.SeaGreen,
    SKColors.Sienna,
    SKColors.SpringGreen,
    SKColors.Turquoise,
    SKColors.Violet,
    SKColors.YellowGreen,
  };

  public static int ComputeStableHash(string s)
  {
    unchecked
    {
      int hash = 17;
      foreach (var ch in s)
        hash = hash * 31 + ch;
      return hash;
    }
  }

  public static SKColor PickByHash(int hash)
  {
    var idx = System.Math.Abs(hash) % Colors.Length;
    return Colors[idx];
  }

  public static SKColor PickForString(string key) =>
    string.IsNullOrEmpty(key) ? new SKColor(33, 150, 243) : PickByHash(ComputeStableHash(key));

  public static SKColor PickForInt(int key) => PickByHash(key);

  public static SKColor PickForLong(long key) => PickByHash((int)key);

  public static SKColor WithAlpha(SKColor c, byte alpha) => new SKColor(c.Red, c.Green, c.Blue, alpha);
}
