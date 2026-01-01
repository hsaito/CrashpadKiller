using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Moq;
using Xunit;

namespace CrashpadKiller.Tests;

public class ProcessKillerAdvancedTests
{
    #region LoadTargetsFromConfig Tests

    [Fact]
    public void LoadTargetsFromConfig_WithValidXml_ReturnsAllTargets()
    {
        // Arrange
        var xml = @"<config>
            <interval>600</interval>
            <processes>
                <process>crashpad_handler</process>
                <process>chrome_crashpad</process>
                <process>firefox_crashpad</process>
            </processes>
        </config>";
        
        var mockFileProvider = new Mock<IFileProvider>();
        mockFileProvider.Setup(f => f.ReadAllText("config.xml")).Returns(xml);
        
        var killer = CreateProcessKiller(fileProvider: mockFileProvider.Object);

        // Act
        var targets = killer.LoadTargetsFromConfig("config.xml");

        // Assert
        Assert.Equal(3, targets.Count);
        Assert.Contains("crashpad_handler", targets);
        Assert.Contains("chrome_crashpad", targets);
        Assert.Contains("firefox_crashpad", targets);
    }

    [Fact]
    public void LoadTargetsFromConfig_WithEmptyProcessList_ReturnsEmptyList()
    {
        // Arrange
        var xml = "<config><processes></processes></config>";
        var mockFileProvider = new Mock<IFileProvider>();
        mockFileProvider.Setup(f => f.ReadAllText("config.xml")).Returns(xml);
        
        var killer = CreateProcessKiller(fileProvider: mockFileProvider.Object);

        // Act
        var targets = killer.LoadTargetsFromConfig("config.xml");

        // Assert - Empty process list returns empty collection, not exception
        Assert.NotNull(targets);
        Assert.Empty(targets);
    }

