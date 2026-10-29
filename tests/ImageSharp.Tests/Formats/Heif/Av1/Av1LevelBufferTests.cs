// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1LevelBufferTests
{
    private const HwIntrinsics LevelConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    [Fact]
    public void InitializeStoresAbsoluteSaturatedLevels()
    {
        // Arrange: the smallest coded transform is four coefficients wide.
        using Av1LevelBuffer levels = new(Configuration.Default, new Size(4, 4));
        Span<int> coefficients = [-300, -1, 1, 300, 0, 2, -2, 0, 0, 0, 0, 0, 0, 0, 0, -127];

        // Act
        levels.Initialize(coefficients);

        // Assert
        Assert.Equal([127, 1, 1, 127], levels.GetRow(0)[..4].ToArray());
        Assert.Equal([0, 2, 2, 0], levels.GetRow(1)[..4].ToArray());
        Assert.Equal([0, 0, 0, 127], levels.GetRow(3)[..4].ToArray());
    }

    /// <summary>
    /// Verifies that every register width stores the saturated magnitude of each coefficient and zero padding to the
    /// right of each row and below the plane, for every coded transform width and height.
    /// </summary>
    [Fact]
    public void InitializeMatchesDefinitionAtEveryRegisterWidth()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateAllSizes, LevelConfigurations);

    private static void ValidateAllSizes()
    {
        for (int widthLog2 = 2; widthLog2 <= 5; widthLog2++)
        {
            for (int heightLog2 = 2; heightLog2 <= 5; heightLog2++)
            {
                int width = 1 << widthLog2;
                int height = 1 << heightLog2;
                int[] coefficients = new int[width * height];
                for (int i = 0; i < coefficients.Length; i++)
                {
                    // Signed values on both sides of the clamp, including zero and values beyond the sixteen-bit range.
                    coefficients[i] = (i % 17) == 3 ? -40000 : (i % 17) == 5 ? 40000 : ((i * 37) % 301) - 150;
                }

                using Av1LevelBuffer levels = new(Configuration.Default, new Size(width, height));
                levels.Initialize(coefficients);
                Span<byte> active = levels.GetActiveLevels();
                for (int row = 0; row < height + Av1Constants.TransformPadBottom; row++)
                {
                    for (int column = 0; column < levels.Stride; column++)
                    {
                        int expected = row < height && column < width
                            ? Math.Min(Math.Abs(coefficients[(row * width) + column]), 127)
                            : 0;

                        Assert.Equal(expected, active[(row * levels.Stride) + column]);
                    }
                }
            }
        }
    }
}
