// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Common;

/// <summary>
/// Tests shared integer sequence generation.
/// </summary>
public partial class SimdUtilsTests
{
    /// <summary>
    /// Checks consecutive values, integer wraparound, scalar tails and destination guards across all supported SIMD widths.
    /// </summary>
    [Fact]
    public void FillSequence_FillsOnlyTheDestination() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        ValidateSequence,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks empty, complete-register and partial-register spans for each production integer type.
    /// </summary>
    private static void ValidateSequence()
    {
        ReadOnlySpan<int> lengths = [0, 1, 3, 4, 7, 8, 15, 16, 17, 31, 32, 33, 63, 64, 65, 129];
        foreach (int length in lengths)
        {
            byte[] bytes = new byte[length + 2];
            int[] integers = new int[length + 2];
            uint[] unsigned = new uint[length + 2];
            bytes.AsSpan().Fill(123);
            integers.AsSpan().Fill(123);
            unsigned.AsSpan().Fill(123);

            SimdUtils.FillSequence(bytes.AsSpan(1, length), (byte)250);
            SimdUtils.FillSequence(integers.AsSpan(1, length), int.MaxValue - 9);
            SimdUtils.FillSequence(unsigned.AsSpan(1, length), uint.MaxValue - 9);

            // Check every value against scalar integer arithmetic. Guard values also detect writes to an empty destination.
            for (int i = 0; i < length; i++)
            {
                Assert.Equal(unchecked((byte)(250 + i)), bytes[i + 1]);
                Assert.Equal(unchecked(int.MaxValue - 9 + i), integers[i + 1]);
                Assert.Equal(unchecked(uint.MaxValue - 9 + (uint)i), unsigned[i + 1]);
            }

            Assert.Equal((byte)123, bytes[0]);
            Assert.Equal((byte)123, bytes[^1]);
            Assert.Equal(123, integers[0]);
            Assert.Equal(123, integers[^1]);
            Assert.Equal(123U, unsigned[0]);
            Assert.Equal(123U, unsigned[^1]);
        }
    }
}
