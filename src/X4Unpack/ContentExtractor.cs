using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

// Minimal logger replacement
internal static class Log
{
  public static void Warn(string message) { }

  public static void Debug(string message) { }
}

namespace X4Unpack
{
  public class CatEntry
  {
    public required string FilePath { get; set; }
    public long FileSize { get; set; }
    public long FileOffset { get; set; }
    public DateTime FileDate { get; set; }
    public required string FileHash { get; set; }
    public required string DatFilePath { get; set; }
  }

  public class ContentExtractor
  {
    protected string _folderPath;
    protected readonly Dictionary<string, CatEntry> _catalog;
    public int FileCount => _catalog.Count;
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    private Regex catEntryRegex = new(@"^(.+?)\s(\d+)\s(\d+)\s([0-9a-fA-F]{32})$");
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

    public ContentExtractor(string folderPath, string pattern = "*.cat", bool excludeSignatures = true)
    {
      _folderPath = folderPath;
      _catalog = new Dictionary<string, CatEntry>();
      InitializeCatalog(pattern, excludeSignatures);
    }

    protected virtual void InitializeCatalog(string pattern = "*.cat", bool excludeSignatures = true)
    {
      List<string> catFiles = Directory.GetFiles(_folderPath, pattern).ToList();
      if (excludeSignatures)
      {
        catFiles = catFiles.Where(f => !f.EndsWith("_sig.cat")).ToList();
      }
      catFiles.Sort();
      foreach (var catFilePath in catFiles)
      {
        string datFilePath = Path.ChangeExtension(catFilePath, ".dat");
        if (File.Exists(datFilePath))
        {
          ParseCatFile(catFilePath, datFilePath);
        }
      }
    }

    private void ParseCatFile(string catFilePath, string datFilePath)
    {
      long offset = 0;
      foreach (var line in File.ReadLines(catFilePath))
      {
        if (string.IsNullOrWhiteSpace(line))
        {
          continue;
        }
        Match? match = catEntryRegex.Match(line);
        if (!match.Success || match.Groups.Count < 5 || !match.Groups.Values.All(g => g.Success))
        {
          Log.Warn($"Warning: Invalid line in catalog file: {line}");
          continue;
        }
        long fileSize = long.TryParse(match.Groups[2].Value, out long sizeValue) ? sizeValue : 0;
        long unixTime = long.TryParse(match.Groups[3].Value, out long timeValue) ? timeValue : 0;
        string filePath = match.Groups[1].Value;
        _catalog[filePath] = new CatEntry
        {
          FilePath = filePath,
          FileSize = fileSize,
          FileOffset = offset,
          FileDate = DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime,
          FileHash = match.Groups[4].Value,
          DatFilePath = datFilePath,
        };
        offset += fileSize;
      }
    }

    public void ExtractFile(string filePath, string outputDirectory, bool overwrite = false, bool skipHashCheck = false)
    {
      var entry = _catalog.FirstOrDefault(e => e.Value.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)).Value;
      if (entry != null)
      {
        ExtractEntry(entry, outputDirectory, overwrite, skipHashCheck);
      }
      else
      {
        Log.Warn($"File {filePath} not found in catalog.");
      }
    }

