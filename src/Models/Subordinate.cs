using System.ComponentModel;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.Models;

public sealed class SubordinateOnInput
{
  public long CommanderId { get; set; }
  public long CommanderConnectionId { get; set; }
  public long SubordinateId { get; set; }
  public string Group { get; set; } = string.Empty;

  public bool IsValid => CommanderId != 0 && SubordinateId != 0 && !string.IsNullOrEmpty(Group);
}

public sealed class Subordinate
{
  public long CommanderId { get; set; }
  public long SubordinateId { get; set; }
  public string Assignment { get; set; } = string.Empty;

  public static List<Subordinate> AllSubordinates { get; } = new();

  public static void LoadAllSubordinates()
  {
    AllSubordinates.Clear();
    var conn = MainWindow.GameData.Connection;
    if (conn == null)
      return;
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT * FROM subordinate";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      var sub = new Subordinate
      {
        CommanderId = Convert.ToInt64(reader["commander_id"]),
        SubordinateId = Convert.ToInt64(reader["subordinate_id"]),
        Assignment = reader["assignment"].ToString() ?? string.Empty,
      };
      AllSubordinates.Add(sub);
    }
  }

  public static HashSet<long> GetSubordinateIdsForCommander(long commanderId)
  {
    var result = new HashSet<long>();
    foreach (var sub in AllSubordinates)
    {
      if (sub.CommanderId == commanderId)
        result.Add(sub.SubordinateId);
    }
    return result;
  }
}
