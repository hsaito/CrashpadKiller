using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public interface IProcess {
    string ProcessName { get; }
    int Id { get; }
    void Kill(bool entireProcessTree);
}

public class ProcessWrapper : IProcess {
    private readonly Process _process;
    public ProcessWrapper(Process process) { _process = process; }
    public string ProcessName => _process.ProcessName;
    public int Id => _process.Id;
    public void Kill(bool entireProcessTree) => _process.Kill(entireProcessTree);
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

public class ProcessKiller
{
    private readonly IProcessProvider _processProvider;
    private readonly IFileProvider _fileProvider;
    private readonly ILogger _logger;

    public ProcessKiller(IProcessProvider processProvider, IFileProvider fileProvider, ILogger logger)
    {
        _processProvider = processProvider;
        _fileProvider = fileProvider;
        _logger = logger;
    }

    public List<string> LoadTargetsFromConfig(string configPath)
    {
        try
        {
            var xml = _fileProvider.ReadAllText(configPath);
            var config = XDocument.Parse(xml);
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

    public void KillProcesses(List<string> targets)
    {
        _logger.Info("Killing those pesky crashpads.");
        _logger.Info("Targets are:");
        if (targets != null && targets.Count > 0)
        {
            foreach (var target in targets)
            {
                _logger.Info(target);
            }
            var processes = _processProvider.GetProcesses();
            var executionTargets = processes.Where(p => targets.Contains(p.ProcessName)).ToList();
            foreach (var proc in executionTargets)
            {
                try
                {
                    _logger.Debug($"Attempting to kill {proc.ProcessName} (PID: {proc.Id})");
                    proc.Kill(false);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to kill {proc.ProcessName} (PID: {proc.Id}): {ex.Message}");
                }
            }
        }
        else
        {
            _logger.Warn("No targets specified in configuration.");
        }
        _logger.Info("Process complete.");
    }
}

public class InvalidProcessConfigurationFileException : Exception
{
    public InvalidProcessConfigurationFileException(string? message = null, Exception? inner = null)
        : base(message, inner)
    {
    }
}
