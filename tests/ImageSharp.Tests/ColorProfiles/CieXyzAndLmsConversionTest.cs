// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="Lms"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using original colorful library.
/// </remarks>
[Trait("Color", "Conversion")]
public class CieXyzAndLmsConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001f);

    [Theory]
    [InlineData(0.941428535, 1.040417467, 1.089532651, 0.95047, 1, 1.08883)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.850765697, -0.713042594, 0.036973283, 0.95047, 0, 0)]
    [InlineData(0.2664, 1.7135, -0.0685, 0, 1, 0)]
    [InlineData(-0.175737162, 0.039960061, 1.121059368, 0, 0, 1.08883)]
    [InlineData(0.2262677362, 0.0961411609, 0.0484570397, 0.216938, 0.150041, 0.048850)]
    public void Convert_Lms_To_CieXyz(float l, float m, float s, float x, float y, float z)
    {
        // Arrange
        Lms input = new(l, m, s);
        ColorProfileConverter converter = new();
        CieXyz expected = new(x, y, z);

        Span<Lms> inputSpan = new Lms[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<Lms, CieXyz>(input);
        converter.Convert<Lms, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0.95047, 1, 1.08883, 0.941428535, 1.040417467, 1.089532651)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.95047, 0, 0, 0.850765697, -0.713042594, 0.036973283)]
    [InlineData(0, 1, 0, 0.2664, 1.7135, -0.0685)]
    [InlineData(0, 0, 1.08883, -0.175737162, 0.039960061, 1.121059368)]
    [InlineData(0.216938, 0.150041, 0.048850, 0.2262677362, 0.0961411609, 0.0484570397)]
    public void Convert_CieXyz_To_Lms(float x, float y, float z, float l, float m, float s)
    {
        // Arrange
        CieXyz input = new(x, y, z);
        ColorProfileConverter converter = new();
        Lms expected = new(l, m, s);

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<Lms> actualSpan = new Lms[5];

        // Act
        Lms actual = converter.Convert<CieXyz, Lms>(input);
        converter.Convert<CieXyz, Lms>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
