# X4 Player Ship Trade Analyzer

A simple, fast desktop tool for X4: Foundations players to understand their ship's trading performance. Point it at your game folder and a save file and get clear insights into:

- Which wares make you the most profit
- How each of your ships has been trading over time
- Totals for price, quantity, and estimated profit per transaction

The app runs locally and reads only your X4 files. Nothing is uploaded.

## Key features

- Ships transactions: A detailed table of every trade in your save
  - Columns: Time, Station, Operation (Buy/Sell), Product, Price, Quantity, Total, Estimated Profit
- Filters for ware type: Container and Solid
- Ships graphs: Compare ships visually
  - Interactive chart per ship; double-click a ship in the list to toggle it on/off
  - Same Container/Solid filters apply
- Wares stats: See what actually makes money
  - Single-series histogram with per-ware colors and tooltips
  - Custom legend shows color + ware name for quick scanning
- Configuration (first-run setup)
  - Set Game Folder (your X4.exe location)
  - Choose a save file (.xml.gz)
  - Optional theme: System, Light, or Dark
  - Quick stats to confirm data loaded (wares, factions, ships, stations, trades, language)
- Built-in Readme: A “Readme” tab mirrors this guide inside the app

## Download and run

1) Get the app
   - Recommended: download the latest Windows build from the project’s Releases page:
      - [Project Releases](https://github.com/chemodun/X4PlayerShipTradeAnalyzer/releases)

2) Start it
   - Unzip the downloaded archive and run `X4PlayerShipTradeAnalyzer.exe` inside the folder `X4PlayerShipTradeAnalyzer`.

## First-time setup (Configuration tab)

1) Game Folder
   - Click Set next to “Game Folder” and select your X4.exe. Typical paths:
     - Steam: `C:\Program Files (x86)\Steam\steamapps\common\X4 Foundations\X4.exe`
     - GOG/Epic: wherever you installed X4

2) Save file
   - Click Set next to “Game Save Path” and choose a save `.xml.gz`. Typical path:
     - Steam: `%USERPROFILE%\Documents\Egosoft\X4\<player-id>\save\quicksave.xml.gz`
     - GOG/Epic: `%USERPROFILE%\Documents\Egosoft\X4\save\quicksave.xml.gz`

3) Load data
   - Click “Reload Data” for the Game Folder to import base game data (wares, factions, etc.).
   - Click “Reload Data” for the Save Path to import your transactions.
   - When data is present, the other tabs become enabled.

4) Optional settings
   - Load Only Game Language: speeds up loading by using your game language only.
   - Theme: System, Light, or Dark.

## Using the app

- Ships transactions tab
  - Browse trades with totals and estimated profit.
- Toggle “Container”/“Solid” to filter quickly.

- Ships graphs tab
  - Visualize activity and compare ships.
  - Double-click a ship in the list to show/hide it on the chart.

- Wares stats tab
  - Histogram of profit by ware with a color legend.
  - Hover bars to see ware name and profit.

- Readme tab
  - Shows this guide inside the app for quick reference.

## Tips & troubleshooting

- I see zeros / tabs are disabled
  - Make sure you set both Game Folder (X4.exe) and a valid save `.xml.gz`, then press both Reload buttons.

- Save not found
  - Check `%USERPROFILE%\Documents\Egosoft\X4\<player-id>\save\` for `quicksave.xml.gz` or any `*.xml.gz` save.

- Loading takes a while
  - Large saves can take a minute. A small progress window appears during import.

- Nothing uploads anywhere
  - The app reads your local files only and keeps all analysis on your machine.

## Credits

- Author: Chem O`Dun
- Based on idea implemented in [X4MagicTripLogAnalyzer by Magic Trip](https://github.com/magictripgames/X4MagicTripLogAnalyzer)
- Not affiliated with Egosoft. "X4: Foundations" is a trademark of its respective owner.
