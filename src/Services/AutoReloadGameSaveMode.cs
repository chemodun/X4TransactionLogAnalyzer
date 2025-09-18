namespace X4PlayerShipTradeAnalyzer.Services;

public enum AutoReloadGameSaveMode
{
  None = 0, // Do not auto reload
  SelectedFile = 1, // Auto reload the configured GameSavePath only
  AnyFile = 2, // Auto reload any save file change in the folder (future behavior)
}
