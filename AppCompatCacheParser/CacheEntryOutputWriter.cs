using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AppCompatCache;
using CsvHelper.TypeConversion;
using ServiceStack;
using ServiceStack.Text;
using CsvWriter = CsvHelper.CsvWriter;

namespace AppCompatCacheParser;

internal sealed class CacheEntryOutputWriter : IDisposable
{
    private StreamWriter _csvStream;
    private CsvWriter _csvWriter;
    private StreamWriter _jsonStream;

    public string CsvPath { get; }
    public string JsonPath { get; }

    public CacheEntryOutputWriter(string csv, string csvf, string json, string jsonf,
        string defaultFileName, string dateTimeFormat)
    {
        CsvPath = GetOutputPath(csv, csvf, defaultFileName);
        JsonPath = GetOutputPath(json, jsonf, Path.ChangeExtension(defaultFileName, ".json"));

        if (CsvPath != null && JsonPath != null &&
            string.Equals(Path.GetFullPath(CsvPath), Path.GetFullPath(JsonPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("CSV and JSON output files must have different paths.");
        }

        try
        {
            if (CsvPath != null)
            {
                Directory.CreateDirectory(csv);
                _csvStream = new StreamWriter(CsvPath);
                _csvWriter = new CsvWriter(_csvStream, CultureInfo.InvariantCulture);
                ConfigureCsv(dateTimeFormat);
                _csvWriter.WriteHeader<CacheEntry>();
                _csvWriter.NextRecord();
            }

            if (JsonPath != null)
            {
                Directory.CreateDirectory(json);
                _jsonStream = new StreamWriter(JsonPath);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static string GetOutputPath(string directory, string fileName, string defaultFileName)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        return Path.Combine(directory, string.IsNullOrEmpty(fileName) ? defaultFileName : Path.GetFileName(fileName));
    }

    private void ConfigureCsv(string dateTimeFormat)
    {
        var map = _csvWriter.Context.AutoMap<CacheEntry>();
        var options = new TypeConverterOptions
        {
            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
        };
        _csvWriter.Context.TypeConverterOptionsCache.AddOptions<CacheEntry>(options);

        map.Map(entry => entry.LastModifiedTimeUTC).Convert(entry => entry.Value.LastModifiedTimeUTC.HasValue
            ? entry.Value.LastModifiedTimeUTC.Value.ToString(dateTimeFormat) : "");

        map.Map(entry => entry.CacheEntrySize).Ignore();
        map.Map(entry => entry.Data).Ignore();
        map.Map(entry => entry.InsertFlags).Ignore();
        map.Map(entry => entry.DataSize).Ignore();
        map.Map(entry => entry.LastModifiedFILETIMEUTC).Ignore();
        map.Map(entry => entry.PathSize).Ignore();
        map.Map(entry => entry.Signature).Ignore();

        map.Map(entry => entry.ControlSet).Index(0);
        map.Map(entry => entry.CacheEntryPosition).Index(1);
        map.Map(entry => entry.Path).Index(2);
        map.Map(entry => entry.LastModifiedTimeUTC).Index(3);
        map.Map(entry => entry.Executed).Index(4);
        map.Map(entry => entry.Duplicate).Index(5);
        map.Map(entry => entry.SourceFile).Index(6);
    }

    public void WriteRecord(CacheEntry entry)
    {
        if (_csvWriter != null)
        {
            _csvWriter.WriteRecord(entry);
            _csvWriter.NextRecord();
        }

        if (_jsonStream != null)
        {
            var record = new Dictionary<string, object>
            {
                [nameof(entry.ControlSet)] = entry.ControlSet,
                [nameof(entry.CacheEntryPosition)] = entry.CacheEntryPosition,
                [nameof(entry.Path)] = entry.Path,
                [nameof(entry.LastModifiedTimeUTC)] = entry.LastModifiedTimeUTC?.ToUniversalTime()
                    .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture),
                [nameof(entry.Executed)] = entry.Executed.ToString(),
                [nameof(entry.Duplicate)] = entry.Duplicate,
                [nameof(entry.SourceFile)] = entry.SourceFile
            };

            // Keep absent timestamps explicit without changing global serializer settings.
            using (JsConfig.With(new Config { IncludeNullValuesInDictionaries = true }))
            {
                _jsonStream.WriteLine(record.ToJson());
            }
        }
    }

    public void Flush()
    {
        _csvWriter?.Flush();
        _csvStream?.Flush();
        _jsonStream?.Flush();
    }

    public void Dispose()
    {
        try
        {
            _csvWriter?.Dispose();
        }
        finally
        {
            try
            {
                _csvStream?.Dispose();
            }
            finally
            {
                _jsonStream?.Dispose();
            }
        }
    }
}
