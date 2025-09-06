using System;

namespace X4PlayerShipTradeAnalyzer.Services
{
  /// <summary>
  /// Utility helpers to format time values as HH:MM:SS strings consistently across the app.
  /// </summary>
  public static class TimeFormatter
  {
    /// <summary>
    /// Formats a duration given in milliseconds into H:MM:SS. Hours are flattened (TotalHours).
    /// If milliseconds is negative, the result is prefixed with '-' and absolute value is formatted.
    /// </summary>
    /// <param name="milliseconds">Duration in milliseconds.</param>
    /// <param name="groupHours">When true, format hours with thousands separators (e.g. 1,234:56:07).</param>
    /// <param name="alwaysShowPlus">When true and value is non-negative, prefix "+".</param>
    public static string FormatHms(long milliseconds, bool groupHours = true, bool alwaysShowPlus = false)
    {
      string sign = string.Empty;
      if (milliseconds < 0)
      {
        sign = "-";
        milliseconds = -milliseconds;
      }
      else if (alwaysShowPlus)
      {
        sign = "+";
      }

      var ts = TimeSpan.FromMilliseconds(milliseconds);
      int hours = (int)ts.TotalHours;
      string hoursStr = groupHours ? hours.ToString("N0") : hours.ToString("D2");
      return $"{sign}{hoursStr}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
  }
}
