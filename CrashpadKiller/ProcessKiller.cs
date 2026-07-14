using System.Diagnostics;
using System.Xml.Linq;

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
        try
        {
            var xml = fileProvider.ReadAllText(configPath);
            var config = XDocument.Parse(xml);
            var processTree = config.Element("config")?.Element("processes");
            var targetProcesses = processTree?.Elements("process");
            if (targetProcesses != null)
                return [.. targetProcesses.Select(target => target.Value)];
            throw new InvalidProcessConfigurationFileException("No process targets found in configuration.");
        }
        catch (Exception ex)
        {
            throw new InvalidProcessConfigurationFileException("Failed to load process configuration.", ex);
        }
    }

    public void KillProcesses(List<string> targets)
    {
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

public class InvalidProcessConfigurationFileException(string? message = null, Exception? inner = null)
    : Exception(message, inner);