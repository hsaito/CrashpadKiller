using System.CommandLine;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Xml;
using System.Xml.Linq;
using NLog;

var intervalOption = new Option<int>(
    name: "--interval",
    description: "Execution interval in seconds.")
{
    IsRequired = true
};

var rootCommand = new RootCommand("CrashpadKiller");

var oneshotCommand = new Command("oneshot", "Run in an oneshot mode.");
var daemonCommand = new Command("daemon", "Run in a daemon mode.");

daemonCommand.AddOption(intervalOption);
daemonCommand.SetHandler(ProcessLoop, intervalOption);

oneshotCommand.SetHandler(Execute);

rootCommand.Add(oneshotCommand);
rootCommand.Add(daemonCommand);

Config.Targets = ListTargets();

return await rootCommand.InvokeAsync(args);

void ProcessLoop(int delay)
{
    if (delay > 0)
    {
        while (true)
        {
            Execute();
            Thread.Sleep(delay * 1000);
        }
    }
}

List<string> ListTargets()
{
    var configFile = new StreamReader("process.xml");
    var config = XDocument.Parse(configFile.ReadToEnd());
    var processTree = config.Element("config").Element("processes");
    var targetProcesses = processTree.Elements("process");

    configFile.Close();
    return targetProcesses.Select(target => target.Value).ToList();
}

void Execute()
{
    var logger = LogManager.GetCurrentClassLogger();

    logger.Info("Killing those pesky crashpads.");
    logger.Info("Targets are:");
    foreach (var targetItem in Config.Targets)
    {
        logger.Info(targetItem);
    }

    var processes = Process.GetProcesses();
    var executionTarget = processes.Where(process => Config.Targets.Contains(process.ProcessName)).ToList();

    foreach (var item in executionTarget)
    {
        logger.Debug($"Attempting to kill {item.Id}");
        item.Kill(false);
    }

    logger.Info("Process complete.");
    LogManager.Shutdown();
}

internal static class Config
{
    internal static List<string> Targets { get; set; }
}