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

## Installation
1. Download or build CrashpadKiller for .NET 9.
2. Place `CrashpadKiller.exe`, `process.xml`, and `nlog.config` in the same directory.
3. Ensure .NET 9 runtime is installed on your system.

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

## Improvements in This Version
- Always loads config/log files from the executable directory
- Improved error logging and diagnostics
- Handles fatal errors gracefully and logs them
- **Requires .NET 9**

## Testing
To run unit tests:

```sh
cd CrashpadKiller.Tests
 dotnet test
```

## License
See [LICENSE](LICENSE)
