using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
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
        ApplyFilter();
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
    // Load ships
    var shipCmd = new SQLiteCommand(
      AppendWhereOnFilters("SELECT DISTINCT id, full_name FROM player_ships_transactions_log") + " ORDER BY full_name",
      conn
    );
    var shipReader = shipCmd.ExecuteReader();
    while (shipReader.Read())
    {
      ShipList.Add(new ShipInfo { ShipId = Convert.ToInt32(shipReader["id"]), ShipName = shipReader["full_name"].ToString() });
    }

    // Load transactions
    var txCmd = new SQLiteCommand(
      AppendWhereOnFilters(
        "SELECT id, time, sector, station, operation, ware_name, price, volume, trade_sum, profit FROM player_ships_transactions_log"
      ),
      conn
    );
    var txReader = txCmd.ExecuteReader();
    while (txReader.Read())
    {
      var ms = -Convert.ToInt64(txReader["time"]);
      TimeSpan span = TimeSpan.FromMilliseconds(ms);
      allTransactions.Add(
        new ShipTransaction
        {
          ShipId = Convert.ToInt32(txReader["id"]),
          RawTimeMs = ms,
          Time = $"-{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}",
          Sector = txReader["sector"].ToString() ?? string.Empty,
          Station = txReader["station"].ToString() ?? string.Empty,
          Operation = txReader["operation"].ToString() ?? string.Empty,
          Product = txReader["ware_name"].ToString() ?? string.Empty,
          Price = Convert.ToDecimal(txReader["price"]),
          VolumeValue = Convert.ToInt32(txReader["volume"]),
          Quantity = Convert.ToInt32(txReader["volume"]),
          Total = Convert.ToDecimal(txReader["trade_sum"]),
          EstimatedProfit = Convert.ToDecimal(txReader["profit"]),
        }
      );
    }
    ApplyFilter();
  }

  private void ApplyFilter()
  {
    FilteredTransactions.Clear();
    if (SelectedShip != null)
    {
      var shipTx = allTransactions.Where(t => t.ShipId == (SelectedShip?.ShipId)).ToList();
      long itemsTraded = 0;
      decimal estimatedProfit = 0;
      List<long> tripsTimes = new();
      string lastOperation = string.Empty;
      string lastWare = string.Empty;
      long lastTimeMs = 0;
      foreach (var tx in shipTx)
      {
        FilteredTransactions.Add(tx);
        if (tx.Operation == "sell")
        {
          itemsTraded += tx.VolumeValue;
          if (lastOperation == "buy" && lastWare == tx.Product)
          {
            tripsTimes.Add(Math.Abs(tx.RawTimeMs - lastTimeMs));
          }
        }
        lastOperation = tx.Operation ?? string.Empty;
        lastWare = tx.Product ?? string.Empty;
        lastTimeMs = tx.RawTimeMs;
        estimatedProfit += tx.EstimatedProfit ?? 0;
      }
      // update summaries
      if (shipTx.Count > 0)
      {
        var firstMs = shipTx.Min(t => t.RawTimeMs);
        var lastMs = shipTx.Max(t => t.RawTimeMs);
        var dur = TimeSpan.FromMilliseconds(lastMs - firstMs);
        TimeInService = $"{(int)dur.TotalHours:N0}:{dur.Minutes:D2}:{dur.Seconds:D2}";
        ItemsTraded = itemsTraded.ToString("N0");
        TotalEstimatedProfit = estimatedProfit.ToString("N2");
        TripTimeMin = tripsTimes.Count > 0 ? TimeSpan.FromMilliseconds(tripsTimes.Min()).ToString(@"hh\:mm\:ss") : "-";
        TripTimeAvg = tripsTimes.Count > 0 ? TimeSpan.FromMilliseconds(tripsTimes.Average()).ToString(@"hh\:mm\:ss") : "-";
        TripTimeMax = tripsTimes.Count > 0 ? TimeSpan.FromMilliseconds(tripsTimes.Max()).ToString(@"hh\:mm\:ss") : "-";
      }
    }
    else
    {
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
