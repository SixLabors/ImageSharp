// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="ClutCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class ClutCalculatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataClut.ClutConversionTestData), MemberType = typeof(IccConversionDataClut))]
    internal void ClutCalculator_WithClut_ReturnsResult(IccClut lut, Vector4 input, Vector4 expected)
    {
        ClutCalculator calculator = new(lut, false);

        Vector4 result = calculator.Calculate(input);

        VectorAssert.Equal(expected, result, 4);
    }

    [Theory]
    [InlineData(0.75F, 0.5F, 0.25F, 0.25F)]
    [InlineData(0.75F, 0.25F, 0.5F, 0.25F)]
    [InlineData(0.5F, 0.25F, 0.75F, 0.25F)]
    [InlineData(0.5F, 0.75F, 0.25F, 0.25F)]
    [InlineData(0.25F, 0.75F, 0.5F, 0.25F)]
    [InlineData(0.25F, 0.5F, 0.75F, 0.25F)]
    [InlineData(0.5F, 0.5F, 0.25F, 0.25F)]
    [InlineData(0.25F, 0.5F, 0.5F, 0.25F)]
    [InlineData(0.5F, 0.25F, 0.5F, 0.25F)]
    [InlineData(0.5F, 0.5F, 0.5F, 0.5F)]
    [InlineData(0F, 0F, 0F, 0F)]
    [InlineData(1F, 1F, 1F, 1F)]
    [InlineData(-0.25F, 0.5F, 0.75F, 0F)]
    [InlineData(1.25F, 0.5F, 0.75F, 0.5F)]
    public void ThreeChannelsInterpolateTetrahedra(float x, float y, float z, float expected)
    {
        // Only the upper corner is nonzero. Its tetrahedral weight is the smallest
        // coordinate, whereas trilinear interpolation would multiply all three.
        // Complementary output channels also detect incorrect table strides.
        IccClut table = new(
            [0F, 1F, 0F, 1F, 0F, 1F, 0F, 1F, 0F, 1F, 0F, 1F, 0F, 1F, 1F, 0F],
            [2, 2, 2],
            IccClutDataType.Float,
            2);

        ClutCalculator calculator = new(table, false);
        Vector4 actual = calculator.Calculate(new Vector4(x, y, z, 0F));
        Assert.Equal(new Vector4(expected, 1F - expected, 0F, 0F), actual);
    }

    [Theory]
    [InlineData(0F, 0.25F)]
    [InlineData(0.5F, 0.3125F)]
    [InlineData(1F, 0.375F)]
    [InlineData(-0.5F, 0.25F)]
    [InlineData(1.5F, 0.375F)]
    public void FourChannelsBlendTetrahedralSlices(float first, float expected)
    {
        // The lower slice evaluates to min(x,y,z); the upper slice evaluates to
        // 0.25 + 0.5 * min(x,y,z). The fourth-dimensional blend is independently
        // determined by the first coordinate, including clipping at either boundary.
        IccClut table = new(
            [0F, 0F, 0F, 0F, 0F, 0F, 0F, 1F, 0.25F, 0.25F, 0.25F, 0.25F, 0.25F, 0.25F, 0.25F, 0.75F],
            [2, 2, 2, 2],
            IccClutDataType.Float,
            1);

        ClutCalculator calculator = new(table, false);
        Vector4 actual = calculator.Calculate(new Vector4(first, 0.75F, 0.5F, 0.25F));
        Assert.Equal(new Vector4(expected, 0F, 0F, 0F), actual);
    }

    [Theory]
    [InlineData(0.25F, 0.25F)]
    [InlineData(0.75F, 0.5F)]
    public void ThreeChannelsUseUnequalGridStrides(float y, float expected)
    {
        // The middle axis has two cells while the other axes have one. Each cell
        // has a different upper value, exposing both incorrect strides and offsets.
        IccClut table = new(
            [0F, 0F, 0F, 0F, 0F, 0F, 0F, 0F, 0F, 0.5F, 0F, 1F],
            [2, 3, 2],
            IccClutDataType.Float,
            1);

        ClutCalculator calculator = new(table, false);
        Vector4 actual = calculator.Calculate(new Vector4(0.75F, y, 0.5F, 0F));
        Assert.Equal(new Vector4(expected, 0F, 0F, 0F), actual);
    }

    [Fact]
    public void ThreeChannelsUseTrilinearWhenSelected()
    {
        // Independent-axis interpolation gives the upper corner the product of
        // the fractions, rather than the minimum used by a tetrahedral table.
        IccClut table = new([0F, 0F, 0F, 0F, 0F, 0F, 0F, 1F], [2, 2, 2], IccClutDataType.Float, 1);
        ClutCalculator calculator = new(table, true);
        Vector4 actual = calculator.Calculate(new Vector4(0.75F, 0.5F, 0.25F, 0F));
        Assert.Equal(new Vector4(0.09375F, 0F, 0F, 0F), actual);
    }
}
