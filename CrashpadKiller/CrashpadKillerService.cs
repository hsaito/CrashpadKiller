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
        try
        {
            var xml = _fileProvider.ReadAllText("process.xml");
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

    private void Execute()
    {
        _processKiller.KillProcesses(_targets);
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