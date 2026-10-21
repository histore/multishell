using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class FontSizeServiceTests
{
    [Fact]
    public void Constructor_DefaultLevels_AreLevel4()
    {
        var service = new FontSizeService();

        Assert.Equal(4, service.AppFontSizeLevel);
        Assert.Equal(4, service.TerminalFontSizeLevel);
        Assert.Equal(1.0, service.AppFontScale);
        Assert.Equal(14.0, service.TerminalFontSize);
    }

    [Theory]
    [InlineData(1, 0.80, 10.0)]
    [InlineData(2, 0.88, 11.0)]
    [InlineData(3, 0.94, 12.5)]
    [InlineData(4, 1.00, 14.0)]
    [InlineData(5, 1.08, 16.0)]
    [InlineData(6, 1.18, 18.5)]
    [InlineData(7, 1.30, 21.0)]
    public void GetScaleAndFontSize_ReturnsExpectedValues(int level, double expectedScale, double expectedPt)
    {
        var service = new FontSizeService();

        Assert.Equal(expectedScale, service.GetAppFontScale(level));
        Assert.Equal(expectedPt, service.GetTerminalFontSize(level));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(7, 7)]
    [InlineData(8, 7)]
    [InlineData(12, 7)]
    public void SetAppFontSizeLevel_ClampsToValidRange(int inputLevel, int expectedClamped)
    {
        var service = new FontSizeService();
        service.SetAppFontSizeLevel(inputLevel);

        Assert.Equal(expectedClamped, service.AppFontSizeLevel);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(7, 7)]
    [InlineData(8, 7)]
    [InlineData(12, 7)]
    public void SetTerminalFontSizeLevel_ClampsToValidRange(int inputLevel, int expectedClamped)
    {
        var service = new FontSizeService();
        service.SetTerminalFontSizeLevel(inputLevel);

        Assert.Equal(expectedClamped, service.TerminalFontSizeLevel);
    }

    [Fact]
    public void SetAppFontSizeLevel_FiresEventOnlyWhenChanged()
    {
        var service = new FontSizeService();
        int eventCallCount = 0;
        int lastLevel = 0;

        service.AppFontSizeLevelChanged += lvl =>
        {
            eventCallCount++;
            lastLevel = lvl;
        };

        service.SetAppFontSizeLevel(5);
        Assert.Equal(1, eventCallCount);
        Assert.Equal(5, lastLevel);

        // Setting same level again should not fire
        service.SetAppFontSizeLevel(5);
        Assert.Equal(1, eventCallCount);
    }

    [Fact]
    public void SetTerminalFontSizeLevel_FiresEventOnlyWhenChanged()
    {
        var service = new FontSizeService();
        int eventCallCount = 0;
        int lastLevel = 0;

        service.TerminalFontSizeLevelChanged += lvl =>
        {
            eventCallCount++;
            lastLevel = lvl;
        };

        service.SetTerminalFontSizeLevel(2);
        Assert.Equal(1, eventCallCount);
        Assert.Equal(2, lastLevel);

        // Setting same level again should not fire
        service.SetTerminalFontSizeLevel(2);
        Assert.Equal(1, eventCallCount);
    }

    [Fact]
    public void ResetLevels_ResetsBothLevelsToDefaultLevel4()
    {
        var service = new FontSizeService(initialAppLevel: 7, initialTerminalLevel: 1);
        Assert.Equal(7, service.AppFontSizeLevel);
        Assert.Equal(1, service.TerminalFontSizeLevel);

        service.ResetLevels();

        Assert.Equal(IFontSizeService.DefaultLevel, service.AppFontSizeLevel);
        Assert.Equal(IFontSizeService.DefaultLevel, service.TerminalFontSizeLevel);
    }

    [Fact]
    public void ResetTerminalFontSizeLevel_ResetsOnlyTerminalLevel()
    {
        var service = new FontSizeService(initialAppLevel: 7, initialTerminalLevel: 1);

        service.ResetTerminalFontSizeLevel();

        Assert.Equal(7, service.AppFontSizeLevel);
        Assert.Equal(IFontSizeService.DefaultLevel, service.TerminalFontSizeLevel);
    }

    [Fact]
    public void ResetAppFontSizeLevel_ResetsOnlyAppLevel()
    {
        var service = new FontSizeService(initialAppLevel: 7, initialTerminalLevel: 1);

        service.ResetAppFontSizeLevel();

        Assert.Equal(IFontSizeService.DefaultLevel, service.AppFontSizeLevel);
        Assert.Equal(1, service.TerminalFontSizeLevel);
    }
}
