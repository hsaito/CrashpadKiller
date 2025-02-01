# CrashpadKiller

CrashpadKiller is a utility designed to terminate `crashpad_handler` processes that can cause system infestations. This tool is written in C# and aims to help users manage and control unwanted crashpad processes on their systems.

## Features

- Identifies and terminates `crashpad_handler` processes
- Easy to use command-line interface
- Lightweight and efficient

## Installation

1. Clone the repository:
    ```sh
    git clone https://github.com/hsaito/CrashpadKiller.git
    cd CrashpadKiller
    ```

2. Build the project using your preferred C# development environment (e.g., Visual Studio or `dotnet` CLI).

## Usage

Run the compiled executable from the command line. For example:
```sh
CrashpadKiller.exe
```

You can add the program to your task scheduler to run automatically at startup.