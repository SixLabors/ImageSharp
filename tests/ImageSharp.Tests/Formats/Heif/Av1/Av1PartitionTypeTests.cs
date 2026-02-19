// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1PartitionTypeTests
{
    [Theory]
    [MemberData(nameof(GetAllCombinations))]
    internal void GetSubBlockSizeReturnsCorrectRatio(int t, int s)
    {
        // Assign
        Av1PartitionType partitionType = (Av1PartitionType)t;
        Av1BlockSize blockSize = (Av1BlockSize)s;
        int expectedRatio = partitionType switch
        {
            Av1PartitionType.None or Av1PartitionType.Split => 1,
            Av1PartitionType.HorizontalA or Av1PartitionType.HorizontalB or Av1PartitionType.Horizontal => 2,
            Av1PartitionType.VerticalA or Av1PartitionType.VerticalB or Av1PartitionType.Vertical => -2,
            Av1PartitionType.Horizontal4 => 4,
            Av1PartitionType.Vertical4 => -4,
            _ => -1
        };

        // Act
        Av1BlockSize subBlockSize = partitionType.GetBlockSubSize(blockSize);

        // Assert
        if (subBlockSize != Av1BlockSize.Invalid)
        {
            int actualRatio = GetRatio(subBlockSize);
            Assert.Equal(expectedRatio, actualRatio);
        }
    }

    public static TheoryData<int, int> GetAllCombinations()
    {
        TheoryData<int, int> combinations = [];
        for (int t = 0; t <= (int)Av1PartitionType.Vertical4; t++)
        {
            for (int s = 0; s < (int)Av1BlockSize.AllSizes; s++)
            {
                combinations.Add(t, s);
            }
        }

        return combinations;
    }

    private static int GetRatio(Av1BlockSize blockSize)
    {
        int width = blockSize.GetWidth();
        int height = blockSize.GetHeight();
        int ratio = width >= height ? width / height : -height / width;
        return ratio;
    }
}
