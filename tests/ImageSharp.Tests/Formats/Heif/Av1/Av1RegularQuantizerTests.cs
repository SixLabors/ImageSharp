// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies regular quantization's precision, scan position, and buffer boundaries across hardware paths.
/// </summary>
[Trait("Format", "Avif")]
public class Av1RegularQuantizerTests
{
    /// <summary>
    /// Exercises regular quantization under each available vector width and scalar fallback.
    /// </summary>
    [Fact]
    public void RegularQuantizationPreservesCoefficientContracts()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateQuantization,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Exports inputs and results for independent native comparison while checking observable storage contracts.
    /// </summary>
    private static void ValidateQuantization()
    {
        string vectorWidth = Vector512.IsHardwareAccelerated ? "512"
            : Vector256.IsHardwareAccelerated ? "256"
            : Vector128.IsHardwareAccelerated ? "128" : "0";

        string directory = Path.Combine(TestEnvironment.ActualOutputDirectoryFullPath, "Heif", "Av1", "RegularQuantization", vectorWidth);

        Directory.CreateDirectory(directory);
        foreach (Av1BitDepth bitDepth in new[] { Av1BitDepth.EightBit, Av1BitDepth.TenBit, Av1BitDepth.TwelveBit })
        {
            foreach (Av1TransformSize size in new[]
            {
                Av1TransformSize.Size4x4, Av1TransformSize.Size8x8, Av1TransformSize.Size16x16,
                Av1TransformSize.Size32x32, Av1TransformSize.Size64x64, Av1TransformSize.Size8x16
            })
            {
                int count = size.GetAdjusted().GetSize2d();
                int[] input = new int[count + 2];
                int[] quantized = new int[count + 2];
                int[] dequantized = new int[count + 2];
                ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(size, Av1TransformType.DctDct).Scan;
                foreach (int qIndex in new[] { 0, 1, 10, 90, 200, 255 })
                {
                    foreach (int sharpness in new[] { 0, 3, 7 })
                    {
                        for (int pattern = 0; pattern < 4; pattern++)
                        {
                            int dcDelta = pattern == 1 ? -20 : pattern == 2 ? 17 : 0;
                            int acDelta = pattern == 1 ? 13 : pattern == 2 ? -11 : 0;
                            int bits = bitDepth.GetBitCount();
                            int maximum = (1 << (bits + 7)) - 1;
                            uint state = (uint)(qIndex + 123);
                            Array.Fill(input, int.MinValue);
                            Array.Fill(quantized, int.MinValue);
                            Array.Fill(dequantized, int.MinValue);
                            for (int index = 0; index < count; index++)
                            {
                                state = unchecked((state * 1664525) + 1013904223);
                                int magnitude = (int)(state % (uint)maximum);
                                if (pattern == 1)
                                {
                                    int divisor = index == 0
                                        ? Av1QuantizationLookup.GetDcQuant(qIndex, dcDelta, bitDepth)
                                        : Av1QuantizationLookup.GetAcQuant(qIndex, acDelta, bitDepth);

                                    int threshold = ((Av1InverseTransformMath.GetQzbinFactor(qIndex, bitDepth) * divisor) + 64) >> 7;
                                    int scale = size.GetScale();
                                    threshold = scale == 0 ? threshold : (threshold + (1 << (scale - 1))) >> scale;
                                    magnitude = threshold + (index % 3) - 1;
                                }
                                else if (pattern == 2)
                                {
                                    magnitude = maximum;
                                }
                                else if (pattern == 3)
                                {
                                    // A sparse final scan position exposes stale output coefficients across candidate reuse.
                                    magnitude = index == scan[count / 3] ? 97 : 0;
                                }

                                input[index + 1] = (index & 1) == 0 ? magnitude : -magnitude;
                            }

                            int[] original = (int[])input.Clone();
                            ushort end = Av1ForwardQuantizer.QuantizeRegular(
                                input.AsSpan(1, count),
                                quantized.AsSpan(1, count),
                                dequantized.AsSpan(1, count),
                                size,
                                Av1TransformType.DctDct,
                                qIndex,
                                dcDelta,
                                acDelta,
                                bitDepth,
                                sharpness);

                            Assert.Equal(original, input);
                            Assert.Equal(int.MinValue, quantized[0]);
                            Assert.Equal(int.MinValue, quantized[^1]);
                            Assert.Equal(int.MinValue, dequantized[0]);
                            Assert.Equal(int.MinValue, dequantized[^1]);
                            int expectedEnd = 0;
                            for (int index = 0; index < count; index++)
                            {
                                int level = quantized[index + 1];
                                int divisor = index == 0
                                    ? Av1QuantizationLookup.GetDcQuant(qIndex, dcDelta, bitDepth)
                                    : Av1QuantizationLookup.GetAcQuant(qIndex, acDelta, bitDepth);

                                // Signed dequantization truncates the magnitude before restoring sign.
                                int restored = (Math.Abs(level) * divisor) >> size.GetScale();
                                Assert.Equal(level < 0 ? -restored : restored, dequantized[index + 1]);
                                if (quantized[scan[index] + 1] != 0)
                                {
                                    expectedEnd = index + 1;
                                }
                            }

                            Assert.Equal(expectedEnd, end);
                            using BinaryWriter output = new(File.Create(Path.Combine(
                                directory, $"{bits}-{(int)size}-{qIndex}-{sharpness}-{pattern}.bin")));

                            foreach (int value in new[] { bits, (int)size, qIndex, dcDelta, acDelta, sharpness, count, end })
                            {
                                output.Write(value);
                            }

                            output.Write(MemoryMarshal.AsBytes(input.AsSpan(1, count)));
                            output.Write(MemoryMarshal.AsBytes(quantized.AsSpan(1, count)));
                            output.Write(MemoryMarshal.AsBytes(dequantized.AsSpan(1, count)));
                        }
                    }
                }
            }
        }
    }
}