    public bool FolderExists(string folderPath)
    {
      return _catalog.Any(e => e.Key.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase));
    }

    public List<CatEntry> GetFolderEntries(string folderPath)
    {
      return _catalog.Where(e => e.Key.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase)).Select(e => e.Value).ToList();
    }

    public void ExtractFolder(string folderPath, string outputDirectory, bool overwrite = false, bool skipHashCheck = false)
    {
      var entries = GetFolderEntries(folderPath);
      foreach (var entry in entries)
      {
        ExtractEntry(entry, outputDirectory, overwrite, skipHashCheck);
      }
    }

    public List<CatEntry> GetFilesByMask(string mask)
    {
      var regexPattern = "^" + Regex.Escape(mask).Replace("\\*", ".*").Replace("\\?", ".") + "$";
      var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

      return _catalog.Where(e => regex.IsMatch(e.Key)).Select(e => e.Value).ToList();
    }

    public void ExtractFilesByMask(string mask, string outputDirectory, bool overwrite = false, bool skipHashCheck = false)
    {
      var entries = GetFilesByMask(mask);
      foreach (var entry in entries)
      {
        ExtractEntry(entry, outputDirectory, overwrite, skipHashCheck);
      }
    }

    public static byte[] GetEntryData(CatEntry entry)
    {
      using var datFileStream = new FileStream(entry.DatFilePath, FileMode.Open, FileAccess.Read);
      datFileStream.Seek(entry.FileOffset, SeekOrigin.Begin);

      byte[] buffer = new byte[entry.FileSize];
      datFileStream.Read(buffer, 0, buffer.Length);
      return buffer;
    }

    /// <summary>
    /// Opens a read-only stream for the specified entry within its .dat file without loading it into memory.
    /// The returned stream is limited to the entry's size and owns the underlying file stream (dispose it when done).
    /// </summary>
    public static Stream OpenEntryStream(CatEntry entry)
    {
      var fs = new FileStream(entry.DatFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      return new SegmentStream(fs, entry.FileOffset, entry.FileSize, ownsBaseStream: true);
    }

    /// <summary>
    /// Stream wrapper that exposes a bounded segment [offset, offset+length) of a seekable base stream.
    /// </summary>
    private sealed class SegmentStream : Stream
    {
      private readonly Stream _baseStream;
      private readonly long _segmentStart;
      private readonly long _segmentLength;
      private readonly bool _ownsBaseStream;
      private long _positionWithin;

      public SegmentStream(Stream baseStream, long offset, long length, bool ownsBaseStream)
      {
        if (baseStream is null)
          throw new ArgumentNullException(nameof(baseStream));
        if (!baseStream.CanSeek)
          throw new NotSupportedException("Base stream must support seeking.");
        if (offset < 0)
          throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0)
          throw new ArgumentOutOfRangeException(nameof(length));

        _baseStream = baseStream;
        _segmentStart = offset;
        _segmentLength = length;
        _ownsBaseStream = ownsBaseStream;

        _baseStream.Seek(_segmentStart, SeekOrigin.Begin);
        _positionWithin = 0;
      }

      public override bool CanRead => true;
      public override bool CanSeek => true;
      public override bool CanWrite => false;
      public override long Length => _segmentLength;
      public override long Position
      {
        get => _positionWithin;
        set => Seek(value, SeekOrigin.Begin);
      }

      public override void Flush() { /* no-op */
      }

      public override int Read(byte[] buffer, int offset, int count)
      {
        if (buffer == null)
          throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
          throw new ArgumentOutOfRangeException();
        if (_positionWithin >= _segmentLength)
          return 0;

        long remaining = _segmentLength - _positionWithin;
        if (count > remaining)
          count = (int)remaining;

        int read = _baseStream.Read(buffer, offset, count);
        _positionWithin += read;
        return read;
      }

      public override long Seek(long offset, SeekOrigin origin)
      {
        long target = origin switch
        {
          SeekOrigin.Begin => offset,
          SeekOrigin.Current => _positionWithin + offset,
          SeekOrigin.End => _segmentLength + offset,
          _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        if (target < 0)
          target = 0;
        if (target > _segmentLength)
          target = _segmentLength;

        _baseStream.Seek(_segmentStart + target, SeekOrigin.Begin);
        _positionWithin = target;
        return _positionWithin;
      }

      public override void SetLength(long value) => throw new NotSupportedException();

      public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

      protected override void Dispose(bool disposing)
      {
        if (disposing && _ownsBaseStream)
        {
          _baseStream.Dispose();
        }
        base.Dispose(disposing);
      }
    }

    public virtual void ExtractEntry(CatEntry entry, string outputDirectory, bool overwrite = false, bool skipHashCheck = false)
    {
      string outputFilePath = Path.Combine(outputDirectory, entry.FilePath);
      Log.Debug($"Extracting {entry.FilePath} from {entry.DatFilePath} to {outputDirectory}");
      if (File.Exists(outputFilePath) && !overwrite)
      {
        Log.Warn($"File {entry.FilePath} already exists in output directory. Skipping extraction.");
        return;
      }

      byte[] buffer = GetEntryData(entry);

      if (!skipHashCheck)
      {
        string extractedFileHash = CalculateMD5Hash(buffer);
        if (extractedFileHash != entry.FileHash)
        {
          Log.Warn($"Warning: Hash mismatch for file {entry.FilePath}. Skipping extraction.");
          return;
        }
      }
      var directoryPath = Path.GetDirectoryName(outputFilePath);
      if (directoryPath != null)
      {
        Directory.CreateDirectory(directoryPath);
      }
      File.WriteAllBytes(outputFilePath, buffer);
      File.SetLastWriteTime(outputFilePath, entry.FileDate);
    }

    public static string CalculateMD5Hash(byte[] data)
    {
      byte[] hashBytes = MD5.HashData(data);
      return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
  }
}
