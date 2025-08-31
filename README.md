# CrashpadKiller

CrashpadKiller is a utility for automatically terminating specified processes (such as crashpad handlers) on Windows. It can be run as a command-line tool, in daemon mode, or as a Windows service.

## Features
- Kill specified processes listed in `process.xml`
- Run as a one-shot command, in daemon mode, or as a Windows service
- Windows service installation and uninstallation support (Windows only)
- Configurable execution interval (default: **600 seconds**; can be set in `process.xml` or via command line)
- Logging via NLog (see `nlog.config`)
- Logs to Windows Event Log (Windows only)
- Robust error handling and diagnostics
- Modern .NET implementation (requires .NET 9)
- **Safe cross-platform behavior:** service and EventLog features are only available on Windows; on other platforms, these commands emit errors and exit safely

## Requirements
- **.NET 9 (required for official builds)**
- Windows (official binaries)
- Other platforms may be supported if you build from source; service and EventLog features are disabled outside Windows

## Installation
1. Download or build CrashpadKiller for .NET 9.
2. Place `CrashpadKiller.exe`, `process.xml`, and `nlog.config` in the same directory (the executable directory).
3. Ensure .NET 9 runtime is installed on your system.

## Usage

### 1. Configuration
- Place your target process names in `process.xml` in the same directory as the executable. Example:

```xml
<config>
  <interval>600</interval>
  <processes>
    <process>crashpad_handler</process>
    <process>other_process</process>
  </processes>
</config>
```

- Place `nlog.config` in the same directory for logging configuration.

### 2. Command Line

- **One-shot mode:**
  ```sh
  CrashpadKiller.exe oneshot
  ```
- **Daemon mode:**
  ```sh
  CrashpadKiller.exe daemon [interval]
  ```
  (Runs continuously every [interval] seconds, default: **600**)

### 3. Windows Service (Windows only)

- **Install as Windows service:**
  ```sh
  CrashpadKiller.exe install
  ```
  (Requires Administrator privileges; only available on Windows)

- **Uninstall Windows service:**
  ```sh
  CrashpadKiller.exe uninstall
  ```
  (Requires Administrator privileges; only available on Windows)

- **Run as Windows service:**
  ```sh
  CrashpadKiller.exe service [interval]
  ```
  (This command is used internally by the Windows Service Manager; only available on Windows)

The Windows service will run automatically at system startup and process targets every 600 seconds (or the specified interval).

> **Note:**
> - Make sure `process.xml` and `nlog.config` are in the same directory as `CrashpadKiller.exe`.
> - Service installation/uninstallation requires Administrator privileges.
> - The service logs to both the console and Windows Event Log (Windows only).
> - On non-Windows platforms, service and EventLog features are disabled and will emit errors if used.

## Improvements in This Version
- Always loads config/log files from the executable directory, with fallback logic
- Improved error logging and diagnostics
- Handles fatal errors gracefully and logs them
- **Added Windows service support with installation/uninstallation (Windows only)**
- **Safe cross-platform behavior: service/EventLog features only on Windows**
- **Default interval is now 600 seconds**
- **Requires .NET 9**

## Testing
To run unit tests:

```sh
cd CrashpadKiller.Tests
 dotnet test
```

## License
See [LICENSE](LICENSE)
