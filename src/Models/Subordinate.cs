using System.ComponentModel;

namespace X4PlayerShipTradeAnalyzer.Models;

public sealed class Subordinate
{
  public long CommanderId { get; set; }
  public long CommanderConnectionId { get; set; }
  public long SubordinateId { get; set; }
  public string Group { get; set; } = string.Empty;

  public bool IsValid => CommanderId != 0 && SubordinateId != 0 && !string.IsNullOrEmpty(Group);
}
