// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

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

    /// <summary>
    /// Checks both public matrix directions across every SIMD width and the scalar remainder.
    /// </summary>
    [Fact]
    public void BulkMatrixConversionsMatchScalar()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifyBulkMatrixConversions,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares mixed packed triples with the existing scalar PCS contract.
    /// </summary>
    private static void VerifyBulkMatrixConversions()
    {
        ReadOnlySpan<Vector3> samples =
        [
            new(0F, -0F, 0F),
            new(.216938F, .150041F, .048850F),
            new(.95047F, 1F, 1.08883F),
            new(-.25F, 1.5F, -2F),
            new(3F, .125F, -.75F),
            new(float.NaN, .5F, .25F),
            new(float.PositiveInfinity, .2F, .3F),
            new(float.NegativeInfinity, 0F, 1F)
        ];

        ColorConversionOptions options = new();
        CieXyz[] xyz = new CieXyz[33];
        Lms[] lms = new Lms[xyz.Length];
        Lms[] actualLms = new Lms[xyz.Length];
        CieXyz[] actualXyz = new CieXyz[xyz.Length];
        for (int i = 0; i < xyz.Length; i++)
        {
            Vector3 sample = samples[i % samples.Length];
            xyz[i] = new CieXyz(sample);
            lms[i] = new Lms(sample);
        }

        // Thirty-three distinct positions span two full 512-bit component groups and one scalar tail.
        // Repeated nonneutral triples expose component swaps; negative and extended values must remain unbounded.
        Lms.FromProfileConnectionSpace(options, xyz, actualLms);
        Lms.ToProfileConnectionSpace(options, lms, actualXyz);
        for (int i = 0; i < xyz.Length; i++)
        {
            Lms expectedLms = Lms.FromProfileConnectingSpace(options, in xyz[i]);
            CieXyz expectedXyz = lms[i].ToProfileConnectingSpace(options);
            if (float.IsFinite(expectedLms.L) && float.IsFinite(expectedLms.M) && float.IsFinite(expectedLms.S))
            {
                Assert.Equal(expectedLms, actualLms[i], Comparer);
            }
            else
            {
                // The existing approximate comparer is for finite values. Compare the scalar NaN/infinity
                // classifications directly, without a new tolerance or a replacement finite-value rule.
                Assert.Equal(expectedLms.L, actualLms[i].L);
                Assert.Equal(expectedLms.M, actualLms[i].M);
                Assert.Equal(expectedLms.S, actualLms[i].S);
            }

            if (float.IsFinite(expectedXyz.X) && float.IsFinite(expectedXyz.Y) && float.IsFinite(expectedXyz.Z))
            {
                Assert.Equal(expectedXyz, actualXyz[i], Comparer);
            }
            else
            {
                Assert.Equal(expectedXyz.X, actualXyz[i].X);
                Assert.Equal(expectedXyz.Y, actualXyz[i].Y);
                Assert.Equal(expectedXyz.Z, actualXyz[i].Z);
            }

            if (i % samples.Length == 0)
            {
                // Signed-zero input must take the same arithmetic path at every width, including the tail.
                Assert.Equal(BitConverter.SingleToInt32Bits(expectedLms.L), BitConverter.SingleToInt32Bits(actualLms[i].L));
                Assert.Equal(BitConverter.SingleToInt32Bits(expectedXyz.X), BitConverter.SingleToInt32Bits(actualXyz[i].X));
            }
        }
    }
}
