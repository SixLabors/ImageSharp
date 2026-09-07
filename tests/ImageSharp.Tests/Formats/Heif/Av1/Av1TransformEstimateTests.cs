// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies motion-winner transform estimates and exports the complete decision boundary for comparison.
/// </summary>
[Trait("Format", "Avif")]
public class Av1TransformEstimateTests
{
    /// <summary>
    /// Exercises complete and partially terminated estimates across sample precision and hardware paths.
    /// </summary>
    [Fact]
    public void InterTransformEstimatePreservesDecisionAndContextContracts()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateEstimates,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks storage and skip decisions, retaining independent input samples and the aggregate output.
    /// </summary>
    private static void ValidateEstimates()
    {
        string vectorWidth = Vector512.IsHardwareAccelerated ? "512"
            : Vector256.IsHardwareAccelerated ? "256"
            : Vector128.IsHardwareAccelerated ? "128" : "0";

        string directory = Path.Combine(TestEnvironment.ActualOutputDirectoryFullPath, "Heif", "Av1", "TransformEstimates", vectorWidth);

        Directory.CreateDirectory(directory);
        using Av1EncoderBlockWorkspace workspace = new(Configuration.Default);
        foreach (Av1BitDepth bitDepth in new[] { Av1BitDepth.EightBit, Av1BitDepth.TenBit, Av1BitDepth.TwelveBit })
        {
            foreach (Av1TransformSize transformSize in new[]
            {
                Av1TransformSize.Size4x4, Av1TransformSize.Size8x8, Av1TransformSize.Size16x16,
                Av1TransformSize.Size32x32, Av1TransformSize.Size64x64, Av1TransformSize.Size8x16
            })
            {
                foreach (bool split in new[] { false, true })
                {
                    Av1BlockSize blockSize = transformSize.ToBlockSize();
                    if (split)
                    {
                        blockSize = transformSize switch
                        {
                            Av1TransformSize.Size4x4 => Av1BlockSize.Block8x8,
                            Av1TransformSize.Size8x8 => Av1BlockSize.Block16x16,
                            Av1TransformSize.Size16x16 => Av1BlockSize.Block32x32,
                            Av1TransformSize.Size32x32 => Av1BlockSize.Block64x64,
                            Av1TransformSize.Size64x64 => Av1BlockSize.Block128x128,
                            _ => Av1BlockSize.Block16x32
                        };
                    }

                    int width = blockSize.GetWidth();
                    int height = blockSize.GetHeight();
                    int stride = width + 3;
                    short[] residual = new short[stride * height];
                    int[] quantized = new int[transformSize.GetAdjusted().GetSize2d()];
                    byte[] above = new byte[width / 4];
                    byte[] left = new byte[height / 4];
                    for (int i = 0; i < above.Length; i++)
                    {
                        above[i] = (byte)(((i % 3) << Av1Constants.CoefficientContextBitCount) + (i % 5));
                    }

                    for (int i = 0; i < left.Length; i++)
                    {
                        left[i] = (byte)((((i + 1) % 3) << Av1Constants.CoefficientContextBitCount) + (i % 7));
                    }

                    byte[] originalAbove = (byte[])above.Clone();
                    byte[] originalLeft = (byte[])left.Clone();
                    foreach (int qIndex in new[] { 0, 90, 255 })
                    {
                        bool lossless = qIndex == 0;
                        if (lossless && transformSize != Av1TransformSize.Size4x4)
                        {
                            continue;
                        }

                        using Av1SymbolEncoder writer = new(Configuration.Default, 65536, qIndex, updateCdf: true);
                        for (int pattern = 0; pattern < 4; pattern++)
                        {
                            int bits = bitDepth.GetBitCount();
                            int amplitude = (1 << bits) - 1;
                            uint random = 73;
                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    random = unchecked((random * 1664525) + 1013904223);
                                    int value = pattern switch
                                    {
                                        0 => 0,
                                        1 => (int)(random % (uint)((2 * amplitude) + 1)) - amplitude,
                                        2 => ((x + y) % 3) - 1,
                                        _ => x == 0 && y == 0 ? amplitude : 0
                                    };

                                    residual[(y * stride) + x] = (short)value;
                                }
                            }

                            int multiplier = 173;
                            int partitionRate = split ? 431 : 0;
                            int noSkipRate = 571;
                            int skipRate = 619;
                            int sharpness = pattern;
                            long cost = Av1TransformBlockEncoder.EstimateInterTransform(
                                workspace,
                                residual,
                                stride,
                                quantized,
                                writer,
                                above,
                                left,
                                blockSize,
                                new Size(width, height),
                                transformSize,
                                qIndex,
                                0,
                                bitDepth,
                                sharpness,
                                lossless,
                                multiplier,
                                partitionRate,
                                noSkipRate,
                                skipRate,
                                long.MaxValue,
                                out Av1RateDistortionStatistics statistics,
                                out long energy,
                                out bool skip);

                            Assert.Equal(originalAbove, above);
                            Assert.Equal(originalLeft, left);
                            if (pattern == 0)
                            {
                                Assert.True(skip);
                                Assert.Equal(0, statistics.Distortion);
                                Assert.Equal(0, energy);
                                Assert.Equal(Av1RateDistortion.GetCost(multiplier, skipRate, 0), cost);
                                Assert.True(statistics.Rate > 0);
                            }
                            else if (lossless)
                            {
                                Assert.False(skip);
                                Assert.Equal(0, statistics.Distortion);
                            }

                            // Running the same trial again must see identical probabilities and incoming neighbors.
                            long repeated = Av1TransformBlockEncoder.EstimateInterTransform(
                                workspace,
                                residual,
                                stride,
                                quantized,
                                writer,
                                above,
                                left,
                                blockSize,
                                new Size(width, height),
                                transformSize,
                                qIndex,
                                0,
                                bitDepth,
                                sharpness,
                                lossless,
                                multiplier,
                                partitionRate,
                                noSkipRate,
                                skipRate,
                                long.MaxValue,
                                out Av1RateDistortionStatistics repeatedStatistics,
                                out long repeatedEnergy,
                                out bool repeatedSkip);

                            Assert.Equal(cost, repeated);
                            Assert.Equal(statistics, repeatedStatistics);
                            Assert.Equal(energy, repeatedEnergy);
                            Assert.Equal(skip, repeatedSkip);

                            if (split)
                            {
                                long terminated = Av1TransformBlockEncoder.EstimateInterTransform(
                                    workspace,
                                    residual,
                                    stride,
                                    quantized,
                                    writer,
                                    above,
                                    left,
                                    blockSize,
                                    new Size(width, height),
                                    transformSize,
                                    qIndex,
                                    0,
                                    bitDepth,
                                    sharpness,
                                    lossless,
                                    multiplier,
                                    partitionRate,
                                    noSkipRate,
                                    skipRate,
                                    0,
                                    out Av1RateDistortionStatistics incomplete,
                                    out _,
                                    out _);

                                Assert.Equal(long.MaxValue, terminated);
                                Assert.Equal(Av1RateDistortionStatistics.Invalid, incomplete);
                            }

                            using BinaryWriter output = new(File.Create(Path.Combine(
                                directory, $"{bits}-{width}-{height}-{(int)transformSize}-{qIndex}-{pattern}.bin")));

                            foreach (int value in new[]
                            {
                                bits, width, height, (int)transformSize, qIndex, sharpness, lossless ? 1 : 0,
                                multiplier, partitionRate, noSkipRate, skipRate, stride
                            })
                            {
                                output.Write(value);
                            }

                            output.Write(cost);
                            output.Write((long)statistics.Rate);
                            output.Write(statistics.Distortion);
                            output.Write(energy);
                            output.Write(skip ? 1L : 0L);
                            output.Write(above);
                            output.Write(left);
                            output.Write(MemoryMarshal.AsBytes(residual.AsSpan()));
                        }
                    }
                }
            }
        }
    }
}
