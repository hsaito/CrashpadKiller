# CrashpadKiller AI Coding Instructions

## Project Overview
CrashpadKiller is a .NET 9 Windows service/utility that automatically terminates specified processes (like crashpad handlers) based on XML configuration. It supports multiple execution modes: one-shot, daemon, and Windows service.

## Architecture & Key Components

### Core Entry Points
- **`Program.cs`**: Main CLI entry point with command switching (`oneshot`, `daemon`, `service`, `install`, `uninstall`)
- **`CrashpadKillerService.cs`**: Background service implementation using Microsoft.Extensions.Hosting
- **`ProcessKiller.cs`**: Testable core logic with dependency injection interfaces
- **`ServiceInstaller.cs`**: Windows service management using `sc.exe` commands

### Cross-Platform Handling Pattern
The codebase uses `OperatingSystem.IsWindows()` checks throughout. Service-related features are Windows-only and gracefully fail on other platforms with clear error messages. This pattern is critical for maintaining cross-platform compatibility.

### Configuration Architecture
- **`process.xml`**: Defines target processes and execution interval
- **`nlog.config`**: Logging configuration for console and Windows Event Log
- **File resolution**: Always checks executable directory first, then current working directory
- **Config loading**: Uses `XDocument.Parse()` with robust error handling and fallback defaults (600 seconds)

## Development Patterns

### Dependency Injection Strategy
The project uses a hybrid approach:
- **Program.cs**: Direct instantiation for CLI modes (legacy approach)
- **CrashpadKillerService**: Full DI with interfaces (`IProcessProvider`, `IFileProvider`, `ILogger`)
- **Testing**: Mock-friendly interfaces enable isolated unit testing

### Error Handling Conventions
- Use custom exceptions: `InvalidProcessConfigurationFileException`
- Log errors at multiple levels (console, Windows Event Log, structured logging)
- Graceful degradation: continue service operation even if individual process kills fail
- Admin privilege detection for service operations with clear error messages

### Logging Architecture
- **NLog**: Primary logging framework with multiple targets
- **Microsoft.Extensions.Logging**: For service integration
- **Windows Event Log**: Platform-specific logging for service mode
- **ServiceLogger**: Adapter pattern bridging NLog and Microsoft logging

## Key Development Workflows

### Building & Testing
```bash
dotnet restore                    # Restore dependencies
dotnet build                      # Build solution
dotnet test                       # Run all tests
dotnet publish -c Release         # Create self-contained executable
```

### Service Development
```bash
# Local testing (requires admin)
CrashpadKiller.exe install        # Install service
CrashpadKiller.exe uninstall      # Remove service
CrashpadKiller.exe daemon 30      # Test daemon mode locally
```

### Publishing Configuration
The project is configured for Windows-specific self-contained deployment:
- `PublishSingleFile=true`
- `RuntimeIdentifier=win-x64`
- `PublishTrimmed=true`
- Includes `nlog.config` and `process.xml` as content files

## Testing Patterns

### Unit Test Structure
- Use Moq for interface mocking
- Test both success and failure paths
- Focus on testing `ProcessKiller` class logic in isolation
- Service tests verify constructor and basic operations without complex hosting setup

### Configuration Testing
When adding new config options:
1. Update `process.xml` schema
2. Add parsing logic to `LoadIntervalFromConfig()` or similar methods
3. Test with missing files, invalid XML, and edge cases
4. Ensure fallback defaults work correctly

## Common Modifications

### Adding New Process Filters
Extend the XML schema in `process.xml` and update parsing logic in both `Program.cs` and `CrashpadKillerService.cs`. Maintain backward compatibility.

### New Execution Modes
Add new command cases to `ProcessArguments()` switch statement in `Program.cs`. Follow the existing pattern of OS checks for Windows-specific features.

### Service Configuration
Modify service properties in `ServiceInstaller.cs`. Always test with both install/uninstall operations and verify Windows Event Log integration.

## Critical Implementation Notes

- **File Paths**: Always use `AppDomain.CurrentDomain.BaseDirectory` for config file resolution
- **Service Lifecycle**: Use proper cancellation tokens in `ExecuteAsync()` for graceful shutdown
- **Process Enumeration**: Wrap `Process.GetProcesses()` calls in try-catch for permission issues
- **XML Parsing**: Use `XDocument.Parse()` consistently, never direct string manipulation
- **Logging**: Always include context (PID, process name, file paths) in log messages

## Version Management
Version is centralized in `Directory.Build.props` as `CrashpadKillerVersion` property and flows to all assembly attributes automatically.