# CrashpadKiller AI Coding Instructions

## Project Overview
CrashpadKiller is a .NET 9 Windows service/utility that automatically terminates specified processes (like crashpad handlers) based on XML configuration. It supports multiple execution modes: one-shot, daemon, and Windows service.

## Architecture & Key Components

### Core Entry Points
- [Program.cs](CrashpadKiller/Program.cs): Main CLI entry point with command switching (`oneshot`, `daemon`, `service`, `install`, `uninstall`)
- [CrashpadKillerService.cs](CrashpadKiller/CrashpadKillerService.cs): Background service using `Microsoft.Extensions.Hosting.BackgroundService`
- [ProcessKiller.cs](CrashpadKiller/ProcessKiller.cs): Testable core logic with DI interfaces (`IProcessProvider`, `IFileProvider`, `ILogger`)
- [ServiceInstaller.cs](CrashpadKiller/ServiceInstaller.cs): Windows service management using `sc.exe` commands (decorated with `[SupportedOSPlatform("windows")]`)

### Cross-Platform Handling Pattern
**Critical**: All service-related features use `OperatingSystem.IsWindows()` checks. Service commands (`install`, `uninstall`, `service`) fail gracefully on non-Windows platforms with clear error messages. This pattern appears in [Program.cs#L37-L40](CrashpadKiller/Program.cs#L37-L40) and must be preserved for any Windows-specific functionality.

### Configuration Architecture
- [process.xml](CrashpadKiller/process.xml): Defines target processes and execution interval (default: 600 seconds)
- [nlog.config](CrashpadKiller/nlog.config): Logging to console + Windows Event Log (Application source: "CrashpadKiller")
- **Config override**: Environment variable `CRASHPADKILLER_CONFIG_PATH` can override default `process.xml` path
- **File resolution**: Uses `AppDomain.CurrentDomain.BaseDirectory` for config files in service mode
- **XML parsing**: Uses `XDocument.Parse()` with fallback defaults; interval must be > 0

### Version Management
Version is centralized in [Directory.Build.props](Directory.Build.props) as `CrashpadKillerVersion` property (currently 1.0.5.0). All projects inherit this via MSBuild.

## Development Patterns

### Dependency Injection Strategy
**Hybrid approach** for backward compatibility:
- **CLI modes** (`oneshot`, `daemon`): Direct instantiation in [Program.cs](CrashpadKiller/Program.cs)
- **Service mode**: Full DI container with `Host.CreateDefaultBuilder()` in [Program.cs#L142-L161](CrashpadKiller/Program.cs#L142-L161)
- **Testing**: Mock-friendly interfaces (`IProcessProvider`, `IFileProvider`, `ILogger`) in [ProcessKiller.cs#L11-L39](CrashpadKiller/ProcessKiller.cs#L11-L39)

### Error Handling Conventions
- **Custom exceptions**: `InvalidProcessConfigurationFileException` for config errors
- **Graceful degradation**: Service continues running even if individual process kills fail (see [CrashpadKillerService.cs#L52-L65](CrashpadKillerService.cs#L52-L65))
- **Admin privilege checks**: `ServiceInstaller.IsRunningAsAdministrator()` before service install/uninstall
- **Logging on failure**: Always log to both console and Windows Event Log (when on Windows)

### Logging Architecture
- **Primary**: NLog with targets defined in [nlog.config](CrashpadKiller/nlog.config)
- **Service integration**: `Microsoft.Extensions.Logging` via `UseNLog()` in [Program.cs#L159](CrashpadKiller/Program.cs#L159)
- **Adapter pattern**: `ServiceLogger` class bridges `ILogger` (custom) to `Microsoft.Extensions.Logging.ILogger`
- **Event Log source**: "CrashpadKiller" must be registered; service handles this in `EnsureEventLogSource()`

## Key Development Workflows

### Building & Testing
```bash
dotnet restore                          # Restore NuGet packages
dotnet build                            # Build solution (both projects)
dotnet test                             # Run xUnit tests with Moq
dotnet publish -c Release               # Single-file win-x64 executable
```

### Local Development & Debugging
```bash
# Quick testing (no admin required)
CrashpadKiller.exe oneshot              # Run once
CrashpadKiller.exe daemon 30            # Run every 30 seconds

# Service testing (requires admin PowerShell)
CrashpadKiller.exe install              # Install service
sc.exe start CrashpadKiller             # Start service
sc.exe query CrashpadKiller             # Check status
CrashpadKiller.exe uninstall            # Remove service
```

### CI/CD
GitHub Actions workflow at [.github/workflows/dotnet.yml](.github/workflows/dotnet.yml):
- Runs on `ubuntu-latest` (validates cross-platform compatibility)
- .NET 9.0 SDK
- Standard: restore → build → test

### Publishing Configuration
**Single-file deployment** configured in [CrashpadKiller.csproj](CrashpadKiller/CrashpadKiller.csproj):
- `PublishSingleFile=true` + `PublishTrimmed=true`
- `RuntimeIdentifier=win-x64` (Windows-only official builds)
- `EnableCompressionInSingleFile=true`
- Config files (`nlog.config`, `process.xml`) copied via `<Content Include=...>`

## Testing Patterns

### Unit Test Framework
- **xUnit** with **Moq** for mocking (see [CrashpadKiller.Tests.csproj](CrashpadKiller.Tests/CrashpadKiller.Tests.csproj))
- **Coverlet** for code coverage collection
- Focus on testable `ProcessKiller` class with injected dependencies

### Test Structure Examples
```csharp
// Mock dependencies for isolation
var mockProcessProvider = new Mock<IProcessProvider>();
var mockFileProvider = new Mock<IFileProvider>();
var mockLogger = new Mock<ILogger>();

// Test ProcessKiller logic
var killer = new ProcessKiller(mockProcessProvider.Object, 
                               mockFileProvider.Object, 
                               mockLogger.Object);
```

### Configuration Testing
When adding config options:
1. Update [process.xml](CrashpadKiller/process.xml) schema
2. Parse in both `Program.LoadIntervalFromConfig()` and `CrashpadKillerService.LoadTargetsFromConfig()`
3. Test missing files, invalid XML, negative intervals
4. Verify fallback defaults work (600 seconds for interval)

## Common Modification Patterns

### Adding New Process Filters
1. Extend `<processes>` in [process.xml](CrashpadKiller/process.xml)
2. Update parsing in [ProcessKiller.LoadTargetsFromConfig()](CrashpadKiller/ProcessKiller.cs#L54-L68)
3. Maintain backward compatibility (existing configs should still work)

### Adding New Commands
1. Add case to switch in [Program.ProcessArguments()](CrashpadKiller/Program.cs#L43-L50)
2. If Windows-only: Add OS check before switch (see [Program.cs#L37-L40](CrashpadKiller/Program.cs#L37-L40))
3. Follow return code convention: 0 = success, 1 = error

### Service Properties
Modify in [ServiceInstaller.cs](CrashpadKiller/ServiceInstaller.cs):
- `ServiceName` (const, line 10): "CrashpadKiller"
- `ServiceDisplayName`: "CrashpadKiller Service"
- `ServiceDescription`: Description shown in Services MMC
- Must match `options.ServiceName` in [Program.cs#L143](CrashpadKiller/Program.cs#L143)

## Critical Implementation Rules

1. **File paths**: Use `Environment.ProcessPath` for executable, `AppDomain.CurrentDomain.BaseDirectory` for config files
2. **Service lifecycle**: Always respect `CancellationToken` in `ExecuteAsync()` (see [CrashpadKillerService.cs#L47-L67](CrashpadKillerService.cs#L47-L67))
3. **Process enumeration**: Wrap `Process.GetProcesses()` in try-catch (permission issues possible)
4. **XML parsing**: Use `XDocument.Parse()` only; validate interval > 0
5. **Windows-specific APIs**: Always decorate with `[SupportedOSPlatform("windows")]` and add runtime `OperatingSystem.IsWindows()` checks
6. **Logging context**: Include PID, process name, file paths in all log messages (see [ProcessKiller.cs#L86](CrashpadKiller/ProcessKiller.cs#L86))
7. **Interval validation**: Reject intervals ≤ 0 before entering service loop ([Program.cs#L104-L108](CrashpadKiller/Program.cs#L104-L108))

## Version Management
Version is centralized in `Directory.Build.props` as `CrashpadKillerVersion` property and flows to all assembly attributes automatically.