namespace iBackup.Core.Tests.Tests;

using FluentAssertions;
using iBackup.Core.Interfaces;
using iBackup.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

public class LocalWirelessServerTests
{
    private readonly Mock<IManifestRepository> _mockRepo;
    private readonly LocalWirelessServer _server;

    public LocalWirelessServerTests()
    {
        _mockRepo = new Mock<IManifestRepository>();
        _server = new LocalWirelessServer(_mockRepo.Object, NullLogger<LocalWirelessServer>.Instance);
    }

    [Fact]
    public void PairingPin_ShouldBeFourDigits()
    {
        _server.PairingPin.Should().NotBeNullOrWhiteSpace();
        _server.PairingPin.Length.Should().Be(4);
        int.TryParse(_server.PairingPin, out int pin).Should().BeTrue();
        pin.Should().BeInRange(1000, 9999);
    }

    [Fact]
    public void ServerUrl_ShouldIncludePortAndPin()
    {
        _server.ServerUrl.Should().Contain($":{_server.Port}/?pin={_server.PairingPin}");
    }

    [Fact]
    public void GenerateQrCodeSvg_ShouldReturnValidSvgMarkup()
    {
        string svg = _server.GenerateQrCodeSvg();

        svg.Should().NotBeNullOrWhiteSpace();
        svg.Should().StartWith("<svg");
        svg.Trim().Should().EndWith("</svg>");
        svg.Should().Contain("viewBox=");
        svg.Should().Contain("<rect");
    }

    [Fact]
    public void StopAsync_WhenNotStarted_ShouldCompleteWithoutThrowing()
    {
        var action = async () => await _server.StopAsync();
        action.Should().NotThrowAsync();
    }
}
