using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Runtime.CompilerServices;
using X4PlayerShipTradeAnalyzer.Models;
using X4PlayerShipTradeAnalyzer.Services;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public class ShipsDataTransactionsModel : ShipsDataBaseModel
{
  public ObservableCollection<ShipTransaction> FilteredTransactions { get; } = new();

  private List<ShipTransaction> allTransactions = new();

  // Transport type filters (shared across ship-based views)
  private bool _isContainerChecked = true;
  public bool IsContainerChecked
  {
    get => _isContainerChecked;
    set
    {
      if (_isContainerChecked == value)
        return;
      if (!value && !_isSolidChecked)
      {
        IsSolidChecked = true; // enforce at least one
      }
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
      if (_isSolidChecked == value)
        return;
      if (!value && !_isContainerChecked)
      {
        IsContainerChecked = true; // enforce at least one
      }
      _isSolidChecked = value;
      OnPropertyChanged();
      LoadData();
    }
  }

  public ShipsDataTransactionsModel() => LoadData();

  protected override void LoadData()
  {
    SelectedShip = null;
    ShipList.Clear();
    allTransactions.Clear();
    // Build ships map while reading transactions
    var ships = new Dictionary<int, ShipInfo>();

    // Load transactions
    foreach (
      var trans in MainViewModel.AllTransactions.Where(t =>
        (IsContainerChecked && t.Transport == "container") || (IsSolidChecked && t.Transport == "solid")
      )
    )
    {
      // aggregate ships
      if (!ships.TryGetValue(trans.ShipId, out var info))
      {
        info = new ShipInfo
        {
          ShipId = trans.ShipId,
          ShipName = trans.FullName,
          EstimatedProfit = 0m,
        };
        ships.Add(trans.ShipId, info);
      }
      info.EstimatedProfit = (info.EstimatedProfit ?? 0m) + trans.EstimatedProfit;

      allTransactions.Add(
        new ShipTransaction
        {
          ShipId = trans.ShipId,
          RawTime = trans.RawTime,
          Time = trans.Time,
          Sector = trans.Sector,
          Station = trans.Station,
          Operation = trans.Operation,
          Product = trans.Product,
          Price = trans.Price,
          Quantity = trans.Quantity,
          Total = trans.Total,
          EstimatedProfit = trans.EstimatedProfit,
        }
      );
    }
    // materialize ShipList sorted by name
    foreach (var s in ships.Values)
      ShipList.Add(s);
    ResortShips();
    ApplyShipFilter();
  }

  protected override void ApplyShipFilter()
  {
    FilteredTransactions.Clear();
    if (SelectedShip == null)
    {
      // reset summaries
      TimeInService = "-";
      ItemsTraded = "0";
      TotalProfit = "0";
      TimeMin = "-";
      TimeAvg = "-";
      TimeMax = "-";
      return;
    }

    long itemsTotal = 0;
    decimal profitTotal = 0m;
    long firstMs = long.MaxValue;
    long lastMs = 0;

    // trip stats (buy -> subsequent sell of same ware)
    long tripMin = long.MaxValue;
    long tripMax = 0;
    long tripSum = 0;
    int tripCount = 0;
    string prevOp = string.Empty;
    string prevWare = string.Empty;
    long prevMs = 0;

    int count = 0;
    foreach (var tx in allTransactions.Where(t => t.ShipId == SelectedShip.ShipId))
    {
      FilteredTransactions.Add(tx);
      count++;

      // accumulate items and profit
      if (tx.Operation == "sell")
      {
        itemsTotal += Convert.ToInt64(tx.Quantity.GetValueOrDefault());
        if (prevOp == "buy" && prevWare == tx.Product)
        {
          var dt = Math.Abs(tx.RawTime - prevMs);
          tripSum += dt;
          tripCount++;
          if (dt < tripMin)
            tripMin = dt;
          if (dt > tripMax)
            tripMax = dt;
        }
      }
      profitTotal += tx.EstimatedProfit ?? 0m;

      // update window
      if (tx.RawTime < firstMs)
        firstMs = tx.RawTime;
      if (tx.RawTime > lastMs)
        lastMs = tx.RawTime;

      // carry last op/ware/time
      prevOp = tx.Operation ?? string.Empty;
      prevWare = tx.Product ?? string.Empty;
      prevMs = tx.RawTime;
    }

    if (count > 0)
    {
      TimeInService = TimeFormatter.FormatHms(lastMs - firstMs, groupHours: true);
      ItemsTraded = itemsTotal.ToString("N0");
      TotalProfit = profitTotal.ToString("N2");
      if (tripCount > 0)
      {
        TimeMin = TimeFormatter.FormatHms(tripMin);
        TimeAvg = TimeFormatter.FormatHms(tripSum / tripCount);
        TimeMax = TimeFormatter.FormatHms(tripMax);
      }
      else
      {
        TimeMin = "-";
        TimeAvg = "-";
        TimeMax = "-";
      }
    }
    else
    {
      // empty selection after filter
      TimeInService = "-";
      ItemsTraded = "0";
      TotalProfit = "0";
      TimeMin = "-";
      TimeAvg = "-";
      TimeMax = "-";
    }
  }
}
