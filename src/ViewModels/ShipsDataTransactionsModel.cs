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

  // summary fields
  private string _timeInService = "-";
  public string TimeInService
  {
    get => _timeInService;
    private set
    {
      if (_timeInService != value)
      {
        _timeInService = value;
        OnPropertyChanged();
      }
    }
  }

  private string _itemsTraded = "0";
  public string ItemsTraded
  {
    get => _itemsTraded;
    private set
    {
      if (_itemsTraded != value)
      {
        _itemsTraded = value;
        OnPropertyChanged();
      }
    }
  }

  private string _totalProfit = "0";
  public string TotalProfit
  {
    get => _totalProfit;
    private set
    {
      if (_totalProfit != value)
      {
        _totalProfit = value;
        OnPropertyChanged();
      }
    }
  }

  private string _timeMin = "-";
  public string TimeMin
  {
    get => _timeMin;
    private set
    {
      if (_timeMin != value)
      {
        _timeMin = value;
        OnPropertyChanged();
      }
    }
  }

  private string _timeAvg = "-";
  public string TimeAvg
  {
    get => _timeAvg;
    private set
    {
      if (_timeAvg != value)
      {
        _timeAvg = value;
        OnPropertyChanged();
      }
    }
  }

  private string _timeMax = "-";
  public string TimeMax
  {
    get => _timeMax;
    private set
    {
      if (_timeMax != value)
      {
        _timeMax = value;
        OnPropertyChanged();
      }
    }
  }

  public ShipsDataTransactionsModel() => LoadData();

  protected override void LoadData()
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
