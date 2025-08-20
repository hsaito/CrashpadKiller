using System.Collections.Generic;
using System.Diagnostics;
using Moq;
using Xunit;

namespace CrashpadKiller.Tests
{
    public class ProcessKillerTests
    {
        [Fact]
        public void KillProcesses_KillsOnlyTargetProcesses_AndLogsCorrectly()
        {
            // Arrange
            var targetNames = new List<string> { "crashpad_handler", "other_process" };
            var mockProcess1 = new Mock<IProcess>();
            mockProcess1.Setup(p => p.ProcessName).Returns("crashpad_handler");
            mockProcess1.Setup(p => p.Id).Returns(123);
            var mockProcess2 = new Mock<IProcess>();
            mockProcess2.Setup(p => p.ProcessName).Returns("not_a_target");
            mockProcess2.Setup(p => p.Id).Returns(456);
            var mockProcess3 = new Mock<IProcess>();
            mockProcess3.Setup(p => p.ProcessName).Returns("other_process");
            mockProcess3.Setup(p => p.Id).Returns(789);

            var processes = new List<IProcess> { mockProcess1.Object, mockProcess2.Object, mockProcess3.Object };
            var processProvider = new Mock<IProcessProvider>();
            processProvider.Setup(p => p.GetProcesses()).Returns(processes);

            var fileProvider = new Mock<IFileProvider>();
            var logger = new Mock<ILogger>();

            var killer = new ProcessKiller(processProvider.Object, fileProvider.Object, logger.Object);

            // Act
            killer.KillProcesses(targetNames);

            // Assert
            mockProcess1.Verify(p => p.Kill(false), Times.Once);
            mockProcess2.Verify(p => p.Kill(false), Times.Never);
            mockProcess3.Verify(p => p.Kill(false), Times.Once);
            logger.Verify(l => l.Info("Killing those pesky crashpads."), Times.Once);
            logger.Verify(l => l.Info("Targets are:"), Times.Once);
            logger.Verify(l => l.Info("crashpad_handler"), Times.Once);
            logger.Verify(l => l.Info("other_process"), Times.Once);
            logger.Verify(l => l.Info("Process complete."), Times.Once);
        }

        [Fact]
        public void LoadTargetsFromConfig_ReturnsCorrectTargets()
        {
            // Arrange
            var xml = "<config><processes><process>crashpad_handler</process><process>other_process</process></processes></config>";
            var fileProvider = new Mock<IFileProvider>();
            fileProvider.Setup(f => f.ReadAllText("process.xml")).Returns(xml);
            var processProvider = new Mock<IProcessProvider>();
            var logger = new Mock<ILogger>();
            var killer = new ProcessKiller(processProvider.Object, fileProvider.Object, logger.Object);

            // Act
            var targets = killer.LoadTargetsFromConfig("process.xml");

            // Assert
            Assert.Contains("crashpad_handler", targets);
            Assert.Contains("other_process", targets);
            Assert.Equal(2, targets.Count);
        }
    }

    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {

        }
    }
}
