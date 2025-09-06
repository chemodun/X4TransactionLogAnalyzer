using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace X4PlayerShipTradeAnalyzer.Services
{
  /// <summary>
  /// Resolves installed extensions (DLCs and other mods) under the X4 'extensions' folder and
  /// returns their relative paths sorted so prerequisites come before dependents, with DLCs first.
  /// Rules:
  /// - DLCs are sorted (topologically) and returned first.
  /// - Other extensions (mods) are sorted (topologically) and returned after DLCs.
  /// - If an extension declares a non-optional dependency (optional missing or false) that is not installed,
  ///   the extension is skipped entirely.
  /// Dependency ids are resolved against the <content id="..."> value in each extension's content.xml.
  /// </summary>
  public static class ExtensionResolver
  {
    private static string? _cachedConfiguredExePath;
    private static List<string>? _cachedRelativePaths;

    /// <summary>
    /// Returns relative paths (from game root) to installed extension folders under 'extensions',
    /// with DLCs first (each group topologically sorted). Caches the result by configuredExePath.
    /// </summary>
    public static List<string> GetExtensionsSorted(string? configuredExePath)
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

      // Discover ALL extension folders (not only DLCs) under 'extensions'
      var allDirs = Directory.EnumerateDirectories(extensionsRoot, "*", SearchOption.TopDirectoryOnly).ToList();

      // Local model for an installed extension
      var installed = new List<Ext>();
      foreach (var dir in allDirs)
      {
        var contentXml = Path.Combine(dir, "content.xml");
        if (!File.Exists(contentXml))
          continue;

        string folderName = Path.GetFileName(dir) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(folderName))
          continue;

        string contentId = string.Empty;
        bool isDlc = false;
        try
        {
          var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
          using var xr = XmlReader.Create(contentXml, settings);
          while (xr.Read())
          {
            if (xr.NodeType == XmlNodeType.Element && xr.LocalName.Equals("content", StringComparison.OrdinalIgnoreCase))
            {
              contentId = xr.GetAttribute("id") ?? string.Empty;
              var typeAttr = xr.GetAttribute("type") ?? string.Empty;
              if (!string.IsNullOrWhiteSpace(typeAttr) && typeAttr.Equals("dlc", StringComparison.OrdinalIgnoreCase))
              {
                isDlc = true;
              }
              break;
            }
          }
        }
        catch
        {
          // ignore parse errors, fallback below
        }

        // Fallback: if id is missing in content.xml, use folder name to keep deterministic behavior
        if (string.IsNullOrWhiteSpace(contentId))
        {
          contentId = folderName;
        }

        // Additional fallback for DLC detection: official DLCs usually have folder prefix 'ego_dlc_'
        if (!isDlc && folderName.StartsWith("ego_dlc_", StringComparison.OrdinalIgnoreCase))
        {
          isDlc = true;
        }

        var rel = Path.Combine("extensions", folderName);
        installed.Add(new Ext(contentId, folderName, dir, rel, contentXml, isDlc));
      }

      if (installed.Count == 0)
      {
        return _cachedRelativePaths;
      }

      // Parse dependencies (id + optional) for each installed extension
      var installedIds = new HashSet<string>(installed.Select(i => i.ContentId), StringComparer.OrdinalIgnoreCase);
      foreach (var i in installed)
      {
        try
        {
          var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
          using var xr = XmlReader.Create(i.ContentXml, settings);
          while (xr.Read())
          {
            if (xr.NodeType == XmlNodeType.Element && xr.LocalName.Equals("dependency", StringComparison.OrdinalIgnoreCase))
            {
              var depId = xr.GetAttribute("id") ?? string.Empty;
              if (string.IsNullOrWhiteSpace(depId))
                continue;
              var optAttr = xr.GetAttribute("optional");
              bool optional = !string.IsNullOrEmpty(optAttr) && optAttr.Equals("true", StringComparison.OrdinalIgnoreCase);
              i.Deps.Add(new Dep(depId, optional));
            }
          }
        }
        catch
        {
          // If parsing fails, treat as no dependencies
        }
      }

      // Skip any extension that has a missing mandatory dependency
      var skipIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var i in installed)
      {
        foreach (var d in i.Deps)
        {
          if (!d.Optional && !installedIds.Contains(d.Id))
          {
            skipIds.Add(i.ContentId);
            break;
          }
        }
      }

      var effective = installed.Where(i => !skipIds.Contains(i.ContentId)).ToList();
      if (effective.Count == 0)
      {
        _cachedRelativePaths = new List<string>();
        return _cachedRelativePaths;
      }

      // Partition into DLCs and others
      var dlcs = effective.Where(i => i.IsDlc).ToList();
      var mods = effective.Where(i => !i.IsDlc).ToList();

      // Topologically sort each group independently
      var dlcOrder = TopologicallySort(
        dlcs,
        node => node.ContentId,
        (node) => node.Deps.Where(d => dlcs.Any(x => x.ContentId.Equals(d.Id, StringComparison.OrdinalIgnoreCase))).Select(d => d.Id)
      );
      var modOrder = TopologicallySort(
        mods,
        node => node.ContentId,
        (node) => node.Deps.Where(d => mods.Any(x => x.ContentId.Equals(d.Id, StringComparison.OrdinalIgnoreCase))).Select(d => d.Id)
      );

      // Map to relative paths preserving order: DLCs first, then others
      var relByContentId = effective
        .GroupBy(i => i.ContentId, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().RelPath, StringComparer.OrdinalIgnoreCase);
      var result = new List<string>(dlcOrder.Count + modOrder.Count);
      result.AddRange(dlcOrder.Where(relByContentId.ContainsKey).Select(id => relByContentId[id]));
      result.AddRange(modOrder.Where(relByContentId.ContainsKey).Select(id => relByContentId[id]));

      _cachedRelativePaths = result;
      return result;
    }

    private readonly record struct Dep(string Id, bool Optional);

    private sealed class Ext
    {
      public string ContentId { get; }
      public string FolderName { get; }
      public string AbsPath { get; }
      public string RelPath { get; }
      public string ContentXml { get; }
      public bool IsDlc { get; }
      public List<Dep> Deps { get; } = new List<Dep>();

      public Ext(string contentId, string folderName, string absPath, string relPath, string contentXml, bool isDlc)
      {
        ContentId = contentId;
        FolderName = folderName;
        AbsPath = absPath;
        RelPath = relPath;
        ContentXml = contentXml;
        IsDlc = isDlc;
      }
    }

    // Generic topological sort: nodes keyed by getId, dependencies provided by getDeps(node) as list of prerequisite ids within the set
    private static List<string> TopologicallySort<T>(IEnumerable<T> nodes, Func<T, string> getId, Func<T, IEnumerable<string>> getDeps)
    {
      var idComparer = StringComparer.OrdinalIgnoreCase;
      var nodeList = nodes.ToList();
      var idSet = new HashSet<string>(nodeList.Select(getId), idComparer);
      var indegree = new Dictionary<string, int>(idComparer);
      var dependents = new Dictionary<string, List<string>>(idComparer);

      foreach (var n in nodeList)
      {
        var id = getId(n);
        indegree[id] = 0;
      }

      foreach (var n in nodeList)
      {
        var toId = getId(n);
        foreach (var depId in getDeps(n))
        {
          if (!idSet.Contains(depId))
            continue;
          if (!dependents.TryGetValue(depId, out var list))
          {
            list = new List<string>();
            dependents[depId] = list;
          }
          list.Add(toId);
          indegree[toId] = indegree.TryGetValue(toId, out var d) ? d + 1 : 1;
        }
      }

      var zero = new SortedSet<string>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key), idComparer);
      var order = new List<string>(nodeList.Count);
      while (zero.Count > 0)
      {
        var next = zero.Min!;
        zero.Remove(next);
        order.Add(next);
        if (dependents.TryGetValue(next, out var outs))
        {
          foreach (var v in outs.OrderBy(s => s, idComparer))
          {
            indegree[v] = indegree[v] - 1;
            if (indegree[v] == 0)
              zero.Add(v);
          }
        }
      }

      if (order.Count < nodeList.Count)
      {
        foreach (var leftover in nodeList.Select(getId).Except(order, idComparer).OrderBy(s => s, idComparer))
        {
          order.Add(leftover);
        }
      }

      return order;
    }
  }
}
