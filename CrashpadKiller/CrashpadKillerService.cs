using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Xml.Linq;

namespace CrashpadKiller;

public class CrashpadKillerService : BackgroundService
{
    private readonly ILogger<CrashpadKillerService> _logger;
    private readonly int _intervalSeconds;
    private readonly ProcessKiller _processKiller;
    private readonly IFileProvider _fileProvider;
    private List<string> _targets = [];

    public CrashpadKillerService(ILogger<CrashpadKillerService> logger, int intervalSeconds = 60)
    {
        _logger = logger;
        _intervalSeconds = intervalSeconds;
        _fileProvider = new FileProvider();
        _processKiller = new ProcessKiller(new ProcessProvider(), _fileProvider, new ServiceLogger(_logger));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        EnsureEventLogSource();
        string workingDir = Environment.CurrentDirectory;
        _logger.LogInformation($"CrashpadKiller service starting. Working directory: {workingDir}");
        try
        {
            _targets = LoadTargetsFromConfig();
            _logger.LogInformation("CrashpadKiller service starting with {Count} targets", _targets.Count);
            foreach (var target in _targets)
            {
                _logger.LogInformation("Target: {Target}", target);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration. Service will not start.");
            if (OperatingSystem.IsWindows())
                LogToWindowsEvent("CrashpadKiller failed to start: " + ex.Message, EventLogEntryType.Error);
            throw;
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Execute();
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during process execution");
                // Continue running even if one iteration fails
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CrashpadKiller service stopping");
        await base.StopAsync(cancellationToken);
    }

    private List<string> LoadTargetsFromConfig()
    {
        string configPath = Environment.GetEnvironmentVariable("CRASHPADKILLER_CONFIG_PATH") ?? "process.xml";
        string attemptedPath = configPath;
        if (!File.Exists(configPath))
        {
            // Try next to the executable
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string exeConfigPath = Path.Combine(exeDir, configPath);
            if (File.Exists(exeConfigPath))
            {
                configPath = exeConfigPath;
            }
        }
        try
        {
            if (!File.Exists(configPath))
            {
                string msg = $"Process configuration file not found. Attempted paths: {attemptedPath}, {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, attemptedPath)}. Working directory: {Environment.CurrentDirectory}";
                _logger.LogError(msg);
                if (OperatingSystem.IsWindows())
                    LogToWindowsEvent(msg, EventLogEntryType.Error);
                throw new InvalidProcessConfigurationFileException(msg);
            }
            var xml = _fileProvider.ReadAllText(configPath);
            var config = XDocument.Parse(xml);
            var processTree = config.Element("config")?.Element("processes");
            var targetProcesses = processTree?.Elements("process");
            if (targetProcesses != null)
                return targetProcesses.Select(target => target.Value).ToList();
            string msg2 = $"No process targets found in configuration. Path: {configPath}, XML: {xml}";
            _logger.LogError(msg2);
            if (OperatingSystem.IsWindows())
                LogToWindowsEvent(msg2, EventLogEntryType.Error);
            throw new InvalidProcessConfigurationFileException(msg2);
        }
        catch (Exception ex)
        {
            string detailedMsg = $"Failed to load process configuration from {configPath}. Exception: {ex.Message}\nStackTrace: {ex.StackTrace}\nInnerException: {ex.InnerException?.Message}";
            _logger.LogError(ex, detailedMsg);
            if (OperatingSystem.IsWindows())
                LogToWindowsEvent(detailedMsg, EventLogEntryType.Error);
            throw new InvalidProcessConfigurationFileException(detailedMsg, ex);
        }
    }

    private void Execute()
    {
        _processKiller.KillProcesses(_targets);
    }

    private void EnsureEventLogSource()
    {
        if (!OperatingSystem.IsWindows())
            return;
        const string source = "CrashpadKiller";
        const string logName = "Application";
        if (!EventLog.SourceExists(source))
        {
            EventLog.CreateEventSource(source, logName);
        }
    }

    private void LogToWindowsEvent(string message, EventLogEntryType type)
    {
        if (!OperatingSystem.IsWindows())
            return;
        const string source = "CrashpadKiller";
        using (var eventLog = new EventLog("Application"))
        {
            eventLog.Source = source;
            eventLog.WriteEntry(message, type);
        }
    }
}

public class FileProvider : IFileProvider
{
    public string ReadAllText(string path) => File.ReadAllText(path);
}

public class ServiceLogger : ILogger
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public ServiceLogger(Microsoft.Extensions.Logging.ILogger logger)
    {
        _logger = logger;
    }

    public void Info(string message) => _logger.LogInformation(message);
    public void Debug(string message) => _logger.LogDebug(message);
    public void Warn(string message) => _logger.LogWarning(message);
}