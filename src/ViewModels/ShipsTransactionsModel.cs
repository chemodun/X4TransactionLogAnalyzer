using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using X4PlayerShipTradeAnalyzer.Models;
using X4PlayerShipTradeAnalyzer.Services;
using X4PlayerShipTradeAnalyzer.Views;

namespace X4PlayerShipTradeAnalyzer.ViewModels;

public class ShipsTransactionsModel : INotifyPropertyChanged
{
  public ObservableCollection<ShipInfo> ShipList { get; } = new();
  public ObservableCollection<ShipTransaction> FilteredTransactions { get; } = new();

  private ShipInfo? selectedShip;
  public ShipInfo? SelectedShip
  {
    get => selectedShip;
    set
    {
      if (selectedShip != value)
      {
        selectedShip = value;
        OnPropertyChanged(nameof(SelectedShip));
        ApplyShipFilter();
      }
    }
  }

  private bool isContainerChecked = true;
  public bool IsContainerChecked
  {
    get => isContainerChecked;
    set
    {
      // prevent both flags being false at the same time
      if (!value && !isSolidChecked)
      {
        IsSolidChecked = true;
        return;
      }
      if (isContainerChecked == value)
      {
        return;
      }
      isContainerChecked = value;
      OnPropertyChanged(nameof(IsContainerChecked));
      LoadData();
    }
  }

  private bool isSolidChecked;
  public bool IsSolidChecked
  {
    get => isSolidChecked;
    set
    {
      // prevent both flags being false at the same time
      if (!value && !isContainerChecked)
      {
        IsContainerChecked = true;
        return;
      }
      if (isSolidChecked == value)
      {
        return;
      }
      isSolidChecked = value;
      OnPropertyChanged(nameof(IsSolidChecked));
      LoadData();
    }
  }

  private List<ShipTransaction> allTransactions = new();

  // summary fields
  private string timeInService = "-";
  public string TimeInService
  {
    get => timeInService;
    private set
    {
      if (timeInService != value)
      {
        timeInService = value;
        OnPropertyChanged(nameof(TimeInService));
      }
    }
  }

  private string itemsTraded = "0";
  public string ItemsTraded
  {
    get => itemsTraded;
    private set
    {
      if (itemsTraded != value)
      {
        itemsTraded = value;
        OnPropertyChanged(nameof(ItemsTraded));
      }
    }
  }

  private string totalEstimatedProfit = "0";
  public string TotalEstimatedProfit
  {
    get => totalEstimatedProfit;
    private set
    {
      if (totalEstimatedProfit != value)
      {
        totalEstimatedProfit = value;
        OnPropertyChanged(nameof(TotalEstimatedProfit));
      }
    }
  }

  private string tripTimeMin = "-";
  public string TripTimeMin
  {
    get => tripTimeMin;
    private set
    {
      if (tripTimeMin != value)
      {
        tripTimeMin = value;
        OnPropertyChanged(nameof(TripTimeMin));
      }
    }
  }

  private string tripTimeAvg = "-";
  public string TripTimeAvg
  {
    get => tripTimeAvg;
    private set
    {
      if (tripTimeAvg != value)
      {
        tripTimeAvg = value;
        OnPropertyChanged(nameof(TripTimeAvg));
      }
    }
  }

  private string tripTimeMax = "-";
  public string TripTimeMax
  {
    get => tripTimeMax;
    private set
    {
      if (tripTimeMax != value)
      {
        tripTimeMax = value;
        OnPropertyChanged(nameof(TripTimeMax));
      }
    }
  }

  public ShipsTransactionsModel()
  {
    LoadData();
  }

  private string AppendWhereOnFilters(string baseQuery)
  {
    var filters = new List<string>();
    if (IsContainerChecked)
      filters.Add("transport == 'container'");
    if (IsSolidChecked)
      filters.Add("transport == 'solid'");
    if (filters.Count > 0)
      return $"{baseQuery} WHERE {string.Join(" OR ", filters)}";
    return baseQuery;
  }

  public void Refresh() => LoadData();

