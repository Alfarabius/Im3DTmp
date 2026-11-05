using NUnit.Framework;
using UnityEngine;

public class DisplayResolutionsTests
{
    private static Resolution Mode(int width, int height, uint hz) => new Resolution
    {
        width = width,
        height = height,
        refreshRateRatio = new RefreshRate { numerator = hz, denominator = 1 }
    };

    [Test]
    public void RefreshRateVariantsProduceOneEntryPerSize()
    {
        var modes = new[] { Mode(2560, 1440, 60), Mode(1920, 1080, 60), Mode(1920, 1080, 144), Mode(2560, 1440, 165) };
        var sizes = DisplayResolutions.GetSizes(modes, new Vector2Int(2560, 1440));
        CollectionAssert.AreEqual(new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) }, sizes);
        Assert.That(DisplayResolutions.IndexOf(sizes, 2560, 1440), Is.EqualTo(1));
    }

    [Test]
    public void CurrentWindowSizeIsRetainedWhenItIsNotAMonitorMode()
    {
        var current = new Vector2Int(1176, 664);
        var sizes = DisplayResolutions.GetSizes(new[] { Mode(1920, 1080, 60) }, current);
        Assert.That(DisplayResolutions.IndexOf(sizes, current.x, current.y), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void MissingModesFallBackToTheCurrentSize()
    {
        CollectionAssert.AreEqual(new[] { new Vector2Int(1920, 1080) },
            DisplayResolutions.GetSizes(null, new Vector2Int(1920, 1080)));
    }

    [Test]
    public void UnsupportedSavedSizeDoesNotSelectTheLowestEntry()
    {
        var sizes = DisplayResolutions.GetSizes(new[] { Mode(1920, 1080, 60) }, new Vector2Int(1920, 1080));
        Assert.That(DisplayResolutions.IndexOf(sizes, 3840, 2160), Is.EqualTo(-1));
    }
}
