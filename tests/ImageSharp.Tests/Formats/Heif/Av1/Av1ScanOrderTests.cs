// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1ScanOrderTests
{
    /// <summary>
    /// Verifies that every scan-order table is a permutation of the coded coefficient indices, whose count is
    /// limited to 32 samples on each axis, for every transform size and type.
    /// </summary>
    [Fact]
    public void ScanOrderTablesArePermutationsOfCodedCoefficients()
    {
        for (int s = 0; s < (int)Av1TransformSize.AllSizes; s++)
        {
            for (int t = 0; t < (int)Av1TransformType.AllTransformTypes; t++)
            {
                Av1TransformSize transformSize = (Av1TransformSize)s;
                int width = Math.Min(transformSize.GetWidth(), 32);
                int height = Math.Min(transformSize.GetHeight(), 32);

                Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, (Av1TransformType)t);

                Assert.Equal(width * height, scanOrder.Scan.Length);
                HashSet<short> visited = [];
                foreach (short scan in scanOrder.Scan)
                {
                    Assert.InRange(scan, 0, scanOrder.Scan.Length - 1);
                    Assert.True(visited.Add(scan), $"Scan {scan} already visited before.");
                }
            }
        }
    }
}
