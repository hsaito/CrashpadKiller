using System.Diagnostics;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Hosting;

namespace CrashpadKiller;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            return await ProcessArguments(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> ProcessArguments(string[] args)
    {
        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "oneshot" => RunOneshot(),
            "daemon" => RunDaemon(args),
            "service" => OperatingSystem.IsWindows() ? await RunAsService(args) : NonWindowsError("service"),
            "install" => OperatingSystem.IsWindows() ? InstallService() : NonWindowsError("install"),
            "uninstall" => OperatingSystem.IsWindows() ? UninstallService() : NonWindowsError("uninstall"),
            _ => ShowUsage()
        };
    }

    static int ShowUsage()
    {
        Console.WriteLine("CrashpadKiller - Process Termination Utility");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  CrashpadKiller oneshot                    - Run once and exit");
        Console.WriteLine("  CrashpadKiller daemon [interval]          - Run continuously (default: 60 seconds)");
        Console.WriteLine("  CrashpadKiller service [interval]         - Run as Windows service");
        Console.WriteLine("  CrashpadKiller install                    - Install as Windows service (requires admin)");
        Console.WriteLine("  CrashpadKiller uninstall                  - Uninstall Windows service (requires admin)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  CrashpadKiller oneshot                    - Kill processes once");
        Console.WriteLine("  CrashpadKiller daemon 30                  - Run continuously every 30 seconds");
        Console.WriteLine("  CrashpadKiller install                    - Install service to run automatically");
        return 1;
    }

    static int NonWindowsError(string command)
    {
        Console.WriteLine($"Error: The '{command}' command is only supported on Windows.");
        return 1;
    }

    static int RunOneshot()
    {
        try
        {
            Config.Targets = LoadTargetsFromConfig();
            Execute();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static int RunDaemon(string[] args)
    {
        try
        {
            int interval = 60; // default interval
            if (args.Length > 1 && int.TryParse(args[1], out var parsed))
                interval = parsed;

            if (interval <= 0)
            {
                Console.WriteLine("Error: Interval must be greater than 0");
                return 1;
            }

            Config.Targets = LoadTargetsFromConfig();
            ProcessLoop(interval);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    [SupportedOSPlatform("windows")]
    static async Task<int> RunAsService(string[] args)
    {
        try
        {
            int interval = 60; // default interval
            if (args.Length > 1 && int.TryParse(args[1], out var parsed))
                interval = parsed;

            if (interval <= 0)
            {
                Console.WriteLine("Error: Interval must be greater than 0");
                return 1;
            }

            var builder = Host.CreateDefaultBuilder()
                .UseWindowsService(options =>
                {
                    options.ServiceName = "CrashpadKiller";
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton(provider => new CrashpadKillerService(
                        provider.GetRequiredService<ILogger<CrashpadKillerService>>(),
                        interval));
                    services.AddHostedService<CrashpadKillerService>(provider => 
                        provider.GetRequiredService<CrashpadKillerService>());
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
                })
                .UseNLog();

            var host = builder.Build();
            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service error: {ex.Message}");
            return 1;
        }
    }

    [SupportedOSPlatform("windows")]
    static int InstallService()
    {
        try
        {
            if (ServiceInstaller.IsServiceInstalled())
            {
                Console.WriteLine("Service is already installed. Use 'uninstall' to remove it first.");
                return 1;
            }

            ServiceInstaller.InstallService();
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("Error: Administrator privileges are required to install a Windows service.");
            Console.WriteLine("Please run this command as Administrator.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Installation failed: {ex.Message}");
            return 1;
        }
    }

    [SupportedOSPlatform("windows")]
    static int UninstallService()
    {
        try
        {
            if (!ServiceInstaller.IsServiceInstalled())
            {
                Console.WriteLine("Service is not installed.");
                return 1;
            }

            ServiceInstaller.UninstallService();
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("Error: Administrator privileges are required to uninstall a Windows service.");
            Console.WriteLine("Please run this command as Administrator.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Uninstallation failed: {ex.Message}");
            return 1;
        }
    }

    // Legacy methods for oneshot and daemon modes
    static void ProcessLoop(int intervalSeconds)
    {
        if (intervalSeconds <= 0) return;
        Console.WriteLine($"Starting daemon mode with {intervalSeconds} second intervals. Press Ctrl+C to stop.");
        
        while (true)
        {
            Execute();
            Thread.Sleep(intervalSeconds * 1000);
        }
    }

    static List<string> LoadTargetsFromConfig()
    {
        try
        {
            using var configFile = new StreamReader("process.xml");
            var config = XDocument.Parse(configFile.ReadToEnd());
            var processTree = config.Element("config")?.Element("processes");
            var targetProcesses = processTree?.Elements("process");
            if (targetProcesses != null)
                return targetProcesses.Select(target => target.Value).ToList();
            throw new InvalidProcessConfigurationFileException("No process targets found in configuration.");
        }
        catch (Exception ex)
        {
            throw new InvalidProcessConfigurationFileException("Failed to load process configuration.", ex);
        }
    }

    static void Execute()
    {
        var logger = LogManager.GetCurrentClassLogger();

        logger.Info("Killing those pesky crashpads.");
        logger.Info("Targets are:");
        if (Config.Targets != null && Config.Targets.Count > 0)
        {
            foreach (var target in Config.Targets)
            {
                logger.Info(target);
            }

            var processes = Process.GetProcesses();
            var executionTargets = processes.Where(p => Config.Targets.Contains(p.ProcessName)).ToList();

            foreach (var proc in executionTargets)
            {
                try
                {
                    logger.Debug($"Attempting to kill {proc.ProcessName} (PID: {proc.Id})");
                    proc.Kill(false);
                }
                catch (Exception ex)
                {
                    logger.Warn($"Failed to kill {proc.ProcessName} (PID: {proc.Id}): {ex.Message}");
                }
            }
        }
        else
        {
            logger.Warn("No targets specified in configuration.");
        }

        logger.Info("Process complete.");
    }
}

internal static class Config
{
    internal static List<string> Targets { get; set; } = [];
}
