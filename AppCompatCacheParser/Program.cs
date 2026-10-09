using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Exceptionless;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServiceStack;
using ServiceStack.Text;

namespace AppCompatCacheParser;

internal class Program
{
    //private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    private static string[] _args;
        
    private static readonly string Header =
        $"AppCompatCache Parser version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}" +
        $"\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
        $"\r\nhttps://github.com/EricZimmerman/AppCompatCacheParser";

    private static readonly string Footer = @"Examples: AppCompatCacheParser.exe --csv c:\temp -t -c 2" + "\r\n\t " +
                                            @"   AppCompatCacheParser.exe --csv c:\temp --csvf results.csv" + "\r\n\t " +
                                            @"   AppCompatCacheParser.exe -f C:\temp\SYSTEM --json C:\temp --jsonf results.json" + "\r\n\t " +
                                            @"   AppCompatCacheParser.exe -f C:\temp\SYSTEM --csv C:\temp --json C:\temp" + "\r\n\t " +
                                            "\r\n\t" +
                                            "    Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

    private static RootCommand _rootCommand;


    
    private static bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }
            
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }


    private static async Task Main(string[] args)
    {
        ExceptionlessClient.Default.Startup("7iL4b0Me7W8PbFflftqWgfQCIdf55flrT2O11zIP");
    
        _args = args;
   
        var fOpt = new Option<string>("-f")
        {
            Description = "Full path to SYSTEM hive to process. If this option is not specified, the live Registry will be used"
        };
        
        var csvOpt = new Option<string>(
            "--csv")
        {
            Description = "Directory to save CSV formatted results to. This or --json is required. Be sure to include the full path in double quotes"
        
        };

        var csvfOpt = new Option<string>(
            "--csvf")
        {
            Description   = "File name to save CSV formatted results to. When present, overrides default name\r\n"
        };

        var jsonOpt = new Option<string>("--json")
        {
            Description = "Directory to save JSON Lines formatted results to. This or --csv is required. Be sure to include the full path in double quotes"
        };

        var jsonfOpt = new Option<string>("--jsonf")
        {
            Description = "File name to save JSON Lines formatted results to. When present, overrides default name"
        };
        
          
        var cOpt = new Option<int>("-c")
        {
            Description = "The ControlSet to parse. Default is to extract all control sets",
            DefaultValueFactory = _ => -1
        };
          
        var tOpt = new Option<bool>("-t")
        {
            Description = "Sorts last modified timestamps in descending order",
            DefaultValueFactory = _ => false
        };
        
        var dtOpt = new Option<string>(
            "--dt"){
            Description =        "The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options",
            DefaultValueFactory = _ => "yyyy-MM-dd HH:mm:ss"
        };
        
        var nlOpt = new Option<bool>("--nl")
        {
            Description = "When true, ignore transaction log files for dirty hives",
            DefaultValueFactory = _ => false
        };
        
        
        var debugOpt = new Option<bool>("--debug")
        {
            Description = "Show debug information during processing",
            DefaultValueFactory = _ => false
        };
        var traceOpt = new Option<bool>("--trace")
        {
            Description = "Show trace information during processing",
            DefaultValueFactory = _ => false
        };
            
        _rootCommand = new RootCommand
        {
           fOpt,
           csvOpt,
           csvfOpt,
           jsonOpt,
           jsonfOpt,
           cOpt,
           tOpt,
          dtOpt,
          nlOpt,
          debugOpt,
          traceOpt
          };
            
        _rootCommand.Description = Header + "\r\n\r\n" + Footer;

        _rootCommand.SetAction(result => DoWork(result.GetValue(fOpt), result.GetValue(csvOpt), result.GetValue(csvfOpt),
            result.GetValue(jsonOpt), result.GetValue(jsonfOpt),
            result.GetValue(cOpt), result.GetValue(tOpt), result.GetValue(dtOpt), result.GetValue(nlOpt),
            result.GetValue(debugOpt),result.GetValue(traceOpt)));
            

        await _rootCommand.Parse(args).InvokeAsync();
        
        Log.CloseAndFlush();
    }

    private static void DoWork(string f,string csv,string csvf,string json,string jsonf,int c, bool t,string dt,bool nl,bool debug,bool trace)
    {
        var levelSwitch = new LoggingLevelSwitch();

        var template = "{Message:lj}{NewLine}{Exception}";

        if (debug)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Debug;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }

        if (trace)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Verbose;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }
        
        var conf = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: template)
            .MinimumLevel.ControlledBy(levelSwitch);
      
        Log.Logger = conf.CreateLogger();
        
        if (string.IsNullOrEmpty(csv) && string.IsNullOrEmpty(json))
        {
            var aaa = new CustomHelpAction(new HelpAction());
            aaa.Invoke(_rootCommand.Parse("--csv or --json is required. Exiting"));

            return;
        }

        
        var hiveToProcess = "Live Registry";
        
        if (f?.Length > 0)
        {
            hiveToProcess = f;
            if (!File.Exists(f))
            {
                Log.Warning("'{F}' not found. Exiting",f);
                return;
            }
        }
        else
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.Fatal("Live Registry support is not available on non-Windows systems");
                Environment.Exit(0);
                //throw new NotSupportedException("Live Registry support is not available on non-Windows systems");
            }
        }

        Log.Information("{Header}",Header);
        Console.WriteLine();
        Log.Information("Command line: {Args}",string.Join(" ", _args));
        Console.WriteLine();

        if (IsAdministrator() == false)
        {
            Log.Fatal($"Warning: Administrator privileges not found!");
            Console.WriteLine();
        }

        Log.Information("Processing hive '{HiveToProcess}'",hiveToProcess);

        Console.WriteLine();

        try
        {
            var appCompat = new AppCompatCache.AppCompatCache(f,
                c,nl);

            string outFileBase;
            var ts1 = DateTime.Now.ToString("yyyyMMddHHmmss");

            if (f?.Length > 0)
            {
                if (c >= 0)
                {
                    outFileBase =
                        $"{ts1}_{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(f)}_ControlSet00{c}_AppCompatCache.csv";
                }
                else
                {
                    outFileBase =
                        $"{ts1}_{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(f)}_AppCompatCache.csv";
                }
            }
            else
            {
                outFileBase = $"{ts1}_{appCompat.OperatingSystem}_{Environment.MachineName}_AppCompatCache.csv";
            }

            using var outputWriter = new CacheEntryOutputWriter(csv, csvf, json, jsonf, outFileBase, dt);

            Log.Debug("**** Found {Count} caches",appCompat.Caches.Count);

            var cacheKeys = new HashSet<string>();

            if (appCompat.Caches.Any())
            {
                foreach (var appCompatCach in appCompat.Caches)
                {
                  
                  Log.Verbose("Dumping cache details: {@Details}",appCompat);
                  
                    try
                    {
                        Log.Information(
                            "Found {Count:N0} cache entries for {OperatingSystem} in {ControlSet}",appCompatCach.Entries.Count,appCompat.OperatingSystem,$"ControlSet00{appCompatCach.ControlSet}");

                        foreach (var cacheEntry in appCompatCach.Entries)
                        {
                            cacheEntry.SourceFile = hiveToProcess;
                            cacheEntry.Duplicate = cacheKeys.Contains(cacheEntry.GetKey());

                            cacheKeys.Add(cacheEntry.GetKey());

                            outputWriter.WriteRecord(cacheEntry);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex,"There was an error: Error message: {Message}",ex.Message);

                        try
                        {
                            appCompatCach.PrintDump();
                        }
                        catch (Exception ex1)
                        {
                            Log.Error(ex1,"Couldn't PrintDump: {Message}",ex1.Message);
                        }
                    }
                }
                outputWriter.Flush();

                Console.WriteLine();
                if (outputWriter.CsvPath != null)
                {
                    Log.Warning("Results saved to '{OutFilename}'",outputWriter.CsvPath);
                }
                if (outputWriter.JsonPath != null)
                {
                    Log.Warning("Results saved to '{OutFilename}'",outputWriter.JsonPath);
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
                Log.Warning("No caches were found!");
                Console.WriteLine();
            }
                
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Sequence numbers do not match and transaction logs were not found in the same direct") == false)
            {
                if (ex.Message.Contains("Administrator privileges not found"))
                {
                    Log.Fatal("Could not access '{F}'. Does it exist?",f);
                    Console.WriteLine();
                    Log.Fatal("Rerun the program with Administrator privileges to try again");
                    Console.WriteLine();
                }

                else if (ex.Message.Contains("Could not find ControlSet"))
                {
                    Console.WriteLine();
                    Log.Fatal("{Msg}. Exiting",ex.Message);
                    Console.WriteLine();
                }
                else if (ex.Message.Contains("Invalid diskName:"))
                {
                    Console.WriteLine();
                    Log.Fatal("Could not access '{F}'. Invalid disk!",f);
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                    Log.Error(ex,"There was an error: {Message}",ex.Message);
                    Console.WriteLine();
                }

            }
                
        }
    }
    private class CustomHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction _defaultHelp;

        public CustomHelpAction(HelpAction action)
        {
            _defaultHelp = action;
        }

        public override int Invoke(ParseResult parseResult)
        {
            var result = _defaultHelp.Invoke(parseResult);

            Log.Warning("{Msg}", string.Join(" ",parseResult.Tokens));

            return result;
        }
    }
}