  private void LoadData()
  {
    using var conn = MainWindow.GameData.Connection;
    SelectedShip = null;
    ShipList.Clear();
    allTransactions.Clear();
    // Build ships map while reading transactions
    var ships = new Dictionary<int, ShipInfo>();

    // Load transactions
    var txCmd = new SQLiteCommand(
      AppendWhereOnFilters(
        "SELECT id, full_name, time, sector, station, operation, ware_name, price, volume, trade_sum, profit FROM player_ships_transactions_log"
      ),
      conn
    );
    var txReader = txCmd.ExecuteReader();
    while (txReader.Read())
    {
      var id = Convert.ToInt32(txReader["id"]);
      var fullName = txReader["full_name"].ToString() ?? string.Empty;
      var profit = Convert.ToDecimal(txReader["profit"]);

      // aggregate ships
      if (!ships.TryGetValue(id, out var info))
      {
        info = new ShipInfo
        {
          ShipId = id,
          ShipName = fullName,
          EstimatedProfit = 0m,
        };
        ships.Add(id, info);
      }
      info.EstimatedProfit = (info.EstimatedProfit ?? 0m) + profit;

      var ms = Convert.ToInt64(txReader["time"]);
      allTransactions.Add(
        new ShipTransaction
        {
          ShipId = id,
          RawTimeMs = Math.Abs(ms),
          Time = TimeFormatter.FormatHms(Convert.ToInt64(txReader["time"])),
          Sector = txReader["sector"].ToString() ?? string.Empty,
          Station = txReader["station"].ToString() ?? string.Empty,
          Operation = txReader["operation"].ToString() ?? string.Empty,
          Product = txReader["ware_name"].ToString() ?? string.Empty,
          Price = Convert.ToDecimal(txReader["price"]),
          VolumeValue = Convert.ToInt32(txReader["volume"]),
          Quantity = Convert.ToInt32(txReader["volume"]),
          Total = Convert.ToDecimal(txReader["trade_sum"]),
          EstimatedProfit = profit,
        }
      );
    }
    // materialize ShipList sorted by name
    foreach (var s in ships.Values.OrderBy(s => s.ShipName, System.StringComparer.Ordinal))
      ShipList.Add(s);
    ApplyShipFilter();
  }

  private void ApplyShipFilter()
  {
    FilteredTransactions.Clear();
    if (SelectedShip == null)
    {
      // reset summaries
      TimeInService = "-";
      ItemsTraded = "0";
      TotalEstimatedProfit = "0";
      TripTimeMin = "-";
      TripTimeAvg = "-";
      TripTimeMax = "-";
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
        itemsTotal += tx.VolumeValue;
        if (prevOp == "buy" && prevWare == tx.Product)
        {
          var dt = Math.Abs(tx.RawTimeMs - prevMs);
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
      if (tx.RawTimeMs < firstMs)
        firstMs = tx.RawTimeMs;
      if (tx.RawTimeMs > lastMs)
        lastMs = tx.RawTimeMs;

      // carry last op/ware/time
      prevOp = tx.Operation ?? string.Empty;
      prevWare = tx.Product ?? string.Empty;
      prevMs = tx.RawTimeMs;
    }

    if (count > 0)
    {
      TimeInService = TimeFormatter.FormatHms(lastMs - firstMs, groupHours: true);
      ItemsTraded = itemsTotal.ToString("N0");
      TotalEstimatedProfit = profitTotal.ToString("N2");
      if (tripCount > 0)
      {
        TripTimeMin = TimeFormatter.FormatHms(tripMin);
        TripTimeAvg = TimeFormatter.FormatHms(tripSum / tripCount);
        TripTimeMax = TimeFormatter.FormatHms(tripMax);
      }
      else
      {
        TripTimeMin = "-";
        TripTimeAvg = "-";
        TripTimeMax = "-";
      }
    }
    else
    {
      // empty selection after filter
      TimeInService = "-";
      ItemsTraded = "0";
      TotalEstimatedProfit = "0";
      TripTimeMin = "-";
      TripTimeAvg = "-";
      TripTimeMax = "-";
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
