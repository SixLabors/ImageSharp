// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the sample-width kernels of the intra block encoder operators.
/// </summary>
[Trait("Format", "Avif")]
public class Av1IntraBlockOperatorTests
{
    /// <summary>
    /// The hardware configurations covering every vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies every seed against get_identity_hash_value() and get_xor_hash_value_hbd(), for row lengths that end
    /// in every vector tail.
    /// </summary>
    [Fact]
    public void SeedRowsMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertSeedRows, Configurations);

    private static void AssertSeedRows()
    {
        int seed = 3;
        foreach (int length in new[] { 1, 3, 4, 7, 8, 15, 16, 17, 31, 32, 33, 47, 63, 64, 65, 333, 1919 })
        {
            byte[] top = new byte[length + 1];
            byte[] bottom = new byte[length + 1];
            ushort[] wideTop = new ushort[length + 1];
            ushort[] wideBottom = new ushort[length + 1];
            for (int i = 0; i <= length; i++)
            {
                top[i] = (byte)Next(ref seed);
                bottom[i] = (byte)Next(ref seed);
                wideTop[i] = (ushort)Next(ref seed);
                wideBottom[i] = (ushort)(Next(ref seed) & 4095);
            }

            uint[] seeds = new uint[length];
            Av1IntraBlockCopySearchIndex.PackSeedRow<byte, Av1IntraSuperblockEncoder.ByteOperator>(top, bottom, seeds);
            for (int x = 0; x < length; x++)
            {
                uint expected = ((uint)top[x] << 24) + ((uint)top[x + 1] << 16) + ((uint)bottom[x] << 8) + bottom[x + 1];
                Assert.Equal(expected, seeds[x]);
            }

            Av1IntraBlockCopySearchIndex.PackSeedRow<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(wideTop, wideBottom, seeds);
            for (int x = 0; x < length; x++)
            {
                uint a = wideTop[x];
                uint b = wideTop[x + 1];
                uint c = wideBottom[x];
                uint d = wideBottom[x + 1];
                uint expected = ((a & 0xFF) << 24) + ((b & 0xFF) << 16) + ((c & 0xFF) << 8) + (d & 0xFF);
                expected ^= ((a & 0xFF00) << 16) + ((b & 0xFF00) << 8) + (c & 0xFF00) + ((d & 0xFF00) >> 8);
                Assert.Equal(expected, seeds[x]);
            }
        }
    }

    /// <summary>
    /// Verifies the 8x8 block equality and the rounded 4x4 average against their per-sample definitions.
    /// </summary>
    [Fact]
    public void BlockEqualityAndAveragesMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertBlockEqualityAndAverages, Configurations);

    private static void AssertBlockEqualityAndAverages()
    {
        const int size = 24;
        byte[] samples = new byte[size * size];
        ushort[] wideSamples = new ushort[size * size];
        int seed = 5;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)(Next(ref seed) & 3);
            wideSamples[i] = (ushort)(Next(ref seed) & 4095);
        }

        // Repeat one block so equal pairs occur, then change one sample of a copy for a near miss.
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                samples[((y + 16) * size) + x + 16] = samples[(y * size) + x];
                wideSamples[((y + 16) * size) + x + 16] = wideSamples[(y * size) + x];
                samples[((y + 8) * size) + x + 16] = samples[(y * size) + x];
                wideSamples[((y + 8) * size) + x + 16] = wideSamples[(y * size) + x];
            }
        }

        samples[(15 * size) + 23]++;
        wideSamples[(15 * size) + 23]++;

        Av1PlaneRegion<byte> plane = new(samples, size, new Rectangle(0, 0, size, size));
        Av1PlaneRegion<ushort> widePlane = new(wideSamples, size, new Rectangle(0, 0, size, size));
        for (int y = 0; y <= size - 8; y++)
        {
            for (int x = 0; x <= size - 8; x++)
            {
                Point first = new(0, 0);
                Point second = new(x, y);
                Assert.Equal(
                    BlocksEqualReference(samples, size, first, second),
                    Av1IntraSuperblockEncoder.ByteOperator.BlocksEqual(plane, first, second));

                Assert.Equal(
                    BlocksEqualReference(wideSamples, size, first, second),
                    Av1IntraSuperblockEncoder.UInt16Operator.BlocksEqual(widePlane, first, second));
            }
        }

        Assert.True(Av1IntraSuperblockEncoder.ByteOperator.BlocksEqual(plane, new Point(0, 0), new Point(16, 16)));
        Assert.False(Av1IntraSuperblockEncoder.ByteOperator.BlocksEqual(plane, new Point(0, 0), new Point(16, 8)));
        Assert.False(Av1IntraSuperblockEncoder.UInt16Operator.BlocksEqual(widePlane, new Point(0, 0), new Point(16, 8)));

        for (int y = 0; y <= size - 4; y++)
        {
            for (int x = 0; x <= size - 4; x++)
            {
                int sum = 0;
                int wideSum = 0;
                for (int row = 0; row < 4; row++)
                {
                    for (int column = 0; column < 4; column++)
                    {
                        sum += samples[((y + row) * size) + x + column];
                        wideSum += wideSamples[((y + row) * size) + x + column];
                    }
                }

                Assert.Equal((sum + 8) >> 4, Av1IntraSuperblockEncoder.ByteOperator.GetAverage4x4(plane, new Point(x, y)));
                Assert.Equal((wideSum + 8) >> 4, Av1IntraSuperblockEncoder.UInt16Operator.GetAverage4x4(widePlane, new Point(x, y)));
            }
        }
    }

    private static bool BlocksEqualReference<T>(T[] samples, int stride, Point first, Point second)
        where T : IEquatable<T>
    {
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                if (!samples[((first.Y + y) * stride) + first.X + x].Equals(samples[((second.Y + y) * stride) + second.X + x]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int Next(ref int seed)
    {
        seed = (seed * 1103515245) + 12345;
        return (seed >>> 8) & 0xFFFF;
    }
}
