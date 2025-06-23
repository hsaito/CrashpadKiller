using System.CommandLine;
using System.Diagnostics;
using System.Xml.Linq;
using NLog;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            if (args.Length == 0 && OperatingSystem.IsWindows())
            {
                int interval = 60; // Default interval
                if (Config.Targets == null)
                    Config.Targets = LoadTargetsFromConfig();
                var host = Host.CreateDefaultBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddHostedService(_ => new CrashpadKillerWorker(interval));
                    })
                    .UseWindowsService()
                    .Build();
                await host.RunAsync();
                return;
            }

            var intervalOption = new Option<int>(
                name: "--interval",
                description: "Execution interval in seconds.")
            {
                IsRequired = true
            };

            var rootCommand = new RootCommand("CrashpadKiller");

            var oneshotCommand = new Command("oneshot", "Run in an oneshot mode.");
            var daemonCommand = new Command("daemon", "Run in a daemon mode.");
            var registerCommand = new Command("register", "Register as a Windows service.");
            var unregisterCommand = new Command("unregister", "Unregister the Windows service.");

            daemonCommand.AddOption(intervalOption);
            daemonCommand.SetHandler(ProcessLoop, intervalOption);

            oneshotCommand.SetHandler(Execute);

            registerCommand.AddOption(intervalOption);
            registerCommand.SetHandler(RegisterService, intervalOption);
            unregisterCommand.SetHandler(UnregisterService);

            rootCommand.Add(oneshotCommand);
            rootCommand.Add(daemonCommand);
            rootCommand.Add(registerCommand);
            rootCommand.Add(unregisterCommand);

            Config.Targets = LoadTargetsFromConfig();

            await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            var logger = LogManager.GetCurrentClassLogger();
            logger.Fatal(ex, "Fatal error in Main");
            throw;
        }
    }

    private static void ProcessLoop(int intervalSeconds)
    {
        if (intervalSeconds <= 0) return;
        while (true)
        {
            Execute();
            Thread.Sleep(intervalSeconds * 1000);
        }
    }

    private static List<string> LoadTargetsFromConfig()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var configPath = Path.Combine(baseDir, "process.xml");
            var logger = LogManager.GetCurrentClassLogger();
            logger.Info($"Looking for process.xml at: {configPath}");
            if (!File.Exists(configPath))
            {
                logger.Error($"Config file not found: {configPath}");
                throw new InvalidProcessConfigurationFileException($"Config file not found: {configPath}");
            }
            using var configFile = new StreamReader(configPath);
            var config = XDocument.Parse(configFile.ReadToEnd());
            var processTree = config.Element("config")?.Element("processes");
            var targetProcesses = processTree?.Elements("process");
            if (targetProcesses != null)
                return targetProcesses.Select(target => target.Value).ToList();
            throw new InvalidProcessConfigurationFileException("No process targets found in configuration.");
        }
        catch (Exception ex)
        {
            var logger = LogManager.GetCurrentClassLogger();
            logger.Error(ex, "Failed to load process configuration.");
            throw new InvalidProcessConfigurationFileException("Failed to load process configuration.", ex);
        }
    }

    private static void Execute()
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
        LogManager.Shutdown();
    }

    private static void RegisterService(int interval)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Service registration is only supported on Windows.");
            return;
        }
        if (interval <= 0)
        {
            Console.WriteLine("Interval must be greater than 0.");
            return;
        }
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath))
        {
            Console.WriteLine("Failed to get executable path.");
            return;
        }
        const string serviceName = "CrashpadKiller";
        const string displayName = "CrashpadKiller Service";
        var args = $"create {serviceName} binPath= \"{exePath} daemon --interval {interval}\" DisplayName= \"{displayName}\" start= auto";
        var psi = new ProcessStartInfo("sc.exe", args) { RedirectStandardOutput = true, UseShellExecute = false };
        var proc = Process.Start(psi);
        proc?.WaitForExit();
        Console.WriteLine(proc?.StandardOutput.ReadToEnd());
    }

    private static void UnregisterService()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Service unregistration is only supported on Windows.");
            return;
        }
        const string serviceName = "CrashpadKiller";
        var args = $"delete {serviceName}";
        var psi = new ProcessStartInfo("sc.exe", args) { RedirectStandardOutput = true, UseShellExecute = false };
        var proc = Process.Start(psi);
        proc?.WaitForExit();
        Console.WriteLine(proc?.StandardOutput.ReadToEnd());
    }

    internal static class Config
    {
        internal static List<string>? Targets { get; set; }
    }

    public class InvalidProcessConfigurationFileException : Exception
    {
        public InvalidProcessConfigurationFileException(string? message = null, Exception? inner = null)
            : base(message, inner)
        {
        }
    }

    // Worker class for Windows Service/daemon support
    public class CrashpadKillerWorker : BackgroundService
    {
        private readonly int _intervalSeconds;
        public CrashpadKillerWorker(int intervalSeconds)
        {
            _intervalSeconds = intervalSeconds > 0 ? intervalSeconds : 60;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var logger = LogManager.GetCurrentClassLogger();
            logger.Info($"CrashpadKillerWorker started. Interval: {_intervalSeconds}s");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Execute();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Exception in CrashpadKillerWorker loop");
                }
                await Task.Delay(_intervalSeconds * 1000, stoppingToken);
            }
            logger.Info("CrashpadKillerWorker stopped.");
        }
    }
}
