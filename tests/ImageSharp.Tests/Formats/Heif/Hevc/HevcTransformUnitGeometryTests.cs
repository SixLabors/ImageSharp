// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC transform-tree component geometry across chroma sampling layouts.
/// </summary>
[Trait("Format", "Heic")]
public class HevcTransformUnitGeometryTests
{
    /// <summary>
    /// Verifies that a sub-minimum 4:2:0 chroma block is retained and processed with the final luma quadrant.
    /// </summary>
    [Fact]
    public void Chroma420RetainsMinimumBlockAtFinalLumaQuadrant()
    {
        HevcTransformUnitGeometry root = HevcTransformUnitGeometry.CreateRoot(16, 24, 3, 1, false, 0);

        for (int childIndex = 0; childIndex < 3; childIndex++)
        {
            HevcTransformUnitGeometry child = root.CreateChild(childIndex);
            Assert.False(child.ChromaBlue.Process);
        }

        HevcTransformComponentGeometry chroma = root.CreateChild(3).ChromaBlue;
        Assert.True(chroma.Process);
        Assert.False(chroma.ProcessesAllQuadrants);
        Assert.Equal(8, chroma.X);
        Assert.Equal(12, chroma.Y);
        Assert.Equal(4, chroma.Width);
        Assert.Equal(4, chroma.Height);
    }

    /// <summary>
    /// Verifies that a 4:2:2 rectangular chroma transform is retained for two vertical four-by-four coefficient blocks.
    /// </summary>
    [Fact]
    public void Chroma422RetainsVerticalSubTransformsAtFinalLumaQuadrant()
    {
        HevcTransformUnitGeometry root = HevcTransformUnitGeometry.CreateRoot(16, 24, 3, 2, false, 0);

        for (int childIndex = 0; childIndex < 3; childIndex++)
        {
            Assert.False(root.CreateChild(childIndex).ChromaBlue.Process);
        }

        HevcTransformComponentGeometry chroma = root.CreateChild(3).ChromaBlue;
        Assert.True(chroma.Process);
        Assert.False(chroma.ProcessesAllQuadrants);
        Assert.Equal(8, chroma.X);
        Assert.Equal(24, chroma.Y);
        Assert.Equal(4, chroma.Width);
        Assert.Equal(8, chroma.Height);
    }

    /// <summary>
    /// Verifies that separate 4:4:4 planes retain full-resolution primary geometry and omit combined chroma syntax.
    /// </summary>
    [Fact]
    public void SeparateColorPlaneUsesFullResolutionPrimaryGeometry()
    {
        HevcTransformUnitGeometry root = HevcTransformUnitGeometry.CreateRoot(16, 24, 5, 3, true, 2);

        Assert.Equal(HevcPlane.Cr, root.PrimaryPlane);
        Assert.Equal(16, root.Primary.X);
        Assert.Equal(24, root.Primary.Y);
        Assert.Equal(32, root.Primary.Width);
        Assert.Equal(32, root.Primary.Height);
        Assert.False(root.HasCombinedChroma);
    }
}
