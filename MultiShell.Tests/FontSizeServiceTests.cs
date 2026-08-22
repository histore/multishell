using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class FontSizeServiceTests
{
    [Fact]
    public void Constructor_DefaultLevels_AreLevel3()
    {
        var service = new FontSizeService();

        Assert.Equal(3, service.AppFontSizeLevel);
        Assert.Equal(3, service.TerminalFontSizeLevel);
        Assert.Equal(1.0, service.AppFontScale);
        Assert.Equal(12.0, service.TerminalFontSize);
    }

    [Theory]
    [InlineData(1, 0.85, 9.5)]
    [InlineData(2, 0.92, 10.5)]
    [InlineData(3, 1.00, 12.0)]
    [InlineData(4, 1.12, 14.0)]
    [InlineData(5, 1.25, 16.5)]
    public void GetScaleAndFontSize_ReturnsExpectedValues(int level, double expectedScale, double expectedPt)
    {
        var service = new FontSizeService();

        Assert.Equal(expectedScale, service.GetAppFontScale(level));
        Assert.Equal(expectedPt, service.GetTerminalFontSize(level));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(6, 5)]
    [InlineData(10, 5)]
    public void SetAppFontSizeLevel_ClampsToValidRange(int inputLevel, int expectedClamped)
    {
        var service = new FontSizeService();
        service.SetAppFontSizeLevel(inputLevel);

        Assert.Equal(expectedClamped, service.AppFontSizeLevel);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(6, 5)]
    [InlineData(10, 5)]
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

        service.SetAppFontSizeLevel(4);
        Assert.Equal(1, eventCallCount);
        Assert.Equal(4, lastLevel);

        // Setting same level again should not fire
        service.SetAppFontSizeLevel(4);
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
}
