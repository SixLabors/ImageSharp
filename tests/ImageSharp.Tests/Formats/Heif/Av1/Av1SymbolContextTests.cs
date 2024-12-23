// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.Diagnostics.Symbols;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
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
        int transformType = Av1SymbolContextHelper.ExtendedTransformIndicesInverse[(int)transformSetType][index];
        int actualIndex = Av1SymbolContextHelper.ExtendedTransformIndices[(int)transformSetType][transformType];

        // Assert
        Assert.Equal(actualIndex, index);
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
        for (Av1TransformSetType setType = Av1TransformSetType.DctOnly; setType <= Av1TransformSetType.All16; setType++)
        {
            int count = Av1SymbolContextHelper.GetExtendedTransformTypeCount(setType);
            for (int type = 1; type < count; type++)
            {
                result.Add((int)setType, type);
            }
        }

        return result;
    }

    /// <summary>
    /// SVT: get_lower_levels_ctx_eob
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
