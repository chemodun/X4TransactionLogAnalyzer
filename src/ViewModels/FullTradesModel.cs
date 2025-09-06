using System.Collections.ObjectModel;
using System.ComponentModel;
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
  public ObservableCollection<FullTrade> FullTrades { get; } = new();

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
      foreach (var ft in MainWindow.GameData.GetFullTrades())
      {
        FullTrades.Add(ft);
      }
      OnPropertyChanged(nameof(FullTrades));
    }
    catch
    {
      // swallow for draft; UI can remain empty on errors
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
