// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies Wiener rounding, clipping, row bounds, and hardware fallbacks.
/// </summary>
[Trait("Format", "Avif")]
public class Av1WienerFilterTests
{
    /// <summary>
    /// Gets transmitted coefficients spanning the legal extrema, zero residual kernel, and mixed signs.
    /// </summary>
    private static ReadOnlySpan<int> Filters =>
    [
        -5, -23, -17,
        10, 8, 46,
        3, -7, 15,
        0, 0, 0,
        10, -23, 46,
        -5, 8, -17
    ];

    /// <summary>
    /// Verifies every SIMD width and scalar fallback against an independent full-kernel calculation.
    /// </summary>
    [Fact]
    public void FilterMatchesFullKernelAtCoefficientAndVectorBoundaries()
    {
        // Establish the matrix in the VSTest host first so an oracle-coverage failure is reported
        // directly before any child process is started for a restricted hardware configuration.
        ValidateFilters();
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateFilters,
            HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Exercises both passes without relying on the production symmetry reduction or 32-bit accumulation.
    /// </summary>
    private static void ValidateFilters()
    {
        int intermediateClips = 0;
        int outputClips = 0;
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            int maximum = (1 << bitDepth) - 1;
            foreach (int width in new[] { 1, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63, 64, 65 })
            {
                foreach (int height in new[] { 1, 3, 17 })
                {
                    int sourceStride = width + 12;
                    int destinationStride = width + 5;
                    ushort[] source = new ushort[sourceStride * (height + 7)];
                    ushort[] actual = new ushort[(destinationStride * height) + 2];
                    ushort[] expected = new ushort[actual.Length];
                    byte[] byteSource = new byte[source.Length];
                    byte[] byteActual = new byte[actual.Length];
                    byte[] byteExpected = new byte[expected.Length];
                    int scratchLength = Av1WienerFilter.GetScratchLength(width, height);
                    ushort[] scratch = new ushort[scratchLength + 2];
                    for (int pattern = 0; pattern < 4; pattern++)
                    {
                        for (int row = 0; row < height + 7; row++)
                        {
                            for (int column = 0; column < sourceStride; column++)
                            {
                                // Sparse impulses and their inverses isolate the center from negative outer
                                // taps, forcing both ends of the first-pass clipping interval.
                                int value = pattern switch
                                {
                                    0 => (row * 239) + (column * 101) + (row * column * 17),
                                    1 => ((row + column) & 1) * maximum,
                                    2 => column % 8 == 3 ? maximum : 0,
                                    _ => column % 8 == 3 ? 0 : maximum
                                };

                                source[(row * sourceStride) + column] = (ushort)(value & maximum);
                                byteSource[(row * sourceStride) + column] = (byte)source[(row * sourceStride) + column];
                            }
                        }

                        for (int horizontal = 0; horizontal < Filters.Length; horizontal += 3)
                        {
                            for (int vertical = 0; vertical < Filters.Length; vertical += 3)
                            {
                                // Outer sentinels and row padding distinguish a numerical match from an
                                // over-wide store. Poisoned scratch also exposes incomplete intermediate writes.
                                actual.AsSpan().Fill(ushort.MaxValue);
                                expected.AsSpan().Fill(ushort.MaxValue);
                                scratch.AsSpan().Fill(ushort.MaxValue);
                                FilterReference(
                                    source,
                                    sourceStride,
                                    expected.AsSpan(1, expected.Length - 2),
                                    destinationStride,
                                    width,
                                    height,
                                    bitDepth,
                                    Filters.Slice(horizontal, 3),
                                    Filters.Slice(vertical, 3),
                                    ref intermediateClips,
                                    ref outputClips);

                                Av1WienerFilter.FilterStripe(
                                    source,
                                    sourceStride,
                                    actual.AsSpan(1, actual.Length - 2),
                                    destinationStride,
                                    width,
                                    height,
                                    bitDepth,
                                    Filters.Slice(horizontal, 3),
                                    Filters.Slice(vertical, 3),
                                    scratch.AsSpan(1, scratchLength));

                                Assert.True(
                                    expected.AsSpan().SequenceEqual(actual),
                                    $"Depth={bitDepth}, width={width}, height={height}, pattern={pattern}, horizontal={horizontal}, vertical={vertical}");

                                Assert.Equal(ushort.MaxValue, scratch[0]);
                                Assert.Equal(ushort.MaxValue, scratch[^1]);

                                if (bitDepth == 8)
                                {
                                    // The same independent result must hold with byte-backed rows. Comparing
                                    // the complete destination also checks narrowed stores and row padding.
                                    byteActual.AsSpan().Fill(byte.MaxValue);
                                    for (int index = 0; index < expected.Length; index++)
                                    {
                                        byteExpected[index] = (byte)expected[index];
                                    }

                                    scratch.AsSpan().Fill(ushort.MaxValue);
                                    Av1WienerFilter.FilterStripe<byte>(
                                        byteSource,
                                        sourceStride,
                                        byteActual.AsSpan(1, byteActual.Length - 2),
                                        destinationStride,
                                        width,
                                        height,
                                        bitDepth,
                                        Filters.Slice(horizontal, 3),
                                        Filters.Slice(vertical, 3),
                                        scratch.AsSpan(1, scratchLength));

                                    Assert.True(
                                        byteExpected.AsSpan().SequenceEqual(byteActual),
                                        $"Byte width={width}, height={height}, pattern={pattern}, horizontal={horizontal}, vertical={vertical}");

                                    Assert.Equal(ushort.MaxValue, scratch[0]);
                                    Assert.Equal(ushort.MaxValue, scratch[^1]);
                                }
                            }
                        }
                    }
                }
            }
        }

        Assert.True(intermediateClips > 0);
        Assert.True(outputClips > 0);
    }

