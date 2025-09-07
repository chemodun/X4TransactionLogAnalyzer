using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using X4PlayerShipTradeAnalyzer.Models;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

/// <summary>
/// Base view model for ship-centric views. Provides:
/// - Common ShipList with sorting (by name or profit)
/// - SelectedShip handling that triggers filtering in derived classes
/// - Transport filters (Container/Solid) and SQL WHERE helper
/// - INotifyPropertyChanged plumbing
/// </summary>
public abstract class ShipsDataBaseModel : INotifyPropertyChanged
{
  // Shared ship list and sorting
  public ObservableCollection<ShipInfo> ShipList { get; } = new();

  private ShipSortOrder _shipsSortOrder = ShipSortOrder.Name;
  public ShipSortOrder ShipsSortOrder
  {
    get => _shipsSortOrder;
    set
    {
      if (_shipsSortOrder == value)
        return;
      _shipsSortOrder = value;
      OnPropertyChanged();
      ResortShips();
    }
  }

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

  // Transport type filters (shared across ship-based views)
  private bool _isContainerChecked = true;
  public bool IsContainerChecked
  {
    get => _isContainerChecked;
    set
    {
      // prevent both flags being false at the same time
      if (!value && !_isSolidChecked)
      {
        IsSolidChecked = true;
        return;
      }
      if (_isContainerChecked == value)
        return;
      _isContainerChecked = value;
      OnPropertyChanged();
      LoadData();
    }
  }

  private bool _isSolidChecked;
  public bool IsSolidChecked
  {
    get => _isSolidChecked;
    set
    {
      // prevent both flags being false at the same time
      if (!value && !_isContainerChecked)
      {
        IsContainerChecked = true;
        return;
      }
      if (_isSolidChecked == value)
        return;
      _isSolidChecked = value;
      OnPropertyChanged();
      LoadData();
    }
  }

  protected string AppendWhereOnFilters(string baseQuery)
  {
    var filters = new System.Collections.Generic.List<string>();
    if (IsContainerChecked)
      filters.Add("transport == 'container'");
    if (IsSolidChecked)
      filters.Add("transport == 'solid'");
    if (filters.Count > 0)
      return $"{baseQuery} WHERE {string.Join(" OR ", filters)}";
    return baseQuery;
  }

  protected void ResortShips()
  {
    if (ShipList.Count == 0)
      return;
    var snapshot = ShipList.ToList();
    ShipList.Clear();
    foreach (var ship in SortShips(snapshot))
      ShipList.Add(ship);
  }

  protected virtual System.Collections.Generic.IEnumerable<ShipInfo> SortShips(System.Collections.Generic.IEnumerable<ShipInfo> ships)
  {
    return ShipsSortOrder switch
    {
      ShipSortOrder.Profit => ships.OrderByDescending(s => s.EstimatedProfit ?? 0m).ThenBy(s => s.ShipName, StringComparer.Ordinal),
      _ => ships.OrderBy(s => s.ShipName, StringComparer.Ordinal),
    };
  }

  public void Refresh() => LoadData();

  // Implementations must load data and apply per-ship filtering/summaries
  protected abstract void LoadData();
  protected abstract void ApplyShipFilter();

  public event PropertyChangedEventHandler? PropertyChanged;

  protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
