using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace CrashpadKiller;

[SupportedOSPlatform("windows")]
public static class ServiceInstaller
{
    private const string ServiceName = "CrashpadKiller";
    private const string ServiceDisplayName = "CrashpadKiller Service";
    private const string ServiceDescription = "Automatically terminates specified processes such as crashpad handlers.";

    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void InstallService()
    {
        if (!IsRunningAsAdministrator())
        {
            throw new UnauthorizedAccessException("Administrator privileges are required to install a Windows service.");
        }

        var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to determine executable path.");
        
        var arguments = $"create \"{ServiceName}\" binPath=\"\\\"{exePath}\\\" service\" DisplayName=\"{ServiceDisplayName}\" start=auto";
        
        var exitCode = RunScCommand(arguments);
        if (exitCode == 0)
        {
            Console.WriteLine($"Service '{ServiceDisplayName}' installed successfully.");
            
            // Set service description
            var descArgs = $"description \"{ServiceName}\" \"{ServiceDescription}\"";
            RunScCommand(descArgs);
        }
        else
        {
            throw new InvalidOperationException($"Failed to install service. SC command exited with code {exitCode}.");
        }
    }

    public static void UninstallService()
    {
        if (!IsRunningAsAdministrator())
        {
            throw new UnauthorizedAccessException("Administrator privileges are required to uninstall a Windows service.");
        }

        // First, stop the service if it's running
        var stopArgs = $"stop \"{ServiceName}\"";
        RunScCommand(stopArgs); // Ignore exit code for stop command

        // Delete the service
        var deleteArgs = $"delete \"{ServiceName}\"";
        var exitCode = RunScCommand(deleteArgs);
        
        if (exitCode == 0)
        {
            Console.WriteLine($"Service '{ServiceDisplayName}' uninstalled successfully.");
        }
        else
        {
            throw new InvalidOperationException($"Failed to uninstall service. SC command exited with code {exitCode}.");
        }
    }

    public static bool IsServiceInstalled()
    {
        var queryArgs = $"query \"{ServiceName}\"";
        var exitCode = RunScCommand(queryArgs, suppressOutput: true);
        return exitCode == 0;
    }

    private static int RunScCommand(string arguments, bool suppressOutput = false)
    {
        using var process = new Process();
        process.StartInfo.FileName = "sc.exe";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        
        if (suppressOutput)
        {
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
        }

        try
        {
            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to execute SC command: {ex.Message}", ex);
        }
    }
}