using System.CommandLine;
using System.Diagnostics;
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

void Execute()
{
    var logger = LogManager.GetCurrentClassLogger();

    logger.Info("Killing those pesky crashpads.");

    var processes = Process.GetProcesses();

    var targets = processes.Where(q => q.ProcessName == "crashpad_handler");

    foreach (var item in targets)
    {
        logger.Debug($"Attempting to kill {item.Id}");
        item.Kill(false);
    }

    logger.Info("Process complete.");
}