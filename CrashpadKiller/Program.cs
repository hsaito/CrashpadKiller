using System.CommandLine;
using System.Diagnostics;
using System.Xml.Linq;
using NLog;
using System.Runtime.InteropServices;

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
daemonCommand.SetHandler((int interval) => ProcessLoop(interval), intervalOption);

oneshotCommand.SetHandler(Execute);

registerCommand.AddOption(intervalOption);
registerCommand.SetHandler((int interval) => RegisterService(interval), intervalOption);
unregisterCommand.SetHandler(UnregisterService);

rootCommand.Add(oneshotCommand);
rootCommand.Add(daemonCommand);
rootCommand.Add(registerCommand);
rootCommand.Add(unregisterCommand);

Config.Targets = LoadTargetsFromConfig();

return await rootCommand.InvokeAsync(args);

void ProcessLoop(int intervalSeconds)
{
    if (intervalSeconds <= 0) return;
    while (true)
    {
        Execute();
        Thread.Sleep(intervalSeconds * 1000);
    }
}

List<string> LoadTargetsFromConfig()
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

void Execute()
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

void RegisterService(int interval)
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

void UnregisterService()
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