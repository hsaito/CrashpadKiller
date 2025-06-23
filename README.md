# CrashpadKiller

CrashpadKiller is a utility for automatically terminating specified processes (such as crashpad handlers) on Windows. It can be run as a command-line tool or in daemon mode.

## Features
- Kill specified processes listed in `process.xml`
- Run as a one-shot command or in daemon mode
- Configurable execution interval
- Logging via NLog (see `nlog.config`)
- Modern .NET implementation (requires .NET 9)

## Requirements
- **.NET 9 (required for official builds)**
- Windows (official binaries)
- Other platforms may be supported if you build from source and adapt as needed

## Usage

### 1. Configuration
- Place your target process names in `process.xml` in the same directory as the executable. Example:

```xml
<config>
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
  CrashpadKiller.exe daemon --interval 60
  ```
  (Runs every 60 seconds)

> **Note:**
> - Make sure `process.xml` and `nlog.config` are in the same directory as `CrashpadKiller.exe`.
> - The service will log to the file specified in `nlog.config`.
> - If the service fails to start, check the log file for errors (e.g., missing config files, permission issues).

## Improvements in This Version
- Uses .NET Worker Service pattern for robust Windows service support
- Always loads config/log files from the executable directory
- Improved error logging and diagnostics
- Handles fatal errors gracefully and logs them
- **Requires .NET 9**

## Troubleshooting
- If the service fails to start (Error 1053), check the log file for details
- Ensure all config files are present in the executable directory
- Run as administrator if required

## License
See [LICENSE](LICENSE)
