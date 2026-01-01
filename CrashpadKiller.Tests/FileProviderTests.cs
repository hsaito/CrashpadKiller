using System;
using System.IO;
using Xunit;

namespace CrashpadKiller.Tests;

public class FileProviderTests
{
    [Fact]
    public void FileProvider_ReadAllText_ReadsFileContent()
    {
        // Arrange
        var fileProvider = new FileProvider();
        var tempFile = Path.GetTempFileName();
        var expectedContent = "Test file content\nLine 2\nLine 3";
        File.WriteAllText(tempFile, expectedContent);

        try
        {
            // Act
            var actualContent = fileProvider.ReadAllText(tempFile);

            // Assert
            Assert.Equal(expectedContent, actualContent);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FileProvider_ReadAllText_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var fileProvider = new FileProvider();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => fileProvider.ReadAllText(nonExistentPath));
    }

    [Fact]
    public void FileProvider_ReadAllText_WithEmptyFile_ReturnsEmptyString()
    {
        // Arrange
        var fileProvider = new FileProvider();
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, string.Empty);

        try
        {
            // Act
            var content = fileProvider.ReadAllText(tempFile);

            // Assert
            Assert.Equal(string.Empty, content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FileProvider_ReadAllText_WithXmlContent_ReturnsCorrectContent()
    {
        // Arrange
        var fileProvider = new FileProvider();
        var tempFile = Path.GetTempFileName();
        var xmlContent = @"<config>
    <interval>600</interval>
    <processes>
        <process>crashpad_handler</process>
    </processes>
</config>";
        File.WriteAllText(tempFile, xmlContent);

        try
        {
            // Act
            var content = fileProvider.ReadAllText(tempFile);

            // Assert
            Assert.Equal(xmlContent, content);
            Assert.Contains("<config>", content);
            Assert.Contains("crashpad_handler", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FileProvider_ReadAllText_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var fileProvider = new FileProvider();
        var tempFile = Path.GetTempFileName();
        var contentWithSpecialChars = "Special chars: äöü ñ é € 中文 日本語";
        File.WriteAllText(tempFile, contentWithSpecialChars);

        try
        {
            // Act
            var content = fileProvider.ReadAllText(tempFile);

            // Assert
            Assert.Equal(contentWithSpecialChars, content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
