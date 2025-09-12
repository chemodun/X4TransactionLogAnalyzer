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
      var item = new SectorToSectorConnection
      {
        SectorFrom = reader["sector_one"].ToString() ?? string.Empty,
        SectorTo = reader["sector_two"].ToString() ?? string.Empty,
      };
      list.Add(item);
    }
    return list;
  }

  private static Dictionary<string, List<string>>? _graph;
  private static readonly Dictionary<(string from, string to), int> _distanceCache = new();

  public static int GetSectorDistance(string from, string to)
  {
    if (from == to)
      return 0;

    // Check cached result
    var key = (from, to);
    if (_distanceCache.TryGetValue(key, out var cached))
      return cached;

    // Build graph if not cached
    if (_graph == null)
    {
      _graph = new Dictionary<string, List<string>>();
      foreach (var conn in GetSectorByGateConnections())
      {
        if (!_graph.ContainsKey(conn.SectorFrom))
          _graph[conn.SectorFrom] = new();
        _graph[conn.SectorFrom].Add(conn.SectorTo);
      }
    }

    // BFS with depth limit
    var visited = new HashSet<string> { from };
    var queue = new Queue<(string sector, int depth)>();
    queue.Enqueue((from, 0));

    while (queue.Count > 0)
    {
      var (current, depth) = queue.Dequeue();
      if (depth >= 2)
        continue;

      if (!_graph.TryGetValue(current, out var neighbors))
        continue;

      foreach (var neighbor in neighbors)
      {
        if (neighbor == to)
        {
          _distanceCache[key] = depth + 1;
          return depth + 1;
        }

        if (visited.Add(neighbor))
          queue.Enqueue((neighbor, depth + 1));
      }
    }

    _distanceCache[key] = -1;
    return -1;
  }
}
