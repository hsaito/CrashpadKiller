using System;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace CrashpadKiller.Tests;

public class InterfaceTests
{
    [Fact]
    public void ProcessWrapper_ReturnsCorrectProcessName()
    {
        // Arrange
        var currentProcess = Process.GetCurrentProcess();
        var wrapper = new ProcessWrapper(currentProcess);

        // Act
        var processName = wrapper.ProcessName;

        // Assert
        Assert.NotNull(processName);
        Assert.Equal(currentProcess.ProcessName, processName);
    }

    [Fact]
    public void ProcessWrapper_ReturnsCorrectProcessId()
    {
        // Arrange
        var currentProcess = Process.GetCurrentProcess();
        var wrapper = new ProcessWrapper(currentProcess);

        // Act
        var processId = wrapper.Id;

        // Assert
        Assert.True(processId > 0);
        Assert.Equal(currentProcess.Id, processId);
    }

    [Fact]
    public void ProcessProvider_GetProcesses_ReturnsNonEmptyList()
    {
        // Arrange
        var provider = new ProcessProvider();

        // Act
        var processes = provider.GetProcesses().ToList();

        // Assert
        Assert.NotEmpty(processes);
        Assert.All(processes, p =>
        {
            Assert.NotNull(p.ProcessName);
            // Idle process has PID 0, which is valid
            Assert.True(p.Id >= 0);
        });
    }

    [Fact]
    public void ProcessProvider_GetProcesses_ReturnsIProcessInstances()
    {
        // Arrange
        var provider = new ProcessProvider();

        // Act
        var processes = provider.GetProcesses().ToList();

        // Assert
        Assert.All(processes, p => Assert.IsAssignableFrom<IProcess>(p));
    }

    [Fact]
    public void ProcessProvider_GetProcesses_ContainsCurrentProcess()
    {
        // Arrange
        var provider = new ProcessProvider();
        var currentProcessId = Process.GetCurrentProcess().Id;

        // Act
        var processes = provider.GetProcesses().ToList();

        // Assert
        Assert.Contains(processes, p => p.Id == currentProcessId);
    }

    [Fact]
    public void InvalidProcessConfigurationFileException_WithMessage_StoresMessage()
    {
        // Arrange
        var message = "Test error message";

        // Act
        var exception = new InvalidProcessConfigurationFileException(message);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void InvalidProcessConfigurationFileException_WithMessageAndInner_StoresBoth()
    {
        // Arrange
        var message = "Outer error";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new InvalidProcessConfigurationFileException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void InvalidProcessConfigurationFileException_WithNoArgs_CreatesException()
    {
        // Act
        var exception = new InvalidProcessConfigurationFileException();

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<InvalidProcessConfigurationFileException>(exception);
    }
}
