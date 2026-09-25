using System.Diagnostics;
using System.Xml.Linq;
using OpenTelemetry.Trace;

namespace CrashpadKiller;

public interface IProcess {
    string ProcessName { get; }
    int Id { get; }
    void Kill(bool entireProcessTree);
}

public class ProcessWrapper(Process process) : IProcess
{
    public string ProcessName => process.ProcessName;
    public int Id => process.Id;
    public void Kill(bool entireProcessTree) => process.Kill(entireProcessTree);
}

public interface IProcessProvider {
    IEnumerable<IProcess> GetProcesses();
}

public class ProcessProvider : IProcessProvider {
    public IEnumerable<IProcess> GetProcesses() => Process.GetProcesses().Select(p => new ProcessWrapper(p));
}

public interface IFileProvider
{
    string ReadAllText(string path);
}

public interface ILogger
{
    void Info(string message);
    void Debug(string message);
    void Warn(string message);
}

public class ProcessKiller(IProcessProvider processProvider, IFileProvider fileProvider, ILogger logger)
{
    public List<string> LoadTargetsFromConfig(string configPath)
    {
        var stopwatch = Stopwatch.StartNew();
        Telemetry.RecordConfigLoadAttempt();
        using var activity = Telemetry.StartActivity("load process configuration");
        activity?.SetTag("crashpadkiller.config.path", configPath);

        try
        {
            var xml = fileProvider.ReadAllText(configPath);
            var config = XDocument.Parse(xml);
            var processTree = config.Element("config")?.Element("processes");
            var targetProcesses = processTree?.Elements("process");
            if (targetProcesses != null)
            {
                var targets = targetProcesses.Select(target => target.Value).ToList();
                activity?.SetTag("crashpadkiller.process.count", targets.Count);
                Telemetry.RecordConfigLoadSuccess(stopwatch.Elapsed);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return targets;
            }
            throw new InvalidProcessConfigurationFileException("No process targets found in configuration.");
        }
        catch (Exception ex)
        {
            Telemetry.RecordConfigLoadFailure(stopwatch.Elapsed);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw new InvalidProcessConfigurationFileException("Failed to load process configuration.", ex);
        }
    }

    public void KillProcesses(List<string> targets)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = Telemetry.StartActivity("kill processes");
        activity?.SetTag("crashpadkiller.target.count", targets?.Count ?? 0);

        logger.Info("Killing those pesky crashpads.");
        logger.Info("Targets are:");
        if (targets is { Count: > 0 })
        {
            foreach (var target in targets)
            {
                logger.Info(target);
            }
            var processes = processProvider.GetProcesses();
            var executionTargets = processes.Where(p => targets.Contains(p.ProcessName)).ToList();
            activity?.SetTag("crashpadkiller.match.count", executionTargets.Count);
            foreach (var proc in executionTargets)
            {
                Telemetry.RecordProcessKillAttempt();
                try
                {
                    logger.Debug($"Attempting to kill {proc.ProcessName} (PID: {proc.Id})");
                    proc.Kill(false);
                }
                catch (Exception ex)
                {
                    Telemetry.RecordProcessKillFailure();
                    logger.Warn($"Failed to kill {proc.ProcessName} (PID: {proc.Id}): {ex.Message}");
                    activity?.AddException(ex);
                }
            }
        }
        else
        {
            logger.Warn("No targets specified in configuration.");
        }
        Telemetry.RecordProcessKillDuration(stopwatch.Elapsed);
        activity?.SetStatus(ActivityStatusCode.Ok);
        logger.Info("Process complete.");
    }
}

public class InvalidProcessConfigurationFileException(string? message = null, Exception? inner = null)
    : Exception(message, inner);