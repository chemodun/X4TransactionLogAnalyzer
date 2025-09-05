using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace X4PlayerShipTradeAnalyzer.Services
{
  /// <summary>
  /// Resolves installed DLCs under the X4 'extensions' folder and returns their relative paths
  /// sorted so prerequisites come before dependents.
  /// </summary>
  public static class DlcResolver
  {
    private static string? _cachedConfiguredExePath;
    private static List<string>? _cachedRelativePaths;

    /// <summary>
    /// Returns relative paths (from game root) to installed DLC folders under 'extensions',
    /// sorted so that items with no present dependencies come first, followed by their dependents.
    /// Caches the result by configuredExePath value.
    /// </summary>
    public static List<string> GetDLCsSorted(string? configuredExePath)
    {
      if (
        !string.IsNullOrWhiteSpace(_cachedConfiguredExePath)
        && string.Equals(_cachedConfiguredExePath, configuredExePath, StringComparison.OrdinalIgnoreCase)
        && _cachedRelativePaths != null
      )
      {
        return _cachedRelativePaths;
      }

      _cachedConfiguredExePath = configuredExePath;
      _cachedRelativePaths = new List<string>();
      if (string.IsNullOrWhiteSpace(configuredExePath))
      {
        return _cachedRelativePaths;
      }

      // Determine game root: allow either folder path or full exe path
      string? gameRoot = null;
      if (Directory.Exists(configuredExePath))
      {
        gameRoot = configuredExePath;
      }
      else if (File.Exists(configuredExePath))
      {
        gameRoot = Path.GetDirectoryName(configuredExePath);
      }
      if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
      {
        return _cachedRelativePaths;
      }

      var extensionsRoot = Path.Combine(gameRoot, "extensions");
      if (!Directory.Exists(extensionsRoot))
      {
        return _cachedRelativePaths;
      }

      // Discover DLC folders by mask ego_dlc_*
      var dlcDirs = Directory.EnumerateDirectories(extensionsRoot, "ego_dlc_*", SearchOption.TopDirectoryOnly).ToList();

      // Filter by presence of content.xml
      var installed = new List<(string Id, string AbsPath, string RelPath, string ContentXml)>();
      foreach (var dir in dlcDirs)
      {
        var contentXml = Path.Combine(dir, "content.xml");
        if (!File.Exists(contentXml))
          continue;
        var id = Path.GetFileName(dir) ?? string.Empty; // assume folder name equals content id
        if (string.IsNullOrWhiteSpace(id))
          continue;
        var rel = Path.Combine("extensions", id);
        installed.Add((id, dir, rel, contentXml));
      }

      if (installed.Count == 0)
      {
        return _cachedRelativePaths;
      }

      // Build dependency graph limited to installed set
      var installedIds = new HashSet<string>(installed.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
      var indegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // key: prerequisite, value: list of dependents

      foreach (var i in installed)
      {
        indegree[i.Id] = 0; // initialize
      }

      foreach (var i in installed)
      {
        // Parse content.xml dependencies of this DLC
        try
        {
          var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
          using var xr = XmlReader.Create(i.ContentXml, settings);
          while (xr.Read())
          {
            if (xr.NodeType == XmlNodeType.Element && xr.Name.Equals("dependency", StringComparison.OrdinalIgnoreCase))
            {
              var depId = xr.GetAttribute("id") ?? string.Empty;
              if (string.IsNullOrWhiteSpace(depId))
                continue;
              // Only consider deps that are installed; optional or missing are ignored for ordering
              if (!installedIds.Contains(depId))
                continue;
              // record edge depId -> i.Id
              if (!dependents.TryGetValue(depId, out var list))
              {
                list = new List<string>();
                dependents[depId] = list;
              }
              list.Add(i.Id);
              indegree[i.Id] = indegree.TryGetValue(i.Id, out var d) ? d + 1 : 1;
            }
          }
        }
        catch
        {
          // If parsing fails, treat as no dependencies
        }
      }

      // Kahn's algorithm with lexicographic tie-breaker
      var zero = new SortedSet<string>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key), StringComparer.OrdinalIgnoreCase);
      var order = new List<string>(installed.Count);
      while (zero.Count > 0)
      {
        var next = zero.Min!;
        zero.Remove(next);
        order.Add(next);
        if (dependents.TryGetValue(next, out var outs))
        {
          foreach (var v in outs.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
          {
            indegree[v] = indegree[v] - 1;
            if (indegree[v] == 0)
              zero.Add(v);
          }
        }
      }

      // If cycle exists or some nodes not processed, append them in name order to keep deterministic
      if (order.Count < installed.Count)
      {
        foreach (
          var leftover in installed
            .Select(i => i.Id)
            .Except(order, StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
        )
        {
          order.Add(leftover);
        }
      }

      // Map to relative paths in that order
      var relById = installed.ToDictionary(i => i.Id, i => i.RelPath, StringComparer.OrdinalIgnoreCase);
      var result = order.Where(id => relById.ContainsKey(id)).Select(id => relById[id]).ToList();

      _cachedRelativePaths = result;
      return result;
    }
  }
}
