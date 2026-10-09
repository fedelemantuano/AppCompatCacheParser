# AppCompatCacheParser

## Command Line Interface

    AppCompatCache Parser version 1.4.4.0
    
    Author: Eric Zimmerman (saericzimmerman@gmail.com)
    https://github.com/EricZimmerman/AppCompatCacheParser
    
            c               The ControlSet to parse. Default is to extract all control sets.
            f               Full path to SYSTEM hive to process. If this option is not specified, the live Registry will be used
            t               Sorts last modified timestamps in descending order
    
            csv             Directory to save CSV formatted results to. This or --json required
            csvf            File name to save CSV formatted results to. When present, overrides default name
            json            Directory to save JSON Lines formatted results to. This or --csv required
            jsonf           File name to save JSON Lines formatted results to. When present, overrides default name
    
            debug           Debug mode
            dt              The custom date/time format to use when displaying timestamps. See https://goo.gl/CNVq0k for options. Default is: yyyy-MM-dd HH:mm:ss
            nl              When true, ignore transaction log files for dirty hives. Default is FALSE
    
    Examples: AppCompatCacheParser.exe --csv c:\temp -t -c 2
              AppCompatCacheParser.exe --csv c:\temp --csvf results.csv
              AppCompatCacheParser.exe -f C:\temp\SYSTEM --json C:\temp --jsonf results.json
              AppCompatCacheParser.exe -f C:\temp\SYSTEM --csv C:\temp --json C:\temp
    
              Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes

## JSON Lines output

`--json` exports one compact JSON object per cache entry, one object per line, to a `.json` file. It can be used alone or together with `--csv`. Field names match the CSV columns; numbers and booleans retain their JSON types, and `Executed` uses the CSV labels (`Yes`, `No`, `NA`).

```json
{"ControlSet":1,"CacheEntryPosition":0,"Path":"C:\\Windows\\System32\\cmd.exe","LastModifiedTimeUTC":"2026-10-09T10:20:30.1234567Z","Executed":"Yes","Duplicate":false,"SourceFile":"SYSTEM"}
```

JSON timestamps always use ISO 8601 in UTC (`Z`) with seven fractional-second digits. A missing timestamp is `null`. `--dt` continues to control CSV formatting and does not affect JSON.

Without `--jsonf`, JSON uses the same automatic naming scheme as CSV with a `.json` extension. `--csvf` and `--jsonf` are independent. Missing output directories are created, existing JSON files are replaced, and a successful parse with no entries produces an empty JSON file. CSV and JSON outputs must have different file paths.

These timestamps are suitable for Elasticsearch date fields. Elasticsearch [`date`](https://www.elastic.co/docs/reference/elasticsearch/mapping-reference/date) indexes timestamps at millisecond resolution; [`date_nanos`](https://www.elastic.co/docs/reference/elasticsearch/mapping-reference/date_nanos) supports finer precision but has a more limited date range. The JSON file preserves all seven fractional-second digits regardless of the mapping selected by the consumer.

Run the output regression tests with:

```shell
dotnet test AppCompatCacheParserOutputTest/AppCompatCacheParserOutputTest.csproj --configuration Release
```

## Documentation

AppCompatCache (ShimCache) parser. Supports Windows XP, Windows 7 (x86 and x64), Windows 8.x, Windows 10, and Windows 11.

[Introducing AppCompatCacheParser](https://binaryforay.blogspot.com/2015/05/introducing-appcompatcacheparser.html)

[AppCompatCacheParser v0.0.5.1 released](https://binaryforay.blogspot.com/2015/05/appcompatcacheparser-v0051-released.html)

[AppCompatCacheParser v0.0.5.2 released](https://binaryforay.blogspot.com/2015/05/appcompatcacheparser-v0052-released.html)

[AppCompatCacheParser v0.9.0.0 released and some AppCompatCache/shimcache parser testing](https://binaryforay.blogspot.com/2016/05/appcompatcacheparser-v0900-released-and.html)

[Windows 10 Creators update vs shimcache parsers: Fight!!](https://binaryforay.blogspot.com/2017/03/windows-10-creators-update-vs-shimcache.html)

[Updates to the left of me, updates to the right of me, version 1 releases are here (for the most part)](https://binaryforay.blogspot.com/2018/03/updates-to-left-of-me-updates-to-right.html)

[Everything gets an update, Sept 2018 edition](https://binaryforay.blogspot.com/2018/09/everything-gets-update-sept-2018-edition.html)

[Locked file support added to AmcacheParser, AppCompatCacheParser, MFTECmd, ShellBags Explorer (and SBECmd), and Registry Explorer (and RECmd)](https://binaryforay.blogspot.com/2019/01/locked-file-support-added-to.html)

[Windows Registry Knowledge Base](https://github.com/libyal/winreg-kb/wiki/Application-Compatibility-Cache-key)

# Download Eric Zimmerman's Tools

All of Eric Zimmerman's tools can be downloaded [here](https://ericzimmerman.github.io/#!index.md). 

# Special Thanks

Open Source Development funding and support provided by the following contributors: [SANS Institute](http://sans.org/) and [SANS DFIR](http://dfir.sans.org/).