    [Fact]
    public void LoadTargetsFromConfig_WithMissingProcessesElement_ThrowsException()
    {
        // Arrange
        var xml = "<config><interval>600</interval></config>";
        var mockFileProvider = new Mock<IFileProvider>();
        mockFileProvider.Setup(f => f.ReadAllText("config.xml")).Returns(xml);
        
        var killer = CreateProcessKiller(fileProvider: mockFileProvider.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidProcessConfigurationFileException>(
            () => killer.LoadTargetsFromConfig("config.xml"));
        // The outer exception wraps the inner exception
        Assert.Contains("Failed to load process configuration", exception.Message);
    }

    [Fact]
    public void LoadTargetsFromConfig_WithInvalidXml_ThrowsException()
    {
        // Arrange
        var invalidXml = "<config><processes><process>unclosed";
        var mockFileProvider = new Mock<IFileProvider>();
        mockFileProvider.Setup(f => f.ReadAllText("config.xml")).Returns(invalidXml);
        
        var killer = CreateProcessKiller(fileProvider: mockFileProvider.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidProcessConfigurationFileException>(
            () => killer.LoadTargetsFromConfig("config.xml"));
        Assert.Contains("Failed to load process configuration", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void LoadTargetsFromConfig_WithFileReadError_ThrowsException()
    {
        // Arrange
        var mockFileProvider = new Mock<IFileProvider>();
        mockFileProvider.Setup(f => f.ReadAllText("config.xml"))
            .Throws(new System.IO.FileNotFoundException("Config file not found"));
        
        var killer = CreateProcessKiller(fileProvider: mockFileProvider.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidProcessConfigurationFileException>(
            () => killer.LoadTargetsFromConfig("config.xml"));
        Assert.Contains("Failed to load process configuration", exception.Message);
    }

    [Fact]
    public void LoadTargetsFromConfig_WithWhitespaceProcessNames_TrimsCorrectly()
    {
        // Arrange
        var xml = @"<config>
            <processes>
                <process>  crashpad_handler  </process>
                <process>
                    chrome_crashpad
                </process>
            </processes>
        </config>";
        
        var mockFileProvider = new Mock<IFileProvider>();
        mockFileProvider.Setup(f => f.ReadAllText("config.xml")).Returns(xml);
        
        var killer = CreateProcessKiller(fileProvider: mockFileProvider.Object);

        // Act
        var targets = killer.LoadTargetsFromConfig("config.xml");

        // Assert - XDocument.Parse preserves whitespace in element values
        Assert.Equal(2, targets.Count);
        Assert.Contains(targets, t => t.Contains("crashpad_handler"));
        Assert.Contains(targets, t => t.Contains("chrome_crashpad"));
    }

    #endregion

    #region KillProcesses Tests

    [Fact]
    public void KillProcesses_WithMatchingProcesses_KillsOnlyMatches()
    {
        // Arrange
        var targets = new List<string> { "target1", "target2" };
        
        var mockTarget1 = CreateMockProcess("target1", 100);
        var mockTarget2 = CreateMockProcess("target2", 200);
        var mockNonTarget1 = CreateMockProcess("notarget", 300);
        var mockNonTarget2 = CreateMockProcess("other", 400);
        
        var mockProcessProvider = new Mock<IProcessProvider>();
        mockProcessProvider.Setup(p => p.GetProcesses()).Returns(new List<IProcess>
        {
            mockTarget1.Object,
            mockNonTarget1.Object,
            mockTarget2.Object,
            mockNonTarget2.Object
        });
        
        var killer = CreateProcessKiller(processProvider: mockProcessProvider.Object);

        // Act
        killer.KillProcesses(targets);

        // Assert
        mockTarget1.Verify(p => p.Kill(false), Times.Once);
        mockTarget2.Verify(p => p.Kill(false), Times.Once);
        mockNonTarget1.Verify(p => p.Kill(false), Times.Never);
        mockNonTarget2.Verify(p => p.Kill(false), Times.Never);
    }

    [Fact]
    public void KillProcesses_WithNullTargetList_LogsWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var killer = CreateProcessKiller(logger: mockLogger.Object);

        // Act
        killer.KillProcesses(null!);

        // Assert
        mockLogger.Verify(l => l.Warn("No targets specified in configuration."), Times.Once);
        mockLogger.Verify(l => l.Info("Process complete."), Times.Once);
    }

    [Fact]
    public void KillProcesses_WithEmptyTargetList_LogsWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var killer = CreateProcessKiller(logger: mockLogger.Object);

        // Act
        killer.KillProcesses(new List<string>());

        // Assert
        mockLogger.Verify(l => l.Warn("No targets specified in configuration."), Times.Once);
        mockLogger.Verify(l => l.Info("Process complete."), Times.Once);
    }

    [Fact]
    public void KillProcesses_WhenKillThrows_LogsWarningAndContinues()
    {
        // Arrange
        var targets = new List<string> { "target1", "target2" };
        
        var mockTarget1 = CreateMockProcess("target1", 100);
        mockTarget1.Setup(p => p.Kill(false))
            .Throws(new InvalidOperationException("Process already exited"));
        
        var mockTarget2 = CreateMockProcess("target2", 200);
        
        var mockProcessProvider = new Mock<IProcessProvider>();
        mockProcessProvider.Setup(p => p.GetProcesses()).Returns(new List<IProcess>
        {
            mockTarget1.Object,
            mockTarget2.Object
        });
        
        var mockLogger = new Mock<ILogger>();
        var killer = CreateProcessKiller(
            processProvider: mockProcessProvider.Object,
            logger: mockLogger.Object);

        // Act
        killer.KillProcesses(targets);

        // Assert - Both kill attempts should be made despite first one failing
        mockTarget1.Verify(p => p.Kill(false), Times.Once);
        mockTarget2.Verify(p => p.Kill(false), Times.Once);
        
        // Verify warning was logged for the failure
        mockLogger.Verify(l => l.Warn(It.Is<string>(
            s => s.Contains("Failed to kill target1") && s.Contains("100"))), 
            Times.Once);
        
        // Verify process completed
        mockLogger.Verify(l => l.Info("Process complete."), Times.Once);
    }

    [Fact]
    public void KillProcesses_WithNoMatchingProcesses_CompletesSuccessfully()
    {
        // Arrange
        var targets = new List<string> { "nonexistent1", "nonexistent2" };
        
        var mockProcess = CreateMockProcess("some_other_process", 999);
        
        var mockProcessProvider = new Mock<IProcessProvider>();
        mockProcessProvider.Setup(p => p.GetProcesses()).Returns(new List<IProcess>
        {
            mockProcess.Object
        });
        
        var mockLogger = new Mock<ILogger>();
        var killer = CreateProcessKiller(
            processProvider: mockProcessProvider.Object,
            logger: mockLogger.Object);

        // Act
        killer.KillProcesses(targets);

        // Assert
        mockProcess.Verify(p => p.Kill(false), Times.Never);
        mockLogger.Verify(l => l.Info("Process complete."), Times.Once);
    }

    [Fact]
    public void KillProcesses_LogsAllTargets_BeforeKilling()
    {
        // Arrange
        var targets = new List<string> { "process1", "process2", "process3" };
        var mockLogger = new Mock<ILogger>();
        var killer = CreateProcessKiller(logger: mockLogger.Object);

        // Act
        killer.KillProcesses(targets);

        // Assert - Verify logging sequence
        mockLogger.Verify(l => l.Info("Killing those pesky crashpads."), Times.Once);
        mockLogger.Verify(l => l.Info("Targets are:"), Times.Once);
        mockLogger.Verify(l => l.Info("process1"), Times.Once);
        mockLogger.Verify(l => l.Info("process2"), Times.Once);
        mockLogger.Verify(l => l.Info("process3"), Times.Once);
    }

    [Fact]
    public void KillProcesses_LogsDebugMessage_ForEachKillAttempt()
    {
        // Arrange
        var targets = new List<string> { "target1", "target2" };
        
        var mockTarget1 = CreateMockProcess("target1", 123);
        var mockTarget2 = CreateMockProcess("target2", 456);
        
        var mockProcessProvider = new Mock<IProcessProvider>();
        mockProcessProvider.Setup(p => p.GetProcesses()).Returns(new List<IProcess>
        {
            mockTarget1.Object,
            mockTarget2.Object
        });
        
        var mockLogger = new Mock<ILogger>();
        var killer = CreateProcessKiller(
            processProvider: mockProcessProvider.Object,
            logger: mockLogger.Object);

        // Act
        killer.KillProcesses(targets);

        // Assert
        mockLogger.Verify(l => l.Debug("Attempting to kill target1 (PID: 123)"), Times.Once);
        mockLogger.Verify(l => l.Debug("Attempting to kill target2 (PID: 456)"), Times.Once);
    }

    [Fact]
    public void KillProcesses_WithDuplicateProcessNames_KillsAll()
    {
        // Arrange - Simulate multiple instances of same process
        var targets = new List<string> { "crashpad_handler" };
        
        var mockInstance1 = CreateMockProcess("crashpad_handler", 100);
        var mockInstance2 = CreateMockProcess("crashpad_handler", 200);
        var mockInstance3 = CreateMockProcess("crashpad_handler", 300);
        
        var mockProcessProvider = new Mock<IProcessProvider>();
        mockProcessProvider.Setup(p => p.GetProcesses()).Returns(new List<IProcess>
        {
            mockInstance1.Object,
            mockInstance2.Object,
            mockInstance3.Object
        });
        
        var killer = CreateProcessKiller(processProvider: mockProcessProvider.Object);

        // Act
        killer.KillProcesses(targets);

        // Assert - All three instances should be killed
        mockInstance1.Verify(p => p.Kill(false), Times.Once);
        mockInstance2.Verify(p => p.Kill(false), Times.Once);
        mockInstance3.Verify(p => p.Kill(false), Times.Once);
    }

    [Fact]
    public void KillProcesses_PassesFalse_ToKillMethod()
    {
        // Arrange - Verify that entireProcessTree parameter is always false
        var targets = new List<string> { "target" };
        var mockTarget = CreateMockProcess("target", 100);
        
        var mockProcessProvider = new Mock<IProcessProvider>();
        mockProcessProvider.Setup(p => p.GetProcesses()).Returns(new List<IProcess>
        {
            mockTarget.Object
        });
        
        var killer = CreateProcessKiller(processProvider: mockProcessProvider.Object);

        // Act
        killer.KillProcesses(targets);

        // Assert - Specifically verify the parameter value
        mockTarget.Verify(p => p.Kill(It.Is<bool>(entireTree => entireTree == false)), Times.Once);
    }

    #endregion

    #region Helper Methods

    private ProcessKiller CreateProcessKiller(
        IProcessProvider? processProvider = null,
        IFileProvider? fileProvider = null,
        ILogger? logger = null)
    {
        return new ProcessKiller(
            processProvider ?? new Mock<IProcessProvider>().Object,
            fileProvider ?? new Mock<IFileProvider>().Object,
            logger ?? new Mock<ILogger>().Object);
    }

    private Mock<IProcess> CreateMockProcess(string name, int pid)
    {
        var mock = new Mock<IProcess>();
        mock.Setup(p => p.ProcessName).Returns(name);
        mock.Setup(p => p.Id).Returns(pid);
        return mock;
    }

    #endregion
}
