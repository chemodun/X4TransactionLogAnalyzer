using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using X4PlayerShipTradeAnalyzer.Models;
using X4Unpack;

namespace X4PlayerShipTradeAnalyzer.Services;

public sealed class GameDataStats : INotifyPropertyChanged
{
  int _waresCount;
  public int WaresCount
  {
    get => _waresCount;
    set
    {
      if (_waresCount != value)
      {
        _waresCount = value;
        OnPropertyChanged(nameof(WaresCount));
      }
    }
  }

  int _factionsCount;
  public int FactionsCount
  {
    get => _factionsCount;
    set
    {
      if (_factionsCount != value)
      {
        _factionsCount = value;
        OnPropertyChanged(nameof(FactionsCount));
      }
    }
  }

  int _playerShipsCount;
  public int PlayerShipsCount
  {
    get => _playerShipsCount;
    set
    {
      if (_playerShipsCount != value)
      {
        _playerShipsCount = value;
        OnPropertyChanged(nameof(PlayerShipsCount));
      }
    }
  }

  int _stationsCount;
  public int StationsCount
  {
    get => _stationsCount;
    set
    {
      if (_stationsCount != value)
      {
        _stationsCount = value;
        OnPropertyChanged(nameof(StationsCount));
      }
    }
  }

  int _tradesCount;
  public int TradesCount
  {
    get => _tradesCount;
    set
    {
      if (_tradesCount != value)
      {
        _tradesCount = value;
        OnPropertyChanged(nameof(TradesCount));
      }
    }
  }

  int _languagesCount;
  public int LanguagesCount
  {
    get => _languagesCount;
    set
    {
      if (_languagesCount != value)
      {
        _languagesCount = value;
        OnPropertyChanged(nameof(LanguagesCount));
      }
    }
  }

  int _currentLanguageTextCount;
  public int CurrentLanguageTextCount
  {
    get => _currentLanguageTextCount;
    set
    {
      if (_currentLanguageTextCount != value)
      {
        _currentLanguageTextCount = value;
        OnPropertyChanged(nameof(CurrentLanguageTextCount));
      }
    }
  }

