using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services;

/// <summary>
/// Unit tests for the ClamAvVirusScanService.
/// Tests virus scanning behavior when enabled and disabled.
/// </summary>
public class ClamAvVirusScanServiceTests
{
    private readonly Mock<ILogger<ClamAvVirusScanService>> _loggerMock;

    public ClamAvVirusScanServiceTests()
    {
        _loggerMock = new Mock<ILogger<ClamAvVirusScanService>>();
    }

    #region Disabled Service Tests

    [Fact]
    public async Task ScanAsync_WhenDisabled_ReturnsSkipped()
    {
        // Arrange
        var settings = CreateSettings(enabled: false);
        var service = new ClamAvVirusScanService(_loggerMock.Object, settings);
        var fileBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        var result = await service.ScanAsync(fileBytes, "test.jpg");

        // Assert
        Assert.True(result.IsClean);
        Assert.False(result.ScanPerformed);
        Assert.True(result.ScanSucceeded);
        Assert.Null(result.VirusName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ScanAsync_WithStream_WhenDisabled_ReturnsSkipped()
    {
        // Arrange
        var settings = CreateSettings(enabled: false);
        var service = new ClamAvVirusScanService(_loggerMock.Object, settings);
        using var stream = new MemoryStream([0x00, 0x01, 0x02, 0x03]);

        // Act
        var result = await service.ScanAsync(stream, "test.jpg");

        // Assert
        Assert.True(result.IsClean);
        Assert.False(result.ScanPerformed);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var settings = CreateSettings(enabled: false);
        var service = new ClamAvVirusScanService(_loggerMock.Object, settings);

        // Act
        var result = await service.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Enabled Service Tests (Connection Failure Scenarios)

    [Fact]
    public async Task ScanAsync_WhenEnabledButClamAvUnavailable_FailClosed_ReturnsError()
    {
        // Arrange - Enable with invalid host (will fail to connect)
        var settings = CreateSettings(
            enabled: true, 
            host: "invalid-host-that-does-not-exist", 
            port: 9999,
            failOpen: false);
        var service = new ClamAvVirusScanService(_loggerMock.Object, settings);
        var fileBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        var result = await service.ScanAsync(fileBytes, "test.jpg");

        // Assert - Fail closed means file is rejected when scan fails
        Assert.False(result.IsClean);
        Assert.True(result.ScanPerformed);
        Assert.False(result.ScanSucceeded);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ScanAsync_WhenEnabledButClamAvUnavailable_FailOpen_ReturnsClean()
    {
        // Arrange - Enable with invalid host but failOpen = true
        var settings = CreateSettings(
            enabled: true, 
            host: "invalid-host-that-does-not-exist", 
            port: 9999,
            failOpen: true);
        var service = new ClamAvVirusScanService(_loggerMock.Object, settings);
        var fileBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        var result = await service.ScanAsync(fileBytes, "test.jpg");

        // Assert - Fail open means file is allowed when scan fails
        Assert.True(result.IsClean);
        Assert.True(result.ScanPerformed);
        // Note: ScanSucceeded may be true or false depending on how we handle failOpen
    }

    [Fact]
    public async Task IsAvailableAsync_WhenEnabledButClamAvUnavailable_ReturnsFalse()
    {
        // Arrange
        var settings = CreateSettings(
            enabled: true, 
            host: "invalid-host-that-does-not-exist", 
            port: 9999);
        var service = new ClamAvVirusScanService(_loggerMock.Object, settings);

        // Act
        var result = await service.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region VirusScanResult Factory Tests

    [Fact]
    public void VirusScanResult_Clean_HasCorrectProperties()
    {
        // Act
        var result = VirusScanResult.Clean("CLEAN: OK");

        // Assert
        Assert.True(result.IsClean);
        Assert.True(result.ScanPerformed);
        Assert.True(result.ScanSucceeded);
        Assert.Null(result.VirusName);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("CLEAN: OK", result.RawResult);
    }

    [Fact]
    public void VirusScanResult_Infected_HasCorrectProperties()
    {
        // Act
        var result = VirusScanResult.Infected("Eicar-Test-Signature", "FOUND: Eicar-Test-Signature");

        // Assert
        Assert.False(result.IsClean);
        Assert.True(result.ScanPerformed);
        Assert.True(result.ScanSucceeded);
        Assert.Equal("Eicar-Test-Signature", result.VirusName);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("FOUND: Eicar-Test-Signature", result.RawResult);
    }

    [Fact]
    public void VirusScanResult_Error_HasCorrectProperties()
    {
        // Act
        var result = VirusScanResult.Error("Connection refused");

        // Assert
        Assert.False(result.IsClean);
        Assert.True(result.ScanPerformed);
        Assert.False(result.ScanSucceeded);
        Assert.Null(result.VirusName);
        Assert.Equal("Connection refused", result.ErrorMessage);
    }

    [Fact]
    public void VirusScanResult_Skipped_HasCorrectProperties()
    {
        // Act
        var result = VirusScanResult.Skipped();

        // Assert
        Assert.True(result.IsClean);
        Assert.False(result.ScanPerformed);
        Assert.True(result.ScanSucceeded);
        Assert.Null(result.VirusName);
        Assert.Null(result.ErrorMessage);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_WithEmptyHost_DisablesScanningGracefully()
    {
        // Arrange
        var settings = Options.Create(new VirusScanSettings
        {
            Enabled = true,
            ClamAvHost = "", // Empty host should be treated as disabled
            ClamAvPort = 3310
        });

        // Act & Assert - Should not throw
        var service = new ClamAvVirusScanService(_loggerMock.Object, settings);
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullHost_DisablesScanningGracefully()
    {
        // Arrange
        var settings = Options.Create(new VirusScanSettings
        {
            Enabled = true,
            ClamAvHost = null!, // Null host should be treated as disabled
            ClamAvPort = 3310
        });

        // Act & Assert - Should not throw
        var service = new ClamAvVirusScanService(_loggerMock.Object, settings);
        Assert.NotNull(service);
    }

    #endregion

    #region Helper Methods

    private static IOptions<VirusScanSettings> CreateSettings(
        bool enabled = false,
        string host = "localhost",
        int port = 3310,
        bool failOpen = false)
    {
        return Options.Create(new VirusScanSettings
        {
            Enabled = enabled,
            ClamAvHost = host,
            ClamAvPort = port,
            FailOpen = failOpen,
            MaxStreamSizeBytes = 100 * 1024 * 1024,
            TimeoutSeconds = 60
        });
    }

    #endregion
}
