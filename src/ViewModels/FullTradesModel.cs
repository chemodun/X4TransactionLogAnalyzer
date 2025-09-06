using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using X4PlayerShipTradeAnalyzer.Models;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

/// <summary>
/// Draft view model that materializes full trades from the database.
/// Loads on construction and can be refreshed after save-game imports.
/// </summary>
public sealed class FullTradesModel : INotifyPropertyChanged
{
  // All full trades loaded from analytics
  public ObservableCollection<FullTrade> FullTrades { get; } = new();

  // Ships list populated from FullTrades (distinct ship id/name)
  public ObservableCollection<ShipInfo> ShipList { get; } = new();

  // Filtered by SelectedShip
  public ObservableCollection<FullTrade> FilteredFullTrades { get; } = new();

  // Combined purchase/sale steps of SelectedFullTrade
  public ObservableCollection<TradeStep> TradeSteps { get; } = new();

  private ShipInfo? _selectedShip;
  public ShipInfo? SelectedShip
  {
    get => _selectedShip;
    set
    {
      if (_selectedShip == value)
        return;
      _selectedShip = value;
      OnPropertyChanged();
      ApplyShipFilter();
    }
  }

  private FullTrade? _selectedFullTrade;
  public FullTrade? SelectedFullTrade
  {
    get => _selectedFullTrade;
    set
    {
      if (_selectedFullTrade == value)
        return;
      _selectedFullTrade = value;
      OnPropertyChanged();
      RebuildTradeSteps();
    }
  }

  public FullTradesModel()
  {
    LoadData();
  }

  public void Refresh() => LoadData();

  private void LoadData()
  {
    try
    {
      FullTrades.Clear();
      ShipList.Clear();
      FilteredFullTrades.Clear();
      TradeSteps.Clear();
      SelectedShip = null;
      SelectedFullTrade = null;

      foreach (var ft in MainWindow.GameData.GetFullTrades())
      {
        FullTrades.Add(ft);
      }

      // Build ships list from loaded full trades
      foreach (var group in FullTrades.GroupBy(f => new { f.ShipId, f.ShipName }).OrderBy(g => g.Key.ShipName))
      {
        ShipList.Add(new ShipInfo { ShipId = (int)group.Key.ShipId, ShipName = group.Key.ShipName });
      }

      OnPropertyChanged(nameof(FullTrades));
      OnPropertyChanged(nameof(ShipList));
    }
    catch
    {
      // swallow for draft; UI can remain empty on errors
    }
  }

  private void ApplyShipFilter()
  {
    FilteredFullTrades.Clear();
    TradeSteps.Clear();
    SelectedFullTrade = null;
    if (SelectedShip == null)
      return;

    foreach (var ft in FullTrades.Where(f => f.ShipId == SelectedShip.ShipId))
      FilteredFullTrades.Add(ft);
  }

  private void RebuildTradeSteps()
  {
    TradeSteps.Clear();
    if (SelectedFullTrade == null)
      return;

    // Merge purchases and sales into a single ordered list by time
    var steps = new List<TradeStep>();
    foreach (var p in SelectedFullTrade.Purchases ?? Array.Empty<TradeLeg>())
    {
      steps.Add(
        new TradeStep
        {
          Time = p.Time,
          Operation = "buy",
          Counterpart = string.IsNullOrWhiteSpace(p.CounterpartName) ? p.CounterpartCode : p.CounterpartName,
          Sector = p.Sector,
          Price = p.Price,
          Volume = p.Volume,
        }
      );
    }
    foreach (var s in SelectedFullTrade.Sales ?? Array.Empty<TradeLeg>())
    {
      steps.Add(
        new TradeStep
        {
          Time = s.Time,
          Operation = "sell",
          Counterpart = string.IsNullOrWhiteSpace(s.CounterpartName) ? s.CounterpartCode : s.CounterpartName,
          Sector = s.Sector,
          Price = s.Price,
          Volume = s.Volume,
        }
      );
    }
    foreach (var step in steps.OrderBy(t => t.Time))
      TradeSteps.Add(step);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  public sealed class TradeStep
  {
    public int Time { get; init; }
    public string Operation { get; init; } = string.Empty; // buy/sell
    public string Counterpart { get; init; } = string.Empty;
    public string Sector { get; init; } = string.Empty;
    public long Price { get; init; }
    public long Volume { get; init; }
  }
}