  int _currentLanguageId;
  public int CurrentLanguageId
  {
    get => _currentLanguageId;
    set
    {
      if (_currentLanguageId != value)
      {
        _currentLanguageId = value;
        OnPropertyChanged(nameof(CurrentLanguageId));
      }
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class GameData
{
  private static readonly List<string> nameIndex = new()
  {
    "",
    " I",
    " II",
    " III",
    " IV",
    " V",
    " VI",
    " VII",
    " VIII",
    " IX",
    " X",
    " XI",
    " XII",
    " XIII",
    " XIV",
    " XV",
    " XVI",
    " XVII",
    " XVIII",
    " XIX",
    " XX",
    " XXI",
    " XXII",
    " XXIII",
    " XXIV",
    " XXV",
    " XXVI",
    " XXVII",
    " XXVIII",
    " XXIX",
    " XXX",
  };
  private const int _batchSize = 1000;
  private readonly string _gameDataDirectory;
  private readonly string _dbPath;
  private SQLiteConnection _conn;

  // Cache for DLCs resolution
  private string? _cachedGameFolderExePathForDlcs;
  private List<string>? _cachedDlcRelativePaths;

  public GameDataStats Stats { get; } = new GameDataStats();

  private int _waresProcessed;
  private int _factionsProcessed;
  private int _processedFiles;

  public SQLiteConnection Connection
  {
    get
    {
      ReOpenConnection();
      return _conn;
    }
  }

  public GameData()
  {
    string baseDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
    _gameDataDirectory = Path.Combine(baseDirectory, "GameData");
    Directory.CreateDirectory(_gameDataDirectory);
    _dbPath = Path.Combine(_gameDataDirectory, "GameData.db");
    var needsCreate = !File.Exists(_dbPath);
    _conn = CreateConnection();
    if (needsCreate)
    {
      CreateSchema();
      SetInitialValues();
    }
    RefreshStats();
  }

  private SQLiteConnection CreateConnection()
  {
    var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
    conn.Open();
    return conn;
  }

  private void ReOpenConnection()
  {
    try
    {
      if (_conn.State == System.Data.ConnectionState.Closed)
      {
        _conn.Open();
      }
    }
    catch (Exception)
    {
      _conn = CreateConnection();
    }
  }

  /// <summary>
  /// Returns relative paths (from the game root) to installed DLC folders under 'extensions',
  /// sorted so that items with no present dependencies come first, followed by their dependents, etc.
  /// The list is cached per GameFolderExePath; if the configured path hasn't changed, the cached list is returned.
  /// </summary>
  public List<string> GetDLCsSorted()
  {
    var configuredExePath = ConfigurationService.Instance.GameFolderExePath;
    if (
      !string.IsNullOrWhiteSpace(_cachedGameFolderExePathForDlcs)
      && string.Equals(_cachedGameFolderExePathForDlcs, configuredExePath, StringComparison.OrdinalIgnoreCase)
      && _cachedDlcRelativePaths != null
    )
    {
      return _cachedDlcRelativePaths;
    }

    _cachedGameFolderExePathForDlcs = configuredExePath;
    _cachedDlcRelativePaths = new List<string>();

    if (string.IsNullOrWhiteSpace(configuredExePath))
    {
      return _cachedDlcRelativePaths;
    }

    // Determine game root: allow either folder path or full exe path
    string? gameRoot = null;
    if (Directory.Exists(configuredExePath))
    {
      gameRoot = configuredExePath;
    }
    else if (File.Exists(configuredExePath))
    {
      gameRoot = Path.GetDirectoryName(configuredExePath);
    }
    if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
    {
      return _cachedDlcRelativePaths;
    }

    var extensionsRoot = Path.Combine(gameRoot, "extensions");
    if (!Directory.Exists(extensionsRoot))
    {
      return _cachedDlcRelativePaths;
    }

    // Discover DLC folders by mask ego_dlc_*
    var dlcDirs = Directory.EnumerateDirectories(extensionsRoot, "ego_dlc_*", SearchOption.TopDirectoryOnly).ToList();

    // Filter by presence of content.xml
    var installed = new List<(string Id, string AbsPath, string RelPath, string ContentXml)>();
    foreach (var dir in dlcDirs)
    {
      var contentXml = Path.Combine(dir, "content.xml");
      if (!File.Exists(contentXml))
      {
        continue; // skip if no content.xml
      }
      var id = Path.GetFileName(dir) ?? string.Empty; // assume folder name equals content id
      if (string.IsNullOrWhiteSpace(id))
        continue;
      var rel = Path.Combine("extensions", id);
      installed.Add((id, dir, rel, contentXml));
    }

    if (installed.Count == 0)
    {
      return _cachedDlcRelativePaths;
    }

    // Build dependency graph limited to installed set
    var installedIds = new HashSet<string>(installed.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
    var indegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // key: prerequisite, value: list of dependents

    foreach (var i in installed)
    {
      indegree[i.Id] = 0; // initialize
    }

    foreach (var i in installed)
    {
      // Parse content.xml dependencies of this DLC
      try
      {
        var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
        using var xr = XmlReader.Create(i.ContentXml, settings);
        while (xr.Read())
        {
          if (xr.NodeType == XmlNodeType.Element && xr.Name.Equals("dependency", StringComparison.OrdinalIgnoreCase))
          {
            var depId = xr.GetAttribute("id") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(depId))
              continue;
            // Only consider deps that are installed; optional or missing are ignored for ordering
            if (!installedIds.Contains(depId))
              continue;
            // record edge depId -> i.Id
            if (!dependents.TryGetValue(depId, out var list))
            {
              list = new List<string>();
              dependents[depId] = list;
            }
            list.Add(i.Id);
            indegree[i.Id] = indegree.TryGetValue(i.Id, out var d) ? d + 1 : 1;
          }
        }
      }
      catch
      {
        // If parsing fails, treat as no dependencies
      }
    }

    // Kahn's algorithm with lexicographic tie-breaker
    var zero = new SortedSet<string>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key), StringComparer.OrdinalIgnoreCase);
    var order = new List<string>(installed.Count);
    while (zero.Count > 0)
    {
      var next = zero.Min!;
      zero.Remove(next);
      order.Add(next);
      if (dependents.TryGetValue(next, out var outs))
      {
        // stable iteration
        foreach (var v in outs.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
          indegree[v] = indegree[v] - 1;
          if (indegree[v] == 0)
          {
            zero.Add(v);
          }
        }
      }
    }

    // If cycle exists or some nodes not processed, append them in name order to keep deterministic
    if (order.Count < installed.Count)
    {
      foreach (
        var leftover in installed
          .Select(i => i.Id)
          .Except(order, StringComparer.OrdinalIgnoreCase)
          .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
      )
      {
        order.Add(leftover);
      }
    }

    // Map to relative paths in that order
    var relById = installed.ToDictionary(i => i.Id, i => i.RelPath, StringComparer.OrdinalIgnoreCase);
    var result = order.Where(id => relById.ContainsKey(id)).Select(id => relById[id]).ToList();

    _cachedDlcRelativePaths = result;
    return result;
  }

  private void CreateSchema()
  {
    using var cmd = _conn.CreateCommand();
    cmd.CommandText =
      @"
CREATE TABLE settings (
    current_language INTEGER NOT NULL
);
CREATE TABLE component (
    id      INTEGER PRIMARY KEY,
    type    TEXT NOT NULL,
    class   TEXT NOT NULL,
    owner   TEXT NOT NULL,
    name    TEXT NOT NULL,
    nameindex TEXT NOT NULL,
    code    TEXT NOT NULL
);
CREATE TABLE trade (
    id         INTEGER PRIMARY KEY,
    seller     INTEGER NOT NULL,
    buyer      INTEGER NOT NULL,
    ware       TEXT NOT NULL,
    price      INTEGER NOT NULL,
    volume     INTEGER NOT NULL,
    time       INTEGER NOT NULL,
    trade_sum   INTEGER GENERATED ALWAYS AS (price * volume) STORED
);
CREATE TABLE ware (
    id         TEXT PRIMARY KEY,
    name       TEXT NOT NULL,
    group_of   TEXT NOT NULL,
    transport  TEXT NOT NULL,
    volume     INTEGER NOT NULL,
    price_min  INTEGER NOT NULL,
    price_avg  INTEGER NOT NULL,
    price_max  INTEGER NOT NULL,
    text       TEXT NOT NULL
);
CREATE TABLE text (
    id_uniq    INTEGER PRIMARY KEY,
    language   INTEGER NOT NULL,
    page       INTEGER NOT NULL,
    id         INTEGER NOT NULL,
    text       TEXT NOT NULL
);
CREATE TABLE faction (
    id         TEXT PRIMARY KEY,
    name       TEXT NOT NULL,
    shortname  TEXT NOT NULL,
    prefixname TEXT NOT NULL
);
CREATE INDEX idx_component_type           ON component(type);
CREATE INDEX idx_component_type_owner     ON component(type, owner);
CREATE INDEX idx_component_type_owner_id  ON component (type, owner, id);
CREATE INDEX idx_trade_seller_time        ON trade(seller, time);
CREATE INDEX idx_trade_buyer_time         ON trade(buyer, time);
CREATE INDEX idx_trade_ware               ON trade(ware);
CREATE INDEX idx_trade_seller_time_ware   ON trade(seller, time, ware);
CREATE INDEX idx_trade_buyer_time_ware    ON trade(buyer, time, ware);
CREATE INDEX idx_text_language_page       ON text(language, page);
CREATE INDEX idx_text_language_page_id    ON text(language, page, id);
CREATE INDEX idx_text_id                  ON text(id);
CREATE INDEX idx_text_language_id         ON text(language, id);
-- View lang
CREATE VIEW lang AS
SELECT t.page, t.id, t.text
FROM text AS t
JOIN settings AS s
  ON t.language = s.current_language;
-- View stations
CREATE VIEW stations AS
SELECT *
FROM component
WHERE type = 'station';
CREATE VIEW player_ships AS
SELECT *
FROM component
WHERE type = 'ship' AND owner = 'player';
-- View player_ships_with_trades
CREATE VIEW player_ships_with_trades AS
SELECT
    c.id,
    c.type,
    c.class,
    c.owner,
    c.name,
    c.code,
    SUM(
        CASE
            WHEN c.id = t.seller THEN  t.trade_sum   -- sold → positive
            WHEN c.id = t.buyer  THEN -t.trade_sum   -- bought → negative
        END
    ) AS total_trade_sum
FROM component AS c
JOIN trade AS t
  ON t.seller = c.id
   OR t.buyer = c.id
WHERE c.type = 'ship'
  AND c.owner = 'player'
GROUP BY
    c.id, c.type, c.class, c.owner, c.name, c.code;
-- View player_ships_transactions_log
CREATE VIEW player_ships_transactions_log AS
SELECT *
FROM (
  SELECT
      ship.id AS id,
      ship.code AS code,
      ship.name AS name,
      ship.class AS class,
      ship.name || ' (' || ship.code || ')' AS full_name,
      cp.code   AS counterpart_code,
      cp.name   AS counterpart_name,
      cp.owner  AS counterpart_faction,
      CASE
        WHEN cp.owner = 'player'
          THEN cp.name || ' (' || cp.code || ')'
        ELSE CASE
              WHEN f.shortname IS NULL THEN ''
              ELSE f.shortname
            END
            || ' ' || cp.name || cp.nameindex || ' (' || cp.code || ')'
      END AS station,
      t.time AS time,
      'sell' AS operation,
      t.ware AS ware,
      w.text AS ware_name,
      t.price / 100.0 AS price,
      t.volume AS volume,
      t.trade_sum / 100.0 AS trade_sum,
      w.transport AS transport,
      (t.trade_sum - w.price_avg * t.volume) / 100.0 AS profit,
      t.volume * w.volume AS capacity
  FROM trade AS t
  JOIN component AS ship
    ON ship.id = t.seller
  JOIN component AS cp
    ON cp.id = t.buyer
  LEFT JOIN faction AS f
    ON f.id = cp.owner
  JOIN ware AS w
    ON w.id = t.ware
  WHERE ship.type = 'ship'
    AND ship.owner = 'player'
UNION ALL
-- Case 2: player ship is the buyer
  SELECT
      ship.id AS id,
      ship.code AS code,
      ship.name AS name,
      ship.class AS class,
      ship.name || ' (' || ship.code || ')' AS full_name,
      cp.code   AS counterpart_code,
      cp.name   AS counterpart_name,
      cp.owner  AS counterpart_faction,
      CASE
        WHEN cp.owner = 'player'
          THEN cp.name || ' (' || cp.code || ')'
        ELSE CASE
              WHEN f.shortname IS NULL THEN ''
              ELSE f.shortname
            END
            || ' ' || cp.name || cp.nameindex || ' (' || cp.code || ')'
      END AS station,
      t.time AS time,
      'buy' AS operation,
      t.ware AS ware,
      w.text AS ware_name,
      t.price / 100.0 AS price,
      t.volume AS volume,
      -t.trade_sum/ 100.0 AS trade_sum,
      w.transport AS transport,
      (w.price_avg * t.volume - t.trade_sum) / 100.0 AS profit,
      t.volume * w.volume AS capacity
  FROM trade AS t
  JOIN component AS ship
    ON ship.id = t.buyer
  JOIN component AS cp
    ON cp.id = t.seller
  LEFT JOIN faction AS f
    ON f.id = cp.owner
  JOIN ware AS w
    ON w.id = t.ware
  WHERE ship.type = 'ship'
    AND ship.owner = 'player'
) AS combined
ORDER BY full_name, time
";
    cmd.ExecuteNonQuery();
  }

  private void SetInitialValues()
  {
    ReOpenConnection();
    using var cmd = _conn.CreateCommand();
    cmd.CommandText = "INSERT INTO settings (current_language) VALUES (44);";
    cmd.ExecuteNonQuery();
  }

  /// <summary>
  /// Load localized text resources from the application's GameData/t directory into the 'text' table.
  /// Refinement rules:
  /// - Resolve references of the form {page,id} within the current language (recursively with a small depth limit).
  /// - Exclude any segments enclosed by unescaped parentheses '(' and ')'. Escaped parentheses (\( or \)) are kept, without the backslash.
  /// </summary>

  public IEnumerable<TradeOperation> GetPlayerTradeOperations()
  {
    using var cmd = _conn.CreateCommand();
    cmd.CommandText =
      @"SELECT code,name,class,counterpart_code,counterpart_name,counterpart_faction,time,ware,price,volume,trade_sum FROM player_ships_transactions_log ORDER BY time";
    using var rdr = cmd.ExecuteReader();
    while (rdr.Read())
    {
      yield return new TradeOperation
      {
        ShipCode = rdr.GetString(0),
        ShipName = rdr.GetString(1),
        CounterpartCode = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
        CounterpartName = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4),
        CounterpartFaction = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5),
        TimeSeconds = (rdr.GetInt64(6)) / 1000.0,
        WareId = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7),
        Volume = rdr.IsDBNull(9) ? 0 : Convert.ToInt32(rdr.GetInt64(9)),
        Money = rdr.IsDBNull(10) ? 0 : Convert.ToInt32(rdr.GetInt64(10)),
      };
    }
  }

  public void LoadGameXmlFiles(Action<ProgressUpdate>? progress = null)
  {
    ReOpenConnection();
    if (_conn == null)
      throw new InvalidOperationException("Database connection is not initialized.");
    string gamePath = ConfigurationService.Instance.GameFolderExePath ?? string.Empty;
    if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
      return;
    List<string> dlcPathList = GetDLCsSorted();
    dlcPathList.Insert(0, string.Empty); // base game first
    ClearTableText();
    ClearTableWare();
    ClearTableFaction();
    _waresProcessed = 0;
    _factionsProcessed = 0;
    _processedFiles = 0;
    // dlcPathList.Prepend(string.Empty); // base game first
    foreach (var dlcRelPath in dlcPathList)
    {
      var dlcFullPath = string.IsNullOrWhiteSpace(dlcRelPath) ? gamePath : Path.Combine(gamePath, dlcRelPath);
      // Report current package: base game or last folder name of the dlc path
      string packageLabel = string.IsNullOrWhiteSpace(dlcRelPath)
        ? "game"
        : (dlcRelPath.Replace('\\', '/').Split('/').LastOrDefault() ?? dlcRelPath);
      progress?.Invoke(new ProgressUpdate { CurrentPackage = packageLabel });

      ContentExtractor contentExtractor = new(dlcFullPath);
      if (contentExtractor.FileCount == 0)
      {
        continue;
      }
      if (string.IsNullOrWhiteSpace(dlcRelPath))
      {
        progress?.Invoke(new ProgressUpdate { Status = "Parsing texts..." });
        LoadTextsFromGameT(contentExtractor, progress);
      }
      progress?.Invoke(new ProgressUpdate { Status = "Parsing wares..." });
      LoadWaresXml(contentExtractor, progress);
      progress?.Invoke(new ProgressUpdate { Status = "Parsing factions..." });
      LoadFactionsXml(contentExtractor, progress);
    }
    RefreshStats();
  }

  private void ClearTableText()
  {
    ReOpenConnection();
    // 2) Refine all values and write to DB
    using (var deleteCmd = _conn.CreateCommand())
    {
      deleteCmd.CommandText = @"DELETE FROM text;";
      deleteCmd.ExecuteNonQuery();
    }
    using (var vacuumCmd = _conn.CreateCommand())
    {
      vacuumCmd.CommandText = "VACUUM;";
      vacuumCmd.ExecuteNonQuery();
    }
  }

  private void LoadTextsFromGameT(ContentExtractor contentExtractor, Action<ProgressUpdate>? progress = null)
  {
    ReOpenConnection();
    string mask = "t/*-l*.xml";
    int langId = 0;
    string gameFolder = ConfigurationService.Instance.GameFolderExePath ?? string.Empty;
    string langPath = Path.Combine(gameFolder, "lang.dat");
    if (File.Exists(langPath))
    {
      var first = File.ReadLines(langPath).FirstOrDefault();
      langId = ParseInt(first);
      if (langId > 0)
      {
        using var update = new SQLiteCommand($"UPDATE settings SET current_language = {langId};", _conn);
        update.ExecuteNonQuery();
        if (ConfigurationService.Instance.LoadOnlyGameLanguage)
        {
          mask = $"t/*-l{langId:D3}.xml";
        }
      }
    }
    List<CatEntry> catEntries = contentExtractor.GetFilesByMask(mask);
    string[] files = catEntries.Select(e => e.FilePath).Distinct().ToArray();
    if (files.Length == 0)
    {
      return;
    }
    // 1) Parse all files and collect raw strings per (lang,page,id)
    //    This allows forward references during refinement.
    var raw = new Dictionary<int, Dictionary<int, Dictionary<int, string>>>(); // lang -> page -> id -> text

    var seenLangs = new HashSet<int>();
    foreach (var file in files)
    {
      try
      {
        CatEntry entry = catEntries.Last(e => e.FilePath == file);
        using var cs = ContentExtractor.OpenEntryStream(entry);
        var doc = XDocument.Load(cs, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

        // Find language scope element
        var languageElems = doc.Descendants()
          .Where(e => string.Equals(e.Name.LocalName, "language", StringComparison.OrdinalIgnoreCase))
          .ToList();

        XElement? scope = languageElems.First();
        int lang = 0;

        lang = ParseInt((string?)scope.Attribute("id"));

        if (lang <= 0 || scope == null)
        {
          continue;
        }
        if (seenLangs.Add(lang))
        {
          progress?.Invoke(new ProgressUpdate { Languages = seenLangs.Count });
        }

        // Helper to add an item
        void AddItem(int page, int id, string value)
        {
          if (page <= 0 || id <= 0)
            return;
          if (!raw.TryGetValue(lang, out var pages))
          {
            pages = new Dictionary<int, Dictionary<int, string>>();
            raw[lang] = pages;
          }
          if (!pages.TryGetValue(page, out var items))
          {
            items = new Dictionary<int, string>();
            pages[page] = items;
          }
          items[id] = value ?? string.Empty;
        }

        // 1) <page id> ... <t id>value</t> ...
        foreach (
          var pageElem in scope.Descendants().Where(e => string.Equals(e.Name.LocalName, "page", StringComparison.OrdinalIgnoreCase))
        )
        {
          int pageId = ParseInt((string?)pageElem.Attribute("id"));
          if (pageId <= 0)
          {
            continue;
          }
          int tCountInPage = 0;
          foreach (var tElem in pageElem.Descendants().Where(e => string.Equals(e.Name.LocalName, "t", StringComparison.OrdinalIgnoreCase)))
          {
            int id = ParseInt((string?)tElem.Attribute("id"));
            if (id <= 0)
            {
              continue;
            }
            string value = tElem.Value ?? string.Empty; // concatenated text, entities decoded
            AddItem(pageId, id, value);
            tCountInPage++;
          }
          progress?.Invoke(new ProgressUpdate { CurrentPage = pageId, TItemsInPage = tCountInPage });
        }

        // 2) <t page="..." id="...">value</t> not inside a <page>
        foreach (var tElem in scope.Descendants().Where(e => string.Equals(e.Name.LocalName, "t", StringComparison.OrdinalIgnoreCase)))
        {
          // Skip those already handled under a page
          bool underPage = tElem.Ancestors().Any(a => string.Equals(a.Name.LocalName, "page", StringComparison.OrdinalIgnoreCase));
          if (underPage)
          {
            continue;
          }
          int pageId = ParseInt((string?)tElem.Attribute("page"));
          int id = ParseInt((string?)tElem.Attribute("id"));
          if (pageId <= 0 || id <= 0)
          {
            continue;
          }
          string value = tElem.Value ?? string.Empty;
          AddItem(pageId, id, value);
        }
      }
      catch
      {
        // Skip file on any parse error
      }
      finally
      {
        _processedFiles++;
        progress?.Invoke(new ProgressUpdate { ProcessedFiles = _processedFiles });
      }
    }

    SQLiteTransaction txn = _conn.BeginTransaction();
    using var insert = new SQLiteCommand(
      "INSERT OR REPLACE INTO text(id_uniq, language, page, id, text) VALUES (@uniq,@lang,@page,@id,@text)",
      _conn,
      txn
    );
    insert.Parameters.Add("@uniq", System.Data.DbType.Int64);
    insert.Parameters.Add("@lang", System.Data.DbType.Int32);
    insert.Parameters.Add("@page", System.Data.DbType.Int32);
    insert.Parameters.Add("@id", System.Data.DbType.Int32);
    insert.Parameters.Add("@text", System.Data.DbType.String);
    long count = 0;
    // Determine target language when configured to load only one language
    int? targetLanguage = null;
    if (ConfigurationService.Instance.LoadOnlyGameLanguage && raw.Count > 0)
    {
      // Pick the language with the highest number of entries (data-driven)
      targetLanguage = raw.Select(kv => new { Lang = kv.Key, Count = kv.Value.Sum(p => p.Value.Count) })
        .OrderByDescending(x => x.Count)
        .First()
        .Lang;
    }

    // Memoization per language for resolved strings
    foreach (var (lang, pages) in raw)
    {
      if (targetLanguage.HasValue && lang != targetLanguage.Value)
      {
        continue; // skip other languages when only one language is requested
      }
      var memo = new Dictionary<(int page, int id), string>();

      foreach (var (page, items) in pages)
      {
        foreach (var (id, value) in items)
        {
          string refined = ResolveAndRefine(lang, page, id, raw, memo, depth: 0);
          long uniq = MakeUniqueKey(lang, page, id);
          insert.Parameters["@uniq"].Value = uniq;
          insert.Parameters["@lang"].Value = lang;
          insert.Parameters["@page"].Value = page;
          insert.Parameters["@id"].Value = id;
          insert.Parameters["@text"].Value = refined;
          insert.ExecuteNonQuery();
          count++;
          if (count % 100 == 0)
          {
            // Report stored T items progress every 100 rows
            progress?.Invoke(new ProgressUpdate { StoredTItems = (int)count });
          }
          if (count % _batchSize == 0)
          {
            txn.Commit();
            txn = _conn.BeginTransaction();
          }
        }
      }
    }
    // Final report and commit
    progress?.Invoke(new ProgressUpdate { StoredTItems = (int)count });
    txn.Commit();
  }

  private static int? TryReadLanguageAttribute(string filePath)
  {
    try
    {
      using var fs = File.OpenRead(filePath);
      var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
      using var xr = XmlReader.Create(fs, settings);
      while (xr.Read())
      {
        if (xr.NodeType == XmlNodeType.Element && xr.Name.Equals("language", StringComparison.OrdinalIgnoreCase))
        {
          return ParseInt(xr.GetAttribute("id"));
        }
      }
    }
    catch { }
    return null;
  }

  private static int ParseInt(string? s)
  {
    return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
  }

  private static long MakeUniqueKey(int language, int page, int id)
  {
    // Build a 64-bit composite key: [16 bits lang][24 bits page][24 bits id]
    long key = ((long)language << 48) | ((long)page << 24) | (uint)id;
    return key;
  }

  private static string ResolveAndRefine(
    int language,
    int page,
    int id,
    Dictionary<int, Dictionary<int, Dictionary<int, string>>> raw,
    Dictionary<(int page, int id), string> memo,
    int depth
  )
  {
    const int MaxDepth = 8;
    if (depth > MaxDepth)
    {
      return string.Empty;
    }
    if (memo.TryGetValue((page, id), out var cached))
      return cached;

    if (!raw.TryGetValue(language, out var pages) || !pages.TryGetValue(page, out var items) || !items.TryGetValue(id, out var value))
    {
      memo[(page, id)] = string.Empty;
      return string.Empty;
    }

    // First, expand references like {page,id}
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    string expanded = Regex.Replace(
      value,
      "\\{(\\d+),(\\d+)\\}",
      m =>
      {
        int rp = ParseInt(m.Groups[1].Value);
        int rid = ParseInt(m.Groups[2].Value);
        if (rp <= 0 || rid <= 0)
          return string.Empty;
        return ResolveAndRefine(language, rp, rid, raw, memo, depth + 1);
      }
    );
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

    // Then, remove segments within unescaped parentheses; keep escaped ones literally (without backslash)
    string refined = RemoveUnescapedParentheses(expanded);

    // Normalize whitespace
    refined = refined.Replace("\r", string.Empty).Replace("\n", " ");
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    refined = Regex.Replace(refined, "\\s+", " ").Trim();
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

    memo[(page, id)] = refined;
    return refined;
  }

  private static string RemoveUnescapedParentheses(string input)
  {
    if (string.IsNullOrEmpty(input))
    {
      return string.Empty;
    }
    var sb = new StringBuilder(input.Length);
    int depth = 0;
    for (int i = 0; i < input.Length; i++)
    {
      char c = input[i];
      char prev = i > 0 ? input[i - 1] : '\0';
      bool escaped = prev == '\\';
      if (c == '(' && !escaped)
      {
        depth++;
        continue;
      }
      if (c == ')' && !escaped)
      {
        if (depth > 0)
        {
          depth--;
        }
        continue;
      }
      if (depth > 0)
      {
        // Inside excluded segment, skip unless this char reduces depth (handled above)
        continue;
      }
      if (c == '(' && escaped)
      {
        // Keep literal '(' without backslash
        // remove the backslash previously appended if any
        if (sb.Length > 0 && sb[^1] == '\\')
        {
          sb.Length -= 1;
        }
        sb.Append('(');
        continue;
      }
      if (c == ')' && escaped)
      {
        if (sb.Length > 0 && sb[^1] == '\\')
        {
          sb.Length -= 1;
        }
        sb.Append(')');
        continue;
      }
      sb.Append(c);
    }
    return sb.ToString();
  }

  private void ClearTableWare()
  {
    ReOpenConnection();
    // 2) Refine all values and write to DB
    using (var deleteCmd = _conn.CreateCommand())
    {
      deleteCmd.CommandText = @"DELETE FROM ware;";
      deleteCmd.ExecuteNonQuery();
    }
    using (var vacuumCmd = _conn.CreateCommand())
    {
      vacuumCmd.CommandText = "VACUUM;";
      vacuumCmd.ExecuteNonQuery();
    }
  }

  private static (int page, int id) ParseTextItem(string input)
  {
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    var match = Regex.Match(input, @"^\{(\d+),(\d+)\}$", RegexOptions.IgnoreCase);
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    if (match.Success && match.Groups.Count == 3)
    {
      int page = int.TryParse(match.Groups[1].Value, out var p) ? p : 0;
      int id = int.TryParse(match.Groups[2].Value, out var t) ? t : 0;
      return (page, id);
    }
    return (0, 0);
  }

  private string GetTextItem(
    string input,
    ref Dictionary<string, string> texts,
    ref HashSet<int> processedPageIds,
    string? alternate = null
  )
  {
    if (!string.IsNullOrEmpty(input))
    {
      var (page, id) = ParseTextItem(input);
      if (page > 0 && id > 0)
      {
        if (!processedPageIds.Contains(page))
        {
          List<int> pages = new() { page };
          // Load this page into factionNames dictionary
          texts = GetTextDict(pages, texts, true);
          processedPageIds.Add(page);
        }
        if (texts.TryGetValue($"{page}:{id}", out var resolvedText))
        {
          return resolvedText;
        }
      }
    }
    return alternate ?? input;
  }

  private void LoadWaresXml(ContentExtractor contentExtractor, Action<ProgressUpdate>? progress = null)
  {
    const string file = "libraries/wares.xml";
    ReOpenConnection();

    List<CatEntry> catEntries = contentExtractor.GetFilesByMask(file);
    string[] files = catEntries.Select(e => e.FilePath).ToArray();
    if (files.Length == 0)
    {
      return;
    }
    try
    {
      CatEntry entry = catEntries.Last(e => e.FilePath == file);
      using var cs = ContentExtractor.OpenEntryStream(entry);
      SQLiteTransaction txn = _conn.BeginTransaction();

      using var cmd = new SQLiteCommand(
        "INSERT OR IGNORE INTO ware(id, name, group_of, transport, volume, price_min, price_avg, price_max, text) VALUES (@id,@name,@group_of,@transport,@volume,@price_min,@price_avg,@price_max,@text)",
        _conn,
        txn
      );
      cmd.Parameters.Add("@id", System.Data.DbType.String);
      cmd.Parameters.Add("@name", System.Data.DbType.String);
      cmd.Parameters.Add("@group_of", System.Data.DbType.String);
      cmd.Parameters.Add("@transport", System.Data.DbType.String);
      cmd.Parameters.Add("@volume", System.Data.DbType.Int64);
      cmd.Parameters.Add("@price_min", System.Data.DbType.Int64);
      cmd.Parameters.Add("@price_avg", System.Data.DbType.Int64);
      cmd.Parameters.Add("@price_max", System.Data.DbType.Int64);
      cmd.Parameters.Add("@text", System.Data.DbType.String);

      var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
      using var xr = XmlReader.Create(cs, settings);

      Dictionary<string, string> wareNames = new();
      HashSet<int> processedPageIds = new();

      while (xr.Read())
      {
        if (xr.NodeType != XmlNodeType.Element || xr.Name != "ware")
          continue;
        string id = xr.GetAttribute("id") ?? string.Empty;
        string name = (xr.GetAttribute("name") ?? string.Empty);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrEmpty(name))
          continue;

        string group = (xr.GetAttribute("group") ?? string.Empty);
        string transport = (xr.GetAttribute("transport") ?? string.Empty);
        long volume = 0;
        long.TryParse(xr.GetAttribute("volume") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out volume);
        long min = 0,
          avg = 0,
          max = 0;
        if (!xr.IsEmptyElement)
        {
          var depth = xr.Depth;
          while (xr.Read())
          {
            if (xr.NodeType == XmlNodeType.Element && xr.Name == "price")
            {
              long.TryParse(xr.GetAttribute("min") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out min);
              long.TryParse(xr.GetAttribute("average") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out avg);
              long.TryParse(xr.GetAttribute("max") ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out max);
            }
            else if (xr.NodeType == XmlNodeType.EndElement && xr.Name == "ware" && xr.Depth == depth)
            {
              break;
            }
          }
        }

        cmd.Parameters["@id"].Value = id;
        cmd.Parameters["@name"].Value = name;
        cmd.Parameters["@group_of"].Value = group;
        cmd.Parameters["@transport"].Value = transport;
        cmd.Parameters["@volume"].Value = volume;
        cmd.Parameters["@price_min"].Value = min * 100;
        cmd.Parameters["@price_avg"].Value = avg * 100;
        cmd.Parameters["@price_max"].Value = max * 100;
        cmd.Parameters["@text"].Value = GetTextItem(name, ref wareNames, ref processedPageIds, alternate: id);

        cmd.ExecuteNonQuery();
        _waresProcessed++;
        if (_waresProcessed % 10 == 0)
        {
          progress?.Invoke(new ProgressUpdate { WaresProcessed = _waresProcessed });
        }
      }
      // Final report
      progress?.Invoke(new ProgressUpdate { WaresProcessed = _waresProcessed });
      txn.Commit();
    }
    catch (Exception ex)
    {
      Console.WriteLine("Error loading wares.xml: " + ex.Message);
      return;
    }
    finally
    {
      _processedFiles++;
      progress?.Invoke(new ProgressUpdate { ProcessedFiles = _processedFiles });
    }
  }

  private void ClearTableFaction()
  {
    ReOpenConnection();
    // 2) Refine all values and write to DB
    using (var deleteCmd = _conn.CreateCommand())
    {
      deleteCmd.CommandText = @"DELETE FROM faction;";
      deleteCmd.ExecuteNonQuery();
    }
    using (var vacuumCmd = _conn.CreateCommand())
    {
      vacuumCmd.CommandText = "VACUUM;";
      vacuumCmd.ExecuteNonQuery();
    }
  }

  private void LoadFactionsXml(ContentExtractor contentExtractor, Action<ProgressUpdate>? progress = null)
  {
    const string file = "libraries/factions.xml";
    ReOpenConnection();

    List<CatEntry> catEntries = contentExtractor.GetFilesByMask(file);
    string[] files = catEntries.Select(e => e.FilePath).ToArray();
    if (files.Length == 0)
    {
      return;
    }
    try
    {
      CatEntry entry = catEntries.Last(e => e.FilePath == file);
      using var cs = ContentExtractor.OpenEntryStream(entry);
      SQLiteTransaction txn = _conn.BeginTransaction();

      using var cmd = new SQLiteCommand(
        "INSERT OR IGNORE INTO faction(id, name, shortname, prefixname) VALUES (@id,@name,@shortname,@prefixname)",
        _conn,
        txn
      );
      cmd.Parameters.Add("@id", System.Data.DbType.String);
      cmd.Parameters.Add("@name", System.Data.DbType.String);
      cmd.Parameters.Add("@shortname", System.Data.DbType.String);
      cmd.Parameters.Add("@prefixname", System.Data.DbType.String);

      var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
      using var xr = XmlReader.Create(cs, settings);

      Dictionary<string, string> factionNames = new();
      HashSet<int> processedPageIds = new();

      while (xr.Read())
      {
        if (xr.NodeType != XmlNodeType.Element || xr.Name != "faction")
          continue;
        string id = xr.GetAttribute("id") ?? string.Empty;
        string name = GetTextItem(xr.GetAttribute("name") ?? string.Empty, ref factionNames, ref processedPageIds);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrEmpty(name))
          continue;
        string shortname = GetTextItem(xr.GetAttribute("shortname") ?? string.Empty, ref factionNames, ref processedPageIds);
        string prefixname = GetTextItem(xr.GetAttribute("prefixname") ?? string.Empty, ref factionNames, ref processedPageIds);

        cmd.Parameters["@id"].Value = id;
        cmd.Parameters["@name"].Value = name;
        cmd.Parameters["@shortname"].Value = shortname;
        cmd.Parameters["@prefixname"].Value = prefixname;

        cmd.ExecuteNonQuery();
        _factionsProcessed++;
        progress?.Invoke(new ProgressUpdate { FactionsProcessed = _factionsProcessed });
      }
      // Final report
      txn.Commit();
    }
    catch (Exception ex)
    {
      Console.WriteLine("Error loading factions.xml: " + ex.Message);
      return;
    }
    finally
    {
      _processedFiles++;
      progress?.Invoke(new ProgressUpdate { ProcessedFiles = _processedFiles });
    }
  }

  public void ImportSaveGame(Action<ProgressUpdate>? progress = null)
  {
    // Prefer external SaveGamesFolder from configuration; fallback to local GameData copy
    string savePath = ConfigurationService.Instance.GameSavePath ?? string.Empty;
    if (string.IsNullOrWhiteSpace(savePath) || !File.Exists(savePath))
      return;
    ReOpenConnection();
    if (_conn == null)
      throw new InvalidOperationException("Database connection is not initialized.");

    // Clear existing data (outside a transaction is fine for SQLite here)
    using (var cmd = _conn.CreateCommand())
    {
      cmd.CommandText = @"DELETE FROM trade; DELETE FROM component;";
      cmd.ExecuteNonQuery();
    }

    // Reclaim space after delete
    using (var vacuumCmd = _conn.CreateCommand())
    {
      vacuumCmd.CommandText = "VACUUM;";
      vacuumCmd.ExecuteNonQuery();
    }

    var factoryNames = GetFactoryNamesDict();

    // Begin transactional import for speed and atomicity
    SQLiteTransaction txn = _conn.BeginTransaction();

    using var insertComp = new SQLiteCommand(
      "INSERT OR IGNORE INTO component(id, type, class, owner, name, nameindex, code) VALUES (@id,@type,@class,@owner,@name,@nameindex,@code)",
      _conn,
      txn
    );
    insertComp.Parameters.Add("@id", System.Data.DbType.Int64);
    insertComp.Parameters.Add("@type", System.Data.DbType.String);
    insertComp.Parameters.Add("@class", System.Data.DbType.String);
    insertComp.Parameters.Add("@owner", System.Data.DbType.String);
    insertComp.Parameters.Add("@name", System.Data.DbType.String);
    insertComp.Parameters.Add("@nameindex", System.Data.DbType.String);
    insertComp.Parameters.Add("@code", System.Data.DbType.String);

    using var insertTrade = new SQLiteCommand(
      "INSERT OR IGNORE INTO trade(seller, buyer, ware, price, volume, time) VALUES (@seller,@buyer,@ware,@price,@volume,@time)",
      _conn,
      txn
    );
    insertTrade.Parameters.Add("@seller", System.Data.DbType.Int64);
    insertTrade.Parameters.Add("@buyer", System.Data.DbType.Int64);
    insertTrade.Parameters.Add("@ware", System.Data.DbType.String);
    insertTrade.Parameters.Add("@price", System.Data.DbType.Int64);
    insertTrade.Parameters.Add("@volume", System.Data.DbType.Int64);
    insertTrade.Parameters.Add("@time", System.Data.DbType.Int64);

    long elementsProcessed = 0;
    int compCount = 0,
      stationsProcessed = 0,
      shipsProcessed = 0,
      tradeCount = 0;

    DateTime startTime = DateTime.Now;
    Console.WriteLine("Started at " + startTime);

    using var fs = new FileStream(savePath, FileMode.Open, FileAccess.Read);
    using var gz = new GZipStream(fs, CompressionMode.Decompress);
    using var sr = new StreamReader(gz);
    XmlReaderSettings settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
    using var xr = XmlReader.Create(sr, settings);

    bool tradeEntries = false;
    bool connectionsProcessed = false;
    bool tradesProcessed = false;
    bool timeProcessed = false;
    bool detectNameViaProduction = false;
    int gameTime = 0;
    Dictionary<string, string> factoryBaseNames = new();
    HashSet<int> processedPageIds = new();

    while (xr.Read())
    {
      if (xr.NodeType != XmlNodeType.Element && xr.NodeType != XmlNodeType.EndElement)
        continue;
      if (xr.NodeType == XmlNodeType.Element)
      {
        elementsProcessed++;
        if (elementsProcessed % 2000 == 0)
        {
          progress?.Invoke(new ProgressUpdate { ElementsProcessed = (int)Math.Min(int.MaxValue, elementsProcessed) });
        }
        if (!timeProcessed && xr.Name == "game")
        {
          gameTime = NormalizeTime(xr.GetAttribute("time") ?? string.Empty);
          timeProcessed = true;
        }
        if (!tradesProcessed && xr.Name == "entries" && xr.GetAttribute("type") == "trade")
        {
          tradeEntries = true;
          detectNameViaProduction = false;
          progress?.Invoke(new ProgressUpdate { Status = "Importing trade logs..." });
          txn.Commit();
          txn.Dispose();
          txn = _conn.BeginTransaction();
          // rebind commands to the new transaction
          insertComp.Transaction = txn;
          insertTrade.Transaction = txn;
          continue;
        }
        if (!tradesProcessed && xr.Name == "entries" && xr.GetAttribute("type") == "money")
        {
          tradeEntries = false;
          tradesProcessed = true;
          continue;
        }
        if (!connectionsProcessed && xr.Name == "component")
        {
          if (detectNameViaProduction && xr.Name == "component" && xr.GetAttribute("class") == "production")
          {
            string macro = xr.GetAttribute("macro") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(macro))
            {
              var macroParts = macro.Split('_');
              if (macroParts.Length == 4)
              {
                insertComp.Parameters["@name"].Value = factoryNames.TryGetValue(macroParts[2], out var n) ? n : macro;
              }
              else
              {
                insertComp.Parameters["@name"].Value = macro;
              }
              insertComp.ExecuteNonQuery();
              compCount++;
              detectNameViaProduction = false;
            }
            continue;
          }

          if (xr.GetAttribute("knownto") != "player")
            continue;

          string componentClass = xr.GetAttribute("class") ?? string.Empty;

          if (!(componentClass == "station") && !componentClass.StartsWith("ship_"))
          {
            continue;
          }
          long id = ParseId(xr.GetAttribute("id") ?? string.Empty);
          if (id <= 0)
          {
            continue;
          }
          if (componentClass == "station")
          {
            stationsProcessed++;
            if (stationsProcessed % 50 == 0)
            {
              progress?.Invoke(new ProgressUpdate { StationsProcessed = stationsProcessed });
            }
          }
          else
          {
            shipsProcessed++;
            if (shipsProcessed % 50 == 0)
            {
              progress?.Invoke(new ProgressUpdate { ShipsProcessed = shipsProcessed });
            }
          }
          string type = componentClass == "station" ? "station" : "ship";
          string owner = xr.GetAttribute("owner") ?? "";
          if (type == "ship" && owner != "player")
          {
            continue;
          }
          string name = xr.GetAttribute("name") ?? "";
          if (string.IsNullOrWhiteSpace(name))
          {
            name = xr.GetAttribute("basename") ?? "";
          }
          string code = xr.GetAttribute("code") ?? "";
          if (string.IsNullOrWhiteSpace(code))
          {
            continue;
          }
          insertComp.Parameters["@id"].Value = id;
          insertComp.Parameters["@type"].Value = type;
          insertComp.Parameters["@class"].Value = componentClass;
          insertComp.Parameters["@owner"].Value = owner;
          insertComp.Parameters["@code"].Value = code;
          insertComp.Parameters["@nameindex"].Value = GameData.nameIndex[int.Parse(xr.GetAttribute("nameindex") ?? "0")] ?? "";
          if (!string.IsNullOrEmpty(name))
          {
            insertComp.Parameters["@name"].Value = GetTextItem(name, ref factoryBaseNames, ref processedPageIds);
            insertComp.ExecuteNonQuery();
            compCount++;
            detectNameViaProduction = false;
          }
          else
          {
            detectNameViaProduction = true;
          }
          continue;
        }
        if (tradeEntries && xr.Name == "log")
        {
          long seller = ParseId(xr.GetAttribute("seller") ?? string.Empty);
          if (seller <= 0)
          {
            continue;
          }
          long buyer = ParseId(xr.GetAttribute("buyer") ?? string.Empty);
          if (buyer <= 0)
          {
            continue;
          }
          int time = NormalizeTime(xr.GetAttribute("time") ?? string.Empty);
          if (time <= 0)
          {
            continue;
          }
          string ware = xr.GetAttribute("ware") ?? string.Empty;
          if (string.IsNullOrWhiteSpace(ware))
          {
            continue;
          }
          long price = long.Parse(xr.GetAttribute("price") ?? string.Empty, CultureInfo.InvariantCulture);
          if (price <= 0)
          {
            continue;
          }
          long volume = long.Parse(xr.GetAttribute("v") ?? string.Empty, CultureInfo.InvariantCulture);
          if (volume <= 0)
          {
            continue;
          }
          insertTrade.Parameters["@seller"].Value = seller;
          insertTrade.Parameters["@buyer"].Value = buyer;
          insertTrade.Parameters["@ware"].Value = ware;
          insertTrade.Parameters["@price"].Value = price;
          insertTrade.Parameters["@volume"].Value = volume;
          insertTrade.Parameters["@time"].Value = time - gameTime;
          insertTrade.ExecuteNonQuery();
          tradeCount++;
          if (tradeCount % 100 == 0)
          {
            progress?.Invoke(new ProgressUpdate { TradesProcessed = tradeCount });
          }

          continue;
        }
        if (detectNameViaProduction && xr.Name == "production")
        {
          string product = xr.GetAttribute("originalproduct") ?? string.Empty;
          if (!string.IsNullOrWhiteSpace(product))
          {
            insertComp.Parameters["@name"].Value = factoryNames.TryGetValue(product, out var n) ? n : product;
            insertComp.ExecuteNonQuery();
            compCount++;
            detectNameViaProduction = false;
          }
          continue;
        }
      }

      if (xr.NodeType == XmlNodeType.EndElement && xr.Name == "universe")
      {
        connectionsProcessed = true;
      }

      if ((compCount + tradeCount) % _batchSize == 0)
      {
        txn.Commit();
        txn.Dispose();
        txn = _conn.BeginTransaction();
        // rebind commands to the new transaction
        insertComp.Transaction = txn;
        insertTrade.Transaction = txn;
      }
    }

    txn.Commit();

    // Final report
    progress?.Invoke(
      new ProgressUpdate
      {
        ElementsProcessed = (int)Math.Min(int.MaxValue, elementsProcessed),
        StationsProcessed = stationsProcessed,
        ShipsProcessed = shipsProcessed,
        TradesProcessed = tradeCount,
        Status = "Save import complete.",
      }
    );

    Console.WriteLine($"Imported {compCount} components, {tradeCount} trades. Time spent: {DateTime.Now - startTime}");
    RefreshStats();
  }

  public void RefreshStats()
  {
    try
    {
      // trade count
      Stats.TradesCount = ExecuteScalarInt("SELECT COUNT(1) FROM trade");
      // player ships via view (if exists)
      Stats.PlayerShipsCount = ViewExists("player_ships")
        ? ExecuteScalarInt("SELECT COUNT(1) FROM player_ships")
        : ExecuteScalarInt("SELECT COUNT(1) FROM component WHERE type='ship' AND owner='player'");
      // stations via view (if exists)
      Stats.StationsCount = ViewExists("stations")
        ? ExecuteScalarInt("SELECT COUNT(1) FROM stations")
        : ExecuteScalarInt("SELECT COUNT(1) FROM component WHERE type='station'");
      // wares table may not exist in this project schema; guard it
      Stats.WaresCount = TableExists("ware") ? ExecuteScalarInt("SELECT COUNT(1) FROM ware") : 0;
      // factions count if table exists
      Stats.FactionsCount = TableExists("faction") ? ExecuteScalarInt("SELECT COUNT(1) FROM faction") : 0;

      // languages present in text table
      if (TableExists("text"))
      {
        Stats.LanguagesCount = ExecuteScalarInt("SELECT COUNT(DISTINCT language) FROM text");

        // Always read the current language id from settings (0 if missing)
        int currentLang = ExecuteScalarInt("SELECT current_language FROM settings LIMIT 1");
        Stats.CurrentLanguageId = currentLang;

        // Count texts for the current language if available
        if (ViewExists("lang"))
        {
          Stats.CurrentLanguageTextCount = ExecuteScalarInt("SELECT COUNT(1) FROM lang");
        }
        else
        {
          Stats.CurrentLanguageTextCount = ExecuteScalarInt($"SELECT COUNT(1) FROM text WHERE language = {currentLang}");
        }
      }
      else
      {
        Stats.LanguagesCount = 0;
        Stats.CurrentLanguageTextCount = 0;
        Stats.CurrentLanguageId = 0;
      }
    }
    catch
    {
      // Ignore stats errors; keep previous values
    }
  }

  bool TableExists(string table)
  {
    ReOpenConnection();
    using var cmd = _conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@n";
    cmd.Parameters.AddWithValue("@n", table);
    var o = cmd.ExecuteScalar();
    return Convert.ToInt32(o, CultureInfo.InvariantCulture) > 0;
  }

  bool ViewExists(string view)
  {
    ReOpenConnection();
    using var cmd = _conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='view' AND name=@n";
    cmd.Parameters.AddWithValue("@n", view);
    var o = cmd.ExecuteScalar();
    return Convert.ToInt32(o, CultureInfo.InvariantCulture) > 0;
  }

  int ExecuteScalarInt(string sql)
  {
    ReOpenConnection();
    using var cmd = _conn.CreateCommand();
    cmd.CommandText = sql;
    var o = cmd.ExecuteScalar();
    return o == null || o is DBNull ? 0 : Convert.ToInt32(o, CultureInfo.InvariantCulture);
  }

  static long ParseId(string raw)
  {
    if (string.IsNullOrWhiteSpace(raw))
      return 0;
    if (!raw.StartsWith('[') || !raw.EndsWith(']'))
      return 0;
    string hex = raw.Trim('[', ']');
    if (string.IsNullOrWhiteSpace(hex))
      return 0;
    try
    {
      if (hex.StartsWith("0x"))
      {
        hex = hex.Substring(2);
        return long.Parse(hex, NumberStyles.HexNumber);
      }
      else
      {
        return long.Parse(hex);
      }
    }
    catch
    {
      return 0;
    }
  }

  static int NormalizeTime(string raw)
  {
    if (string.IsNullOrWhiteSpace(raw))
      return 0;
    if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
      return 0;
    return (int)Math.Round(f * 1000); // ms ticks
  }

  private (Dictionary<string, string>, HashSet<int>) GetProductionWareDict()
  {
    ReOpenConnection();
    var dict = new Dictionary<string, string>();
    var uniquePages = new HashSet<int>();

    using var cmd = new SQLiteCommand(
      @"
    SELECT id, name
    FROM ware
    WHERE id NOT LIKE '%\_%' ESCAPE '\'
      AND id <> 'credits'
      AND id <> 'crew';",
      _conn
    );

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      string id = reader.GetString(0);
      string name = reader.GetString(1);
      (int textPage, int textId) = ParseTextItem(name);
      if (textPage > 0 && textId > 0)
      {
        string value = $"{textPage}:{textId + 3}";
        dict[id] = value;
        uniquePages.Add(textPage);
      }
    }
    return (dict, uniquePages);
  }

  private Dictionary<string, string> GetTextDict(
    List<int> pages,
    Dictionary<string, string>? existingDict = null,
    bool? newConnection = null
  )
  {
    SQLiteConnection? conn;
    if (newConnection == true)
    {
      conn = CreateConnection();
    }
    else
    {
      ReOpenConnection();
      conn = _conn;
    }
    var dict = existingDict ?? new Dictionary<string, string>();

    // Build parameter placeholders dynamically
    var placeholders = string.Join(",", pages.Select((_, i) => $"@p{i}"));

    using var cmd = new SQLiteCommand($"SELECT page, id, text FROM lang WHERE page IN ({placeholders});", conn);

    // Add parameters
    for (int i = 0; i < pages.Count; i++)
    {
      cmd.Parameters.AddWithValue($"@p{i}", pages[i]);
    }

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      int page = reader.GetInt32(0);
      int id = reader.GetInt32(1);
      string text = reader.GetString(2);

      string key = $"{page}:{id}";
      dict[key] = text;
    }
    return dict;
  }

  private Dictionary<string, string> GetFactoryNamesDict()
  {
    ReOpenConnection();
    var (prodDict, uniquePages) = GetProductionWareDict();
    var dict = GetTextDict(uniquePages.ToList());
    foreach (var (wareId, prodKey) in prodDict)
    {
      if (dict.TryGetValue(prodKey, out var name))
      {
        prodDict[wareId] = name;
      }
    }
    return prodDict;
  }
}
