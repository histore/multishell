using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class PathColorCodingServiceTests
{
    private readonly PathColorCodingService _service = new();

    [Fact]
    public void GetPathSegments_NullOrEmpty_ReturnsEmptyList()
    {
        Assert.Empty(_service.GetPathSegments(null));
        Assert.Empty(_service.GetPathSegments(""));
        Assert.Empty(_service.GetPathSegments("   "));
    }

    [Fact]
    public void GetPathSegments_StandardWindowsPath_SplitsCorrectly()
    {
        var segments = _service.GetPathSegments(@"C:\dt\multishell\Services");

        Assert.Equal(4, segments.Count);
        Assert.Equal("C:", segments[0]);
        Assert.Equal("dt", segments[1]);
        Assert.Equal("multishell", segments[2]);
        Assert.Equal("Services", segments[3]);
    }

    [Fact]
    public void GetPathSegments_UnixAndMixedSeparators_SplitsCorrectly()
    {
        var segments = _service.GetPathSegments("/home/user/project/src");

        Assert.Equal(4, segments.Count);
        Assert.Equal("home", segments[0]);
        Assert.Equal("user", segments[1]);
        Assert.Equal("project", segments[2]);
        Assert.Equal("src", segments[3]);
    }

    [Fact]
    public void GetCommonPrefixLength_SinglePathOrEmpty_ReturnsZero()
    {
        Assert.Equal(0, _service.GetCommonPrefixLength(new List<IReadOnlyList<string>>()));
        Assert.Equal(0, _service.GetCommonPrefixLength(new List<IReadOnlyList<string>>
        {
            new[] { "C:", "dt", "multishell" }
        }));
    }

    [Fact]
    public void GetCommonPrefixLength_TwoPathsSharingDriveAndFolder_ReturnsMatchingCount()
    {
        var p1 = new[] { "C:", "dt", "projectA", "src" };
        var p2 = new[] { "C:", "dt", "projectB", "api" };

        var count = _service.GetCommonPrefixLength(new[] { p1, p2 });

        Assert.Equal(2, count); // "C:" and "dt"
    }

    [Fact]
    public void GetCommonPrefixLength_CaseInsensitiveMatching_Succeeds()
    {
        var p1 = new[] { "c:", "DT", "projectA" };
        var p2 = new[] { "C:", "dt", "projectB" };

        var count = _service.GetCommonPrefixLength(new[] { p1, p2 });

        Assert.Equal(2, count);
    }

    [Fact]
    public void GetStripesForPath_UserScenario_TwoTabsOnRootLevel_OmitsDriveAndShowsFolders()
    {
        // When tabs are on C:\xxx and C:\yyy, both folders should be displayed
        var path1 = @"C:\xxx";
        var path2 = @"C:\yyy";

        var stripes1 = _service.GetStripesForPath(path1);
        var stripes2 = _service.GetStripesForPath(path2);

        Assert.Single(stripes1);
        Assert.Equal("xxx", stripes1[0].FolderName);

        Assert.Single(stripes2);
        Assert.Equal("yyy", stripes2[0].FolderName);
    }

    [Fact]
    public void GetStripesForPath_UserScenario_CommonParentDt_DisplaysAllFoldersForBothTabs()
    {
        var path1 = @"C:\dt\projectA";
        var path2 = @"C:\dt\projectB";

        var stripes1 = _service.GetStripesForPath(path1);
        var stripes2 = _service.GetStripesForPath(path2);

        // Both tabs share "dt" as their first level, followed by their distinct project
        Assert.Equal(2, stripes1.Count);
        Assert.Equal("dt", stripes1[0].FolderName);
        Assert.Equal("projectA", stripes1[1].FolderName);

        Assert.Equal(2, stripes2.Count);
        Assert.Equal("dt", stripes2[0].FolderName);
        Assert.Equal("projectB", stripes2[1].FolderName);

        // Common structure has identical color
        Assert.Equal(stripes1[0].HexColor, stripes2[0].HexColor);
    }

    [Fact]
    public void GetStripesForPath_SubfoldersInSameProject_DisplaysAllFoldersWithoutDynamicOmission()
    {
        var path1 = @"C:\dt\multishell\Services";
        var path2 = @"C:\dt\multishell\Core";

        var stripes1 = _service.GetStripesForPath(path1);
        var stripes2 = _service.GetStripesForPath(path2);

        // Both tabs display all their folders: dt, multishell, and subfolder
        Assert.Equal(3, stripes1.Count);
        Assert.Equal("dt", stripes1[0].FolderName);
        Assert.Equal("multishell", stripes1[1].FolderName);
        Assert.Equal("Services", stripes1[2].FolderName);

        Assert.Equal(3, stripes2.Count);
        Assert.Equal("dt", stripes2[0].FolderName);
        Assert.Equal("multishell", stripes2[1].FolderName);
        Assert.Equal("Core", stripes2[2].FolderName);

        // Common structure has identical color
        Assert.Equal(stripes1[0].HexColor, stripes2[0].HexColor);
        Assert.Equal(stripes1[1].HexColor, stripes2[1].HexColor);
    }

    [Fact]
    public void GetStripesForPath_ParentAndChildTab_ShowsAllFoldersStably()
    {
        // Tab 1 is in C:\dt\multishell
        // Tab 2 is in C:\dt\multishell\Services
        var path1 = @"C:\dt\multishell";
        var path2 = @"C:\dt\multishell\Services";

        var stripes1 = _service.GetStripesForPath(path1);
        var stripes2 = _service.GetStripesForPath(path2);

        // Tab 1 shows dt, multishell
        Assert.Equal(2, stripes1.Count);
        Assert.Equal("dt", stripes1[0].FolderName);
        Assert.Equal("multishell", stripes1[1].FolderName);

        // Tab 2 shows dt, multishell, Services
        Assert.Equal(3, stripes2.Count);
        Assert.Equal("dt", stripes2[0].FolderName);
        Assert.Equal("multishell", stripes2[1].FolderName);
        Assert.Equal("Services", stripes2[2].FolderName);

        // Shared level colors match between both tabs
        Assert.Equal(stripes1[0].HexColor, stripes2[0].HexColor);
        Assert.Equal(stripes1[1].HexColor, stripes2[1].HexColor);
    }

    [Fact]
    public void GetStripesForPath_SingleTabOpen_ShowsAllFolders()
    {
        var path = @"C:\dt\multishell\Services";
        var stripes = _service.GetStripesForPath(path);

        // Shows all folder segments: dt, multishell, Services
        Assert.Equal(3, stripes.Count);
        Assert.Equal("dt", stripes[0].FolderName);
        Assert.Equal("multishell", stripes[1].FolderName);
        Assert.Equal("Services", stripes[2].FolderName);
    }

    [Fact]
    public void GetColorForFolderName_DeterministicAndCaseInsensitive()
    {
        var color1 = _service.GetColorForFolderName("multishell");
        var color2 = _service.GetColorForFolderName("MultiShell");
        var color3 = _service.GetColorForFolderName("MULTISHELL");

        Assert.Equal(color1, color2);
        Assert.Equal(color2, color3);

        // Different folder names should produce different colors
        var otherColor = _service.GetColorForFolderName("Services");
        Assert.NotEqual(color1, otherColor);
    }

    [Fact]
    public void GetColorForFolderName_EmptyOrWhitespace_ReturnsNeutralColor()
    {
        var color = _service.GetColorForFolderName("   ");
        Assert.Equal(Color.FromRgb(128, 128, 128), color);
    }

    [Fact]
    public void GetStripesForPath_MaxStripes_CapsAtMaximum()
    {
        var path1 = @"C:\dt\a\b\c\d\e\f";
        var path2 = @"C:\dt\other";
        var allPaths = new[] { path1, path2 };

        var stripes = _service.GetStripesForPath(path1, allPaths);

        Assert.True(stripes.Count <= PathColorCodingService.MaxVisibleStripes);
    }

    [Fact]
    public void PathColorStripe_DefaultDimensions_WidthIsNineAndHeightIsThree()
    {
        var stripe = new MultiShell.Models.PathColorStripe("multishell", "#6496C8");

        Assert.Equal(3.0, stripe.Height);
        Assert.Equal(9.0, stripe.Width);
    }

    [Fact]
    public void GetStripesForPath_AppliesPadovanMultipliersToWidths()
    {
        var path = @"C:\dt\projectA\sub1\sub2";

        var stripes = _service.GetStripesForPath(path);

        // Levels: dt (level 0), projectA (level 1), sub1 (level 2), sub2 (level 3)
        Assert.Equal(4, stripes.Count);
        Assert.Equal("dt", stripes[0].FolderName);
        Assert.Equal("projectA", stripes[1].FolderName);
        Assert.Equal("sub1", stripes[2].FolderName);
        Assert.Equal("sub2", stripes[3].FolderName);

        // Padovan multipliers: 1, 2, 3, 4 -> widths: 9, 18, 27, 36
        Assert.Equal(9.0 * 1, stripes[0].Width);
        Assert.Equal(9.0 * 2, stripes[1].Width);
        Assert.Equal(9.0 * 3, stripes[2].Width);
        Assert.Equal(9.0 * 4, stripes[3].Width);

        // Heights are all 3.0
        Assert.All(stripes, s => Assert.Equal(3.0, s.Height));
    }

    [Fact]
    public void GetStripesForPath_IndependentOfOtherTabs_StripeCountAndColorsNeverChange()
    {
        var targetPath = @"C:\dt\projectA\sub";

        // Tab alone
        var stripesAlone = _service.GetStripesForPath(targetPath, new[] { targetPath });

        // Tab with sibling
        var stripesWithSibling = _service.GetStripesForPath(targetPath, new[] { targetPath, @"C:\dt\projectA\other" });

        // Tab with different drive
        var stripesWithDifferentDrive = _service.GetStripesForPath(targetPath, new[] { targetPath, @"D:\tools" });

        // All should be exactly identical
        Assert.Equal(stripesAlone.Count, stripesWithSibling.Count);
        Assert.Equal(stripesAlone.Count, stripesWithDifferentDrive.Count);

        for (int i = 0; i < stripesAlone.Count; i++)
        {
            Assert.Equal(stripesAlone[i].FolderName, stripesWithSibling[i].FolderName);
            Assert.Equal(stripesAlone[i].HexColor, stripesWithSibling[i].HexColor);
            Assert.Equal(stripesAlone[i].Width, stripesWithSibling[i].Width);

            Assert.Equal(stripesAlone[i].FolderName, stripesWithDifferentDrive[i].FolderName);
            Assert.Equal(stripesAlone[i].HexColor, stripesWithDifferentDrive[i].HexColor);
            Assert.Equal(stripesAlone[i].Width, stripesWithDifferentDrive[i].Width);
        }
    }

    [Fact]
    public void GetStripesForPath_PathWithRelativeSegments_OmitsDotAndDotDot()
    {
        var path = @"C:\dt\projectA\..\projectB\.\sub";
        var stripes = _service.GetStripesForPath(path);

        Assert.DoesNotContain(stripes, s => s.FolderName is "." or "..");
        Assert.Equal(4, stripes.Count);
        Assert.Equal("dt", stripes[0].FolderName);
        Assert.Equal("projectA", stripes[1].FolderName);
        Assert.Equal("projectB", stripes[2].FolderName);
        Assert.Equal("sub", stripes[3].FolderName);
    }
}
