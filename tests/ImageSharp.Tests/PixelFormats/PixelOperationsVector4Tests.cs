// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

/// <summary>
/// Verifies the shared bulk arithmetic used by affine pixel-vector conversions.
/// </summary>
[Trait("Category", "PixelFormats")]
public class PixelOperationsVector4Tests
{
    private static readonly int[] Lengths = [0, 1, 2, 3, 4, 5, 7, 8, 15, 16, 17, 257];

    /// <summary>
    /// Verifies multiply-then-add behavior for every SIMD boundary and the software fallback.
    /// </summary>
    [Fact]
    public void MultiplyThenAddMatchesComponentArithmeticAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertMultiplyThenAddMatchesComponentArithmetic,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies add-then-divide behavior for every SIMD boundary and the software fallback.
    /// </summary>
    [Fact]
    public void AddThenDivideMatchesComponentArithmeticAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertAddThenDivideMatchesComponentArithmetic,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares the multiply-then-add traversal with the runtime's scalar estimate at every component.
    /// </summary>
    private static void AssertMultiplyThenAddMatchesComponentArithmetic()
    {
        // These are the native-range mappings used by Byte4, signed-normalized pixels and Short4.
        Vector4 multiplier = new(255F, 2F, 65535F, 2F);
        Vector4 offset = new(0F, -1F, -32768F, -1F);

        foreach (int length in Lengths)
        {
            Vector4[] actual = CreateSource(length);
            Vector4[] expected = new Vector4[length];

            for (int i = 0; i < expected.Length; i++)
            {
                Vector4 value = actual[i];

                expected[i] = new Vector4(
                    float.MultiplyAddEstimate(value.X, multiplier.X, offset.X),
                    float.MultiplyAddEstimate(value.Y, multiplier.Y, offset.Y),
                    float.MultiplyAddEstimate(value.Z, multiplier.Z, offset.Z),
                    float.MultiplyAddEstimate(value.W, multiplier.W, offset.W));
            }

            global::SixLabors.ImageSharp.PixelFormats.PixelOperations.MultiplyThenAdd(actual, multiplier, offset);

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// Compares the add-then-divide traversal with independently evaluated component expressions.
    /// </summary>
    private static void AssertAddThenDivideMatchesComponentArithmetic()
    {
        Vector4 offset = new(-7F, 11F, 13F, -17F);
        Vector4 divisor = new(2F, -3F, .5F, 4F);

        foreach (int length in Lengths)
        {
            Vector4[] actual = CreateSource(length);
            Vector4[] expected = new Vector4[length];

            for (int i = 0; i < expected.Length; i++)
            {
                Vector4 value = actual[i];

                expected[i] = new Vector4(
                    (value.X + offset.X) / divisor.X,
                    (value.Y + offset.Y) / divisor.Y,
                    (value.Z + offset.Z) / divisor.Z,
                    (value.W + offset.W) / divisor.W);
            }

            global::SixLabors.ImageSharp.PixelFormats.PixelOperations.AddThenDivide(actual, offset, divisor);

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// Creates non-uniform values that expose component ordering and traversal overlap errors.
    /// </summary>
    /// <param name="length">The number of vectors to create.</param>
    /// <returns>The populated vector buffer.</returns>
    private static Vector4[] CreateSource(int length)
    {
        Vector4[] result = new Vector4[length];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new Vector4((i * 17F) - 31F, (i * -23F) + 37F, (i * .25F) - 41F, (i * 3F) + 43F);
        }

        return result;
    }
}
