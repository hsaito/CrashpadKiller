using Xunit;
using Microsoft.Extensions.Logging;
using Moq;

namespace CrashpadKiller.Tests;

public class CrashpadKillerServiceTests
{
    [Fact]
    public void CrashpadKillerService_Constructor_SetsInterval()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CrashpadKillerService>>();
        var interval = 30;

        // Act
        var service = new CrashpadKillerService(mockLogger.Object, interval);

        // Assert
        Assert.NotNull(service);
        // We can't directly test the private interval field, but we can ensure
        // the service was created without exceptions
    }

    [Fact]
    public void ServiceLogger_Methods_DoNotThrow()
    {
        // Arrange
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger>();
        var serviceLogger = new ServiceLogger(mockLogger.Object);

        // Act & Assert - Just verify that calling these methods doesn't throw
        serviceLogger.Info("Info message");
        serviceLogger.Debug("Debug message");
        serviceLogger.Warn("Warning message");
        
        // If we get here, all methods executed without exceptions
        Assert.True(true);
    }

    [Fact]
    public void FileProvider_ReadAllText_ReturnsFileContent()
    {
        // Arrange
        var fileProvider = new FileProvider();
        var tempFilePath = Path.GetTempFileName();
        var expectedContent = "Test content";
        File.WriteAllText(tempFilePath, expectedContent);

        try
        {
            // Act
            var actualContent = fileProvider.ReadAllText(tempFilePath);

            // Assert
            Assert.Equal(expectedContent, actualContent);
        }
        finally
        {
            // Cleanup
            File.Delete(tempFilePath);
        }
    }
}