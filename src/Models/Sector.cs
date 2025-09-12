using System.ComponentModel;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.Models;

public sealed class SectorToSectorConnection
{
  public string SectorFrom { get; set; } = string.Empty;
  public string SectorTo { get; set; } = string.Empty;

  public static List<SectorToSectorConnection> GetSectorByGateConnections()
  {
    var list = new List<SectorToSectorConnection>();

    var conn = MainWindow.GameData.Connection;
    if (conn == null)
      return list;

    using var cmd = conn.CreateCommand();
    cmd.CommandText =
      @"
SELECT
    go.sector AS sector_one,
    go.code   AS gate_one,
    gt.sector AS sector_two,
    gt.code   AS gate_two
FROM gate AS go
JOIN gate AS gt
  ON go.connected = gt.connection
WHERE go.connection != go.connected;
";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      list.Add(
        new SectorToSectorConnection
        {
          SectorFrom = reader["sector_one"].ToString() ?? string.Empty,
          SectorTo = reader["sector_two"].ToString() ?? string.Empty,
        }
      );
    }
    return list;
  }

  public static void EnrichSectorConnectionsWithHighways(ref List<SectorToSectorConnection> list)
  {
    var conn = MainWindow.GameData.Connection;
    if (conn == null)
      return;

    using var cmd = conn.CreateCommand();
    cmd.CommandText =
      @"
SELECT
  sector_from,
  sector_to
FROM superhighway
";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      list.Add(
        new SectorToSectorConnection
        {
          SectorFrom = reader["sector_from"].ToString() ?? string.Empty,
          SectorTo = reader["sector_to"].ToString() ?? string.Empty,
        }
      );
    }
  }

  // Caches
  private static Dictionary<string, List<string>>? _graph;
  private static readonly Dictionary<(string from, string to), int> _distanceCache = new();
  private static readonly object _lock = new();

  // Build or rebuild the adjacency once (directed)
  private static void EnsureGraph()
  {
    if (_graph != null)
      return;

    var edges = GetSectorByGateConnections();
    EnrichSectorConnectionsWithHighways(ref edges);

    var g = new Dictionary<string, List<string>>(StringComparer.Ordinal);

    foreach (var e in edges)
    {
      var from = e.SectorFrom.Trim();
      var to = e.SectorTo.Trim();
      if (from.Length == 0 || to.Length == 0)
        continue;

      if (!g.ContainsKey(from))
        g[from] = new List<string>();
      if (!g.ContainsKey(to))
        g[to] = new List<string>();

      g[from].Add(to);
    }

    // Deduplicate adjacency
    foreach (var key in g.Keys.ToList())
    {
      var distinct = g[key].Distinct(StringComparer.Ordinal).ToList();
      g[key].Clear();
      g[key].AddRange(distinct);
    }
    _graph = g;
    _distanceCache.Clear();
  }

  // Call when DB changes or enrichment rules change
  public static void ClearCaches()
  {
    lock (_lock)
    {
      _graph = null;
      _distanceCache.Clear();
    }
  }

  public static int GetSectorDistance(string from, string to)
  {
    from = from.Trim();
    to = to.Trim();
    if (from == to)
      return 0;

    if (_distanceCache.TryGetValue((from, to), out var cached))
      return cached;

    EnsureGraph();
    if (!_graph!.ContainsKey(from) || !_graph.ContainsKey(to))
      return _distanceCache[(from, to)] = -1;

    var visited = new HashSet<string>(StringComparer.Ordinal) { from };
    var queue = new Queue<(string node, int hops)>();
    queue.Enqueue((from, 0));

    while (queue.Count > 0)
    {
      var (cur, hops) = queue.Dequeue();
      foreach (var neighbor in _graph[cur])
      {
        if (neighbor == to)
          return _distanceCache[(from, to)] = hops + 1;

        if (visited.Add(neighbor))
          queue.Enqueue((neighbor, hops + 1));
      }
    }

    return _distanceCache[(from, to)] = -1;
  }
}

public sealed class SuperHighway
{
  public long Id { get; set; }
  public string Macro { get; set; } = string.Empty;
  public string SectorFrom { get; set; } = string.Empty;
  public long EntryGate { get; set; }
  public long ExitGate { get; set; }
  public string SectorTo { get; set; } = string.Empty;
  public bool IsPrepared => EntryGate > 0 && ExitGate > 0;
  public bool IsReady => !string.IsNullOrWhiteSpace(SectorFrom) && !string.IsNullOrWhiteSpace(SectorTo) && EntryGate > 0 && ExitGate > 0;
}

public sealed class HighWayGate
{
  public long Id { get; set; }
  public string Sector { get; set; } = string.Empty;
  public string Type { get; set; } = string.Empty; // "entry" or "exit"
}
