using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AppCompatCache;
using AppCompatCacheParser;
using CsvHelper;
using NUnit.Framework;
using Execute = AppCompatCache.AppCompatCache.Execute;

namespace AppCompatCacheParserOutputTest;

[TestFixture]
public class CacheEntryOutputWriterTests
{
    private string _directory;
    private const string DefaultFileName = "20261009102030_Windows10_SYSTEM_AppCompatCache.csv";
    private const string DefaultDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "AppCompatCacheOutputTests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static CacheEntry Entry()
    {
        return new CacheEntry
        {
            ControlSet = 1,
            CacheEntryPosition = 3,
            Path = @"C:\temp\calc.exe",
            LastModifiedTimeUTC = new DateTimeOffset(2026, 10, 9, 10, 20, 30, TimeSpan.Zero).AddTicks(1234567),
            Executed = Execute.Yes,
            SourceFile = "SYSTEM"
        };
    }

    [TestCase(Execute.Yes)]
    [TestCase(Execute.No)]
    [TestCase(Execute.NA)]
    public void JsonHasExactlyCsvFieldsWithNativeTypesAndEscapesStrings(Execute executed)
    {
        var entry = Entry();
        entry.Executed = executed;
        entry.Duplicate = true;
        entry.Path = "C:\\temp\\è,\"file\"\n.exe";
        entry.SourceFile = "SYSTEM\r\ncopy";
        entry.Data = new byte[] { 1, 2, 3 };

        string path;
        using (var writer = new CacheEntryOutputWriter(null, null, _directory, null, DefaultFileName, DefaultDateTimeFormat))
        {
            path = writer.JsonPath;
            writer.WriteRecord(entry);
        }

        var lines = File.ReadAllLines(path);
        Assert.That(lines, Has.Length.EqualTo(1));
        using var document = JsonDocument.Parse(lines[0]);
        var root = document.RootElement;
        Assert.That(root.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[]
        {
            "ControlSet", "CacheEntryPosition", "Path", "LastModifiedTimeUTC", "Executed", "Duplicate", "SourceFile"
        }));
        Assert.That(root.GetProperty("ControlSet").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("CacheEntryPosition").GetInt32(), Is.EqualTo(3));
        Assert.That(root.GetProperty("Duplicate").GetBoolean(), Is.True);
        Assert.That(root.GetProperty("Executed").GetString(), Is.EqualTo(executed.ToString()));
        Assert.That(root.GetProperty("Path").GetString(), Is.EqualTo(entry.Path));
        Assert.That(root.GetProperty("SourceFile").GetString(), Is.EqualTo(entry.SourceFile));
        Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
        Assert.That(File.ReadAllBytes(path).Take(3).ToArray(), Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf }));
    }

    [TestCase("fr-FR")]
    [TestCase("ar-SA")]
    public void JsonTimestampIsUtcWithSevenFractionalDigitsRegardlessOfCultureAndDt(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var entry = Entry();
        entry.LastModifiedTimeUTC = entry.LastModifiedTimeUTC.Value.ToOffset(TimeSpan.FromHours(2));
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using (var writer = new CacheEntryOutputWriter(null, null, _directory, "out.json", DefaultFileName, "dd/MM/yyyy"))
            {
                writer.WriteRecord(entry);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, "out.json")));
        var timestamp = document.RootElement.GetProperty("LastModifiedTimeUTC").GetString();
        Assert.That(timestamp, Is.EqualTo("2026-10-09T10:20:30.1234567Z"));
        Assert.That(DateTimeOffset.ParseExact(timestamp, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).Ticks,
            Is.EqualTo(entry.LastModifiedTimeUTC.Value.UtcTicks));
    }

    [Test]
    public void JsonIncludesExplicitNullTimestampAndOneLinePerRecord()
    {
        var entry = Entry();
        entry.LastModifiedTimeUTC = null;
        using (var writer = new CacheEntryOutputWriter(null, null, _directory, "out.json", DefaultFileName, DefaultDateTimeFormat))
        {
            writer.WriteRecord(entry);
            writer.WriteRecord(Entry());
        }

        var lines = File.ReadAllLines(Path.Combine(_directory, "out.json"));
        Assert.That(lines, Has.Length.EqualTo(2));
        using var document = JsonDocument.Parse(lines[0]);
        Assert.That(document.RootElement.GetProperty("LastModifiedTimeUTC").ValueKind, Is.EqualTo(JsonValueKind.Null));
        using var second = JsonDocument.Parse(lines[1]);
        Assert.That(second.RootElement.GetProperty("LastModifiedTimeUTC").ValueKind, Is.EqualTo(JsonValueKind.String));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CsvRetainsExistingColumnsFormattingAndNullRepresentation(bool includeJson)
    {
        using (var writer = new CacheEntryOutputWriter(_directory, "out.csv", includeJson ? _directory : null,
                   "out.json", DefaultFileName, DefaultDateTimeFormat))
        {
            writer.WriteRecord(Entry());
            var missing = Entry();
            missing.LastModifiedTimeUTC = null;
            writer.WriteRecord(missing);
        }

        Assert.That(File.ReadAllText(Path.Combine(_directory, "out.csv")), Is.EqualTo(
            "ControlSet,CacheEntryPosition,Path,LastModifiedTimeUTC,Executed,Duplicate,SourceFile\r\n" +
            "1,3,C:\\temp\\calc.exe,2026-10-09 10:20:30,Yes,False,SYSTEM\r\n" +
            "1,3,C:\\temp\\calc.exe,,Yes,False,SYSTEM\r\n"));
    }

    [Test]
    public void CombinedOutputAppliesCustomDtOnlyToCsv()
    {
        using (var writer = new CacheEntryOutputWriter(_directory, "out.csv", _directory, "out.json",
                   DefaultFileName, "dd/MM/yyyy HH:mm:ss"))
        {
            writer.WriteRecord(Entry());
        }

        Assert.That(File.ReadAllText(Path.Combine(_directory, "out.csv")), Does.Contain("09/10/2026 10:20:30"));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, "out.json")));
        Assert.That(document.RootElement.GetProperty("LastModifiedTimeUTC").GetString(),
            Is.EqualTo("2026-10-09T10:20:30.1234567Z"));
    }

    [Test]
    public void DefaultJsonNameIsIndependentOfCustomCsvNameAndDirectoriesAreCreated()
    {
        var csvDirectory = Path.Combine(_directory, "csv");
        var jsonDirectory = Path.Combine(_directory, "json");
        using (var writer = new CacheEntryOutputWriter(csvDirectory, "custom.csv", jsonDirectory, null,
                   DefaultFileName, DefaultDateTimeFormat))
        {
            writer.WriteRecord(Entry());
        }

        Assert.That(File.Exists(Path.Combine(csvDirectory, "custom.csv")), Is.True);
        Assert.That(File.Exists(Path.Combine(jsonDirectory, Path.ChangeExtension(DefaultFileName, ".json"))), Is.True);
    }

    [Test]
    public void CustomJsonFilenameUsesBasenameAndIsIndependentOfCsvFilename()
    {
        using (var writer = new CacheEntryOutputWriter(_directory, "custom.csv", _directory,
                   Path.Combine("ignored", "custom.json"), DefaultFileName, DefaultDateTimeFormat))
        {
            writer.WriteRecord(Entry());
        }

        Assert.That(File.Exists(Path.Combine(_directory, "custom.csv")), Is.True);
        Assert.That(File.Exists(Path.Combine(_directory, "custom.json")), Is.True);
    }

    [Test]
    public void ExistingJsonIsTruncatedAndSuccessfulEmptyExportHasZeroBytes()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "out.json");
        File.WriteAllText(path, new string('x', 4096));
        using (var writer = new CacheEntryOutputWriter(null, null, _directory, "out.json", DefaultFileName, DefaultDateTimeFormat))
        {
            writer.WriteRecord(Entry());
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.That(document.RootElement.GetProperty("Path").GetString(), Is.EqualTo(Entry().Path));

        using (new CacheEntryOutputWriter(null, null, _directory, "out.json", DefaultFileName, DefaultDateTimeFormat))
        {
        }
        Assert.That(new FileInfo(path).Length, Is.Zero);
    }

    [Test]
    public void SameCsvAndJsonPathIsRejectedBeforeExistingFileIsChanged()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "out.json");
        File.WriteAllText(path, "existing content", Encoding.UTF8);
        Assert.Throws<ArgumentException>(() => new CacheEntryOutputWriter(_directory, "out.json", _directory,
            "out.json", DefaultFileName, DefaultDateTimeFormat));
        Assert.That(File.ReadAllText(path), Is.EqualTo("existing content"));
    }

    [TestCase("WinXPx86.bin")]
    [TestCase("Win2k8Standard.bin")]
    [TestCase("win7x86.bin")]
    [TestCase("win7x64.bin")]
    [TestCase("Win80.bin")]
    [TestCase("Win81.bin")]
    [TestCase("Win10.bin")]
    [TestCase("Win10Creators.bin")]
    public void ParsedFixtureProducesEquivalentCsvRowsAndJsonDocuments(string fileName)
    {
        var bytes = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles", fileName));
        IAppCompatCache cache = fileName switch
        {
            "WinXPx86.bin" => new WindowsXP(bytes, true, 1),
            "Win2k8Standard.bin" => new VistaWin2k3Win2k8(bytes, false, 1),
            "win7x86.bin" => new Windows7(bytes, true, 1),
            "win7x64.bin" => new Windows7(bytes, false, 1),
            "Win80.bin" => new Windows8x(bytes, AppCompatCache.AppCompatCache.OperatingSystemVersion.Windows80_Windows2012, 1),
            "Win81.bin" => new Windows8x(bytes, AppCompatCache.AppCompatCache.OperatingSystemVersion.Windows81_Windows2012R2, 1),
            _ => new Windows10(bytes, 1)
        };
        Assert.That(cache.Entries, Is.Not.Empty);

        using (var writer = new CacheEntryOutputWriter(_directory, "out.csv", _directory, "out.json", DefaultFileName, "O"))
        {
            foreach (var entry in cache.Entries)
            {
                entry.SourceFile = fileName;
                writer.WriteRecord(entry);
            }
        }

        var lines = File.ReadAllLines(Path.Combine(_directory, "out.json"));
        Assert.That(lines, Has.Length.EqualTo(cache.Entries.Count));
        using var stream = new StreamReader(Path.Combine(_directory, "out.csv"));
        using var csv = new CsvReader(stream, CultureInfo.InvariantCulture);
        Assert.That(csv.Read(), Is.True);
        csv.ReadHeader();
        foreach (var line in lines)
        {
            Assert.That(csv.Read(), Is.True);
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            Assert.That(root.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(csv.HeaderRecord));
            Assert.That(root.GetProperty("ControlSet").GetInt32(), Is.EqualTo(csv.GetField<int>("ControlSet")));
            Assert.That(root.GetProperty("CacheEntryPosition").GetInt32(), Is.EqualTo(csv.GetField<int>("CacheEntryPosition")));
            Assert.That(root.GetProperty("Duplicate").GetBoolean(), Is.EqualTo(csv.GetField<bool>("Duplicate")));
            foreach (var field in new[] { "Path", "Executed", "SourceFile" })
            {
                Assert.That(root.GetProperty(field).GetString(), Is.EqualTo(csv.GetField(field)));
            }
            var csvDate = csv.GetField("LastModifiedTimeUTC");
            var jsonDate = root.GetProperty("LastModifiedTimeUTC");
            if (string.IsNullOrEmpty(csvDate))
            {
                Assert.That(jsonDate.ValueKind, Is.EqualTo(JsonValueKind.Null));
            }
            else
            {
                Assert.That(jsonDate.GetString(), Does.EndWith("Z"));
                Assert.That(DateTimeOffset.Parse(jsonDate.GetString(), CultureInfo.InvariantCulture),
                    Is.EqualTo(DateTimeOffset.Parse(csvDate, CultureInfo.InvariantCulture)));
            }
        }
        Assert.That(csv.Read(), Is.False);
    }
}