    /// <summary>
    /// Applies two complete eight-slot kernels with 64-bit sums and independently calculated pass precision.
    /// </summary>
    /// <param name="source">The source including three-sample filter context and one padded slot.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="destination">The visible destination and its row padding.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="width">The output width.</param>
    /// <param name="height">The output height.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="horizontal">The transmitted horizontal coefficients.</param>
    /// <param name="vertical">The transmitted vertical coefficients.</param>
    /// <param name="intermediateClips">The accumulated count of first-pass clipping events.</param>
    /// <param name="outputClips">The accumulated count of final clipping events.</param>
    private static void FilterReference(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        ReadOnlySpan<int> horizontal,
        ReadOnlySpan<int> vertical,
        ref int intermediateClips,
        ref int outputClips)
    {
        int[] horizontalKernel =
        [
            horizontal[0], horizontal[1], horizontal[2],
            128 - (2 * (horizontal[0] + horizontal[1] + horizontal[2])),
            horizontal[2], horizontal[1], horizontal[0], 0
        ];

        int[] verticalKernel =
        [
            vertical[0], vertical[1], vertical[2],
            128 - (2 * (vertical[0] + vertical[1] + vertical[2])),
            vertical[2], vertical[1], vertical[0], 0
        ];

        int firstShift = bitDepth == 12 ? 5 : 3;
        int secondShift = 14 - firstShift;
        long intermediateMaximum = (1L << (bitDepth + 8 - firstShift)) - 1;
        int[] intermediate = new int[width * (height + 7)];
        for (int row = 0; row < height + 7; row++)
        {
            for (int column = 0; column < width; column++)
            {
                long sum = 1L << (bitDepth + 6);
                for (int tap = 0; tap < 8; tap++)
                {
                    sum += (long)source[(row * sourceStride) + column + tap] * horizontalKernel[tap];
                }

                long rounded = (sum + (1L << (firstShift - 1))) >> firstShift;
                long clipped = Math.Clamp(rounded, 0, intermediateMaximum);
                intermediateClips += clipped == rounded ? 0 : 1;
                intermediate[(row * width) + column] = (int)clipped;
            }
        }

        int maximum = (1 << bitDepth) - 1;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                long sum = -(1L << (bitDepth + secondShift - 1));
                for (int tap = 0; tap < 8; tap++)
                {
                    sum += (long)intermediate[((row + tap) * width) + column] * verticalKernel[tap];
                }

                long rounded = (sum + (1L << (secondShift - 1))) >> secondShift;
                long clipped = Math.Clamp(rounded, 0, maximum);
                outputClips += clipped == rounded ? 0 : 1;
                destination[(row * destinationStride) + column] = (ushort)clipped;
            }
        }
    }
}
