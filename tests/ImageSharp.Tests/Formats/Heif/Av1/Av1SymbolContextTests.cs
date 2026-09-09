// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.Diagnostics.Symbols;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1SymbolContextTests
{
    [Theory]
    [MemberData(nameof(GetLowLevelContextEndOfBlockData))]
    public void TestLowLevelContextEndOfBlockAccuracy(int width, int height, int index)
    {
        // Arrange
        Size size = new(width, height);
        Av1LevelBuffer levels = new(Configuration.Default, size);
        Point position = levels.GetPosition(index);
        int blockWidthLog2 = Av1Math.Log2(width);
        int expectedContext = GetExpectedLowerLevelContextEndOfBlock(blockWidthLog2, height, index);

        // Act
        int actualContext = Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(levels, position);

        // Assert
        Assert.Equal(expectedContext, actualContext);
    }

    [Theory]
    [MemberData(nameof(GetExtendedTransformIndicesData))]
    public void RoundTripExtendedTransformIndices(int setType, int index)
    {
        // Arrange
        Av1TransformSetType transformSetType = (Av1TransformSetType)setType;

        // Act
        Av1TransformType transformType = Av1SymbolContextHelper.GetExtendedTransformType(transformSetType, index);
        int actualIndex = Av1SymbolContextHelper.GetExtendedTransformIndex(transformSetType, transformType);

        // Assert
        Assert.Equal(actualIndex, index);
    }

    [Theory]
    [InlineData((int)Av1PredictionMode.DC, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1PredictionMode.Vertical, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.AdstDct)]
    [InlineData((int)Av1PredictionMode.Horizontal, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.DctAdst)]
    [InlineData((int)Av1PredictionMode.Directional45Degrees, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.AdstAdst)]
    [InlineData((int)Av1PredictionMode.Directional113Degrees, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.AdstDct)]
    [InlineData((int)Av1PredictionMode.Directional157Degrees, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.DctAdst)]
    [InlineData((int)Av1PredictionMode.Directional203Degrees, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.DctAdst)]
    [InlineData((int)Av1PredictionMode.Directional67Degrees, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.AdstDct)]
    [InlineData((int)Av1PredictionMode.Smooth, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.AdstAdst)]
    [InlineData((int)Av1PredictionMode.SmoothVertical, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.AdstDct)]
    [InlineData((int)Av1PredictionMode.SmoothHorizontal, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.DctAdst)]
    [InlineData((int)Av1PredictionMode.Paeth, (int)Av1TransformSize.Size8x8, false, (int)Av1TransformType.AdstAdst)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, (int)Av1TransformSize.Size8x8, true, (int)Av1TransformType.AdstAdst)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, (int)Av1TransformSize.Size32x32, false, (int)Av1TransformType.DctDct)]
    public void DefaultIntraTransformTypeMatchesCurrentLibaom(
        int modeValue,
        int transformSizeValue,
        bool useReducedSet,
        int expectedValue)
    {
        Av1TransformType actual = Av1SymbolContextHelper.GetDefaultIntraTransformType(
            (Av1PredictionMode)modeValue,
            (Av1TransformSize)transformSizeValue,
            useReducedSet);

        Assert.Equal((Av1TransformType)expectedValue, actual);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 4, 12)]
    [InlineData(1, 4, 20)]
    public void CoefficientContextMatchesCurrentLibaom(int dcCoefficient, ushort endOfBlock, byte expected)
    {
        Span<int> coefficients = stackalloc int[16];
        coefficients.Fill(1);
        coefficients[0] = dcCoefficient;

        byte actual = Av1SymbolContextHelper.GetCoefficientContext(
            coefficients,
            Av1TransformSize.Size4x4,
            Av1TransformType.DctDct,
            endOfBlock);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(8, 8, (int)Av1BlockSize.Block8x8, (int)Av1TransformSize.Size8x8, 18)]
    [InlineData(4, 8, (int)Av1BlockSize.Block8x8, (int)Av1TransformSize.Size8x8, 19)]
    [InlineData(4, 4, (int)Av1BlockSize.Block8x8, (int)Av1TransformSize.Size8x8, 20)]
    [InlineData(4, 4, (int)Av1BlockSize.Block16x16, (int)Av1TransformSize.Size8x8, 17)]
    [InlineData(0, 0, (int)Av1BlockSize.Block8x8, (int)Av1TransformSize.Size4x4, 0)]
    public void TransformPartitionContextMatchesCurrentLibaom(
        byte aboveWidth,
        byte leftHeight,
        int blockSizeValue,
        int transformSizeValue,
        int expected)
    {
        int actual = Av1SymbolContextHelper.GetTransformPartitionContext(
            aboveWidth,
            leftHeight,
            (Av1BlockSize)blockSizeValue,
            (Av1TransformSize)transformSizeValue);

        Assert.Equal(expected, actual);
    }

    public static TheoryData<int, int, int> GetLowLevelContextEndOfBlockData()
    {
        TheoryData<int, int, int> result = [];
        for (int y = 1; y < 6; y++)
        {
            for (int x = 1; x < 6; x++)
            {
                int total = (1 << x) * (1 << y);
                for (int i = 0; i < total; i++)
                {
                    result.Add(1 << x, 1 << y, i);
                }
            }
        }

        return result;
    }

    public static TheoryData<int, int> GetExtendedTransformIndicesData()
    {
        TheoryData<int, int> result = [];
        for (Av1TransformSetType setType = Av1TransformSetType.DctOnly; setType < Av1TransformSetType.AllSets; setType++)
        {
            int count = Av1SymbolContextHelper.GetExtendedTransformTypeCount(setType);
            for (int index = 0; index < count; index++)
            {
                result.Add((int)setType, index);
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the expected lower-level coefficient context at the end of a transform block.
    /// </summary>
    internal static int GetExpectedLowerLevelContextEndOfBlock(int blockWidthLog2, int height, int scanIndex)
    {
        if (scanIndex == 0)
        {
            return 0;
        }

        if (scanIndex <= height << blockWidthLog2 >> 3)
        {
            return 1;
        }

        if (scanIndex <= height << blockWidthLog2 >> 2)
        {
            return 2;
        }

        return 3;
    }
}
