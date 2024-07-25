// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1ScanOrderTests
{
    [Theory]
    [MemberData(nameof(GetCombinations))]
    internal void AllIndicesScannedExactlyOnce(int s, int t)
    {
        // Assign
        HashSet<short> visitedScans = [];
        Av1TransformSize transformSize = (Av1TransformSize)s;
        Av1TransformType transformType = (Av1TransformType)t;

        // Act
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType);

        // Assert
        foreach (short scan in scanOrder.Scan)
        {
            Assert.False(visitedScans.Contains(scan), $"Scan {scan} already visited before.");
            visitedScans.Add(scan);
        }
    }

    [Theory]
    [MemberData(nameof(GetCombinations))]
    internal void AllIndicesScannedAreWithinRange(int s, int t)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)s;
        Av1TransformType transformType = (Av1TransformType)t;
        int lowValue = 0;

        // Act
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType);
        int highValue = scanOrder.Scan.Length - 1;

        // Assert
        foreach (short scan in scanOrder.Scan)
        {
            Assert.InRange(scan, lowValue, highValue);
        }
    }

    [Theory]
    [MemberData(nameof(GetCombinations))]
    internal void CorrectNumberOfIndicesScanned(int s, int t)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)s;
        Av1TransformType transformType = (Av1TransformType)t;
        int width = Math.Min(transformSize.GetWidth(), 32);
        int height = Math.Min(transformSize.GetHeight(), 32);

        // Act
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType);

        // Assert
        Assert.Equal(width * height, scanOrder.Scan.Length);
    }

    public static TheoryData<int, int> GetCombinations()
    {
        TheoryData<int, int> combinations = [];
        for (int s = 0; s < (int)Av1TransformSize.AllSizes; s++)
        {
            for (int t = 0; t < (int)Av1TransformType.AllTransformTypes; t++)
            {
                combinations.Add(s, t);
            }
        }

        return combinations;
    }
}
