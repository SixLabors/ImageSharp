// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC planar, DC, angular, reference-filter, and SIMD prediction behavior.
/// </summary>
[Trait("Format", "Heic")]
public class HevcIntraPredictorTests
{
    /// <summary>
    /// The hardware configurations required to exercise each SIMD tier and the complete scalar fallback.
    /// </summary>
    private const HwIntrinsics PredictorConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies fixed four-by-four prediction results derived from the HEVC intra-prediction equations.
    /// </summary>
    [Fact]
    public void PredictsKnownFourByFourBlocks()
    {
        ushort[] top = [64, 80, 96, 112, 128, 144, 160, 176, 192];
        ushort[] left = [64, 70, 76, 82, 88, 94, 100, 106, 112];
        int[] modes = [0, 1, 2, 9, 18, 30, 34];
        ushort[][] expected =
        [
            [86, 101, 117, 132, 90, 103, 115, 128, 94, 104, 114, 123, 98, 105, 112, 119],
            [84, 93, 97, 101, 88, 92, 92, 92, 90, 92, 92, 92, 91, 92, 92, 92],
            [76, 82, 88, 94, 82, 88, 94, 100, 88, 94, 100, 106, 94, 100, 106, 112],
            [70, 71, 71, 72, 76, 77, 77, 78, 82, 83, 83, 84, 88, 89, 89, 90],
            [64, 80, 96, 112, 70, 64, 80, 96, 76, 70, 64, 80, 82, 76, 70, 64],
            [87, 103, 119, 135, 93, 109, 125, 141, 100, 116, 132, 148, 106, 122, 138, 154],
            [96, 112, 128, 144, 112, 128, 144, 160, 128, 144, 160, 176, 144, 160, 176, 192]
        ];

        const int size = 4;
        const int stride = 7;
        ushort[] destination = new ushort[stride * size];
        ushort[] scratch = new ushort[HevcIntraPredictor.GetScratchLength(2)];
        for (int caseIndex = 0; caseIndex < modes.Length; caseIndex++)
        {
            destination.AsSpan().Fill(ushort.MaxValue);
            int mode = modes[caseIndex];
            HevcIntraPredictor.Predict(top, left, destination, stride, 2, mode, 8, mode == 1, scratch);

            for (int y = 0; y < size; y++)
            {
                ReadOnlySpan<ushort> expectedRow = expected[caseIndex].AsSpan(y * size, size);
                ReadOnlySpan<ushort> actualRow = destination.AsSpan(y * stride, size);
                Assert.True(expectedRow.SequenceEqual(actualRow), $"Mode {mode}, row {y} did not match the fixed HEVC result.");
            }
        }
    }

    /// <summary>
    /// Verifies the optional luma boundary filter for the pure horizontal and vertical modes.
    /// </summary>
    [Fact]
    public void FiltersPureDirectionPredictionEdges()
    {
        ushort[] top = [64, 80, 96, 112, 128, 144, 160, 176, 192];
        ushort[] left = [64, 70, 76, 82, 88, 94, 100, 106, 112];
        ushort[] scratch = new ushort[HevcIntraPredictor.GetScratchLength(2)];
        ushort[] horizontal = new ushort[16];
        ushort[] vertical = new ushort[16];

        HevcIntraPredictor.Predict(top, left, horizontal, 4, 2, 10, 8, true, scratch);
        HevcIntraPredictor.Predict(top, left, vertical, 4, 2, 26, 8, true, scratch);

        ushort[] expectedHorizontal = [78, 86, 94, 102, 76, 76, 76, 76, 82, 82, 82, 82, 88, 88, 88, 88];
        ushort[] expectedVertical = [83, 96, 112, 128, 86, 96, 112, 128, 89, 96, 112, 128, 92, 96, 112, 128];
        Assert.True(expectedHorizontal.AsSpan().SequenceEqual(horizontal));
        Assert.True(expectedVertical.AsSpan().SequenceEqual(vertical));
    }

    /// <summary>
    /// Verifies exact three-tap filtering, including the shared top-left sample.
    /// </summary>
    [Fact]
    public void FiltersReferenceSamplesWithThreeTapKernel()
    {
        ushort[] top = [64, 80, 96, 112, 128, 144, 160, 176, 192];
        ushort[] left = [64, 70, 76, 82, 88, 94, 100, 106, 112];
        ushort[] filteredTop = new ushort[top.Length];
        ushort[] filteredLeft = new ushort[left.Length];

        HevcIntraPredictor.FilterReferenceSamples(top, left, filteredTop, filteredLeft, 2, 8, true);

        ushort[] expectedTop = [70, 80, 96, 112, 128, 144, 160, 176, 192];
        ushort[] expectedLeft = [70, 70, 76, 82, 88, 94, 100, 106, 112];
        Assert.True(expectedTop.AsSpan().SequenceEqual(filteredTop));
        Assert.True(expectedLeft.AsSpan().SequenceEqual(filteredLeft));
    }

    /// <summary>
    /// Verifies strong bilinear and normal three-tap reference filtering through every SIMD tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void ReferenceFiltersMatchScalarDefinitionsAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateReferenceFilters, PredictorConfigurations);

    /// <summary>
    /// Compares every prediction mode and block width with a specification-shaped scalar oracle through every SIMD tier.
    /// </summary>
    [Fact]
    public void EveryModeMatchesScalarOracleAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateEveryMode, PredictorConfigurations);

    /// <summary>
    /// Verifies strong bilinear and normal three-tap reference filtering under the selected hardware configuration.
    /// </summary>
    private static void ValidateReferenceFilters()
    {
        const int size = 32;
        ushort[] top = new ushort[(size * 2) + 1];
        ushort[] left = new ushort[top.Length];
        ushort[] filteredTop = new ushort[top.Length];
        ushort[] filteredLeft = new ushort[left.Length];
        for (int i = 0; i < top.Length; i++)
        {
            top[i] = (ushort)(100 + i + (i % 3));
            left[i] = (ushort)(100 + (2 * i) + (i % 5));
        }

        // Strong smoothing is selected from the endpoint/midpoint test, so keep those six values exactly bilinear
        // while the remaining samples deliberately differ from the expected straight lines.
        top[0] = left[0] = 100;
        top[size] = 132;
        top[size * 2] = 164;
        left[size] = 164;
        left[size * 2] = 228;

        HevcIntraPredictor.FilterReferenceSamples(top, left, filteredTop, filteredLeft, 5, 10, true);

        for (int i = 0; i < top.Length; i++)
        {
            Assert.Equal((ushort)(100 + i), filteredTop[i]);
            Assert.Equal((ushort)(100 + (2 * i)), filteredLeft[i]);
        }

        HevcIntraPredictor.FilterReferenceSamples(top, left, filteredTop, filteredLeft, 5, 10, false);

        Assert.Equal((ushort)((left[1] + (2 * top[0]) + top[1] + 2) >> 2), filteredTop[0]);
        Assert.Equal(filteredTop[0], filteredLeft[0]);
        for (int i = 1; i < top.Length - 1; i++)
        {
            Assert.Equal((ushort)((top[i - 1] + (2 * top[i]) + top[i + 1] + 2) >> 2), filteredTop[i]);
            Assert.Equal((ushort)((left[i - 1] + (2 * left[i]) + left[i + 1] + 2) >> 2), filteredLeft[i]);
        }

        Assert.Equal(top[^1], filteredTop[^1]);
        Assert.Equal(left[^1], filteredLeft[^1]);
    }

    /// <summary>
    /// Compares every prediction mode and block width with the scalar oracle under the selected hardware configuration.
    /// </summary>
    private static void ValidateEveryMode()
    {
        ReadOnlySpan<(int Log2Size, int BitDepth)> cases = [(2, 8), (3, 10), (4, 12), (5, 12)];
        foreach ((int log2Size, int bitDepth) in cases)
        {
            int size = 1 << log2Size;
            int maximum = (1 << bitDepth) - 1;
            int referenceLength = (size * 2) + 1;
            ushort[] top = new ushort[referenceLength];
            ushort[] left = new ushort[referenceLength];
            top[0] = left[0] = (ushort)(maximum / 3);
            for (int i = 1; i < referenceLength; i++)
            {
                top[i] = (ushort)((top[0] + (37 * i) + (3 * size)) & maximum);
                left[i] = (ushort)((left[0] + (53 * i) + (5 * size)) & maximum);
            }

            int stride = size + 3;
            ushort[] expected = new ushort[stride * size];
            ushort[] actual = new ushort[stride * size];
            ushort[] scratch = new ushort[HevcIntraPredictor.GetScratchLength(log2Size)];
            for (int mode = 0; mode <= 34; mode++)
            {
                expected.AsSpan().Clear();
                actual.AsSpan().Clear();
                PredictScalar(top, left, expected, stride, size, mode, bitDepth, true);
                HevcIntraPredictor.Predict(top, left, actual, stride, log2Size, mode, bitDepth, true, scratch);
                Assert.True(expected.AsSpan().SequenceEqual(actual), $"Mode {mode}, size {size}, and bit depth {bitDepth} did not match the scalar oracle.");
            }
        }
    }

    /// <summary>
    /// Reconstructs one block directly from the HEVC planar, DC, and angular prediction equations.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="left">The left reference samples.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="mode">The prediction mode.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="filterPredictionEdges">Whether the luma edge filter applies.</param>
    private static void PredictScalar(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> destination,
        int destinationStride,
        int size,
        int mode,
        int bitDepth,
        bool filterPredictionEdges)
    {
        if (mode == 0)
        {
            int shift = BitOperations.Log2((uint)size) + 1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int horizontal = ((size - x - 1) * left[y + 1]) + ((x + 1) * top[size + 1]);
                    int vertical = ((size - y - 1) * top[x + 1]) + ((y + 1) * left[size + 1]);
                    destination[(y * destinationStride) + x] = (ushort)((horizontal + vertical + size) >> shift);
                }
            }

            return;
        }

        if (mode == 1)
        {
            PredictDcScalar(top, left, destination, destinationStride, size, filterPredictionEdges);
            return;
        }

        if (mode == 10)
        {
            PredictHorizontalScalar(top, left, destination, destinationStride, size, bitDepth, filterPredictionEdges);
            return;
        }

        if (mode == 26)
        {
            PredictVerticalScalar(top, left, destination, destinationStride, size, bitDepth, filterPredictionEdges);
            return;
        }

        PredictAngularScalar(top, left, destination, destinationStride, size, mode);
    }

    /// <summary>
    /// Reconstructs a scalar DC block and its optional boundary filter.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="left">The left reference samples.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="filterPredictionEdges">Whether the luma edge filter applies.</param>
    private static void PredictDcScalar(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> destination,
        int destinationStride,
        int size,
        bool filterPredictionEdges)
    {
        int sum = 0;
        for (int i = 1; i <= size; i++)
        {
            sum += top[i] + left[i];
        }

        ushort dc = (ushort)((sum + size) >> (BitOperations.Log2((uint)size) + 1));
        for (int y = 0; y < size; y++)
        {
            destination.Slice(y * destinationStride, size).Fill(dc);
        }

        if (!filterPredictionEdges)
        {
            return;
        }

        destination[0] = (ushort)((top[1] + left[1] + (2 * dc) + 2) >> 2);
        for (int i = 1; i < size; i++)
        {
            destination[i] = (ushort)((top[i + 1] + (3 * dc) + 2) >> 2);
            destination[i * destinationStride] = (ushort)((left[i + 1] + (3 * dc) + 2) >> 2);
        }
    }

    /// <summary>
    /// Reconstructs scalar horizontal prediction and its optional boundary filter.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="left">The left reference samples.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="filterPredictionEdges">Whether the luma edge filter applies.</param>
    private static void PredictHorizontalScalar(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> destination,
        int destinationStride,
        int size,
        int bitDepth,
        bool filterPredictionEdges)
    {
        for (int y = 0; y < size; y++)
        {
            destination.Slice(y * destinationStride, size).Fill(left[y + 1]);
        }

        if (filterPredictionEdges)
        {
            int maximum = (1 << bitDepth) - 1;
            for (int x = 0; x < size; x++)
            {
                destination[x] = (ushort)Math.Clamp(destination[x] + ((top[x + 1] - top[0]) >> 1), 0, maximum);
            }
        }
    }

    /// <summary>
    /// Reconstructs scalar vertical prediction and its optional boundary filter.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="left">The left reference samples.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="filterPredictionEdges">Whether the luma edge filter applies.</param>
    private static void PredictVerticalScalar(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> destination,
        int destinationStride,
        int size,
        int bitDepth,
        bool filterPredictionEdges)
    {
        int maximum = (1 << bitDepth) - 1;
        for (int y = 0; y < size; y++)
        {
            top.Slice(1, size).CopyTo(destination[(y * destinationStride)..]);
            if (filterPredictionEdges)
            {
                int offset = y * destinationStride;
                destination[offset] = (ushort)Math.Clamp(destination[offset] + ((left[y + 1] - left[0]) >> 1), 0, maximum);
            }
        }
    }

    /// <summary>
    /// Reconstructs a scalar angular block, including negative-reference extension and horizontal transposition.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="left">The left reference samples.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="mode">The angular prediction mode.</param>
    private static void PredictAngularScalar(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> destination,
        int destinationStride,
        int size,
        int mode)
    {
        ReadOnlySpan<int> angles = [0, 2, 5, 9, 13, 17, 21, 26, 32];
        ReadOnlySpan<int> inverseAngles = [0, 4096, 1638, 910, 630, 482, 390, 315, 256];
        bool vertical = mode >= 18;
        int angleMode = vertical ? mode - 26 : 10 - mode;
        int absoluteAngleMode = Math.Abs(angleMode);
        int angle = angles[absoluteAngleMode] * Math.Sign(angleMode);
        ReadOnlySpan<ushort> main = vertical ? top : left;
        ReadOnlySpan<ushort> side = vertical ? left : top;
        int mainOrigin = size * 2;
        int[] extendedMain = new int[(4 * size) + 1];
        for (int i = 0; i < main.Length; i++)
        {
            extendedMain[mainOrigin + i] = main[i];
        }

        if (angle < 0)
        {
            int inverseAngleSum = 128;
            for (int index = -1; index > ((size * angle) >> 5); index--)
            {
                inverseAngleSum += inverseAngles[absoluteAngleMode];
                extendedMain[mainOrigin + index] = side[inverseAngleSum >> 8];
            }
        }

        ushort[] temporary = new ushort[size * size];
        for (int y = 0, deltaPosition = angle; y < size; y++, deltaPosition += angle)
        {
            int deltaInteger = deltaPosition >> 5;
            int deltaFraction = deltaPosition & 31;
            for (int x = 0; x < size; x++)
            {
                int index = mainOrigin + x + deltaInteger + 1;
                temporary[(y * size) + x] = deltaFraction == 0
                    ? (ushort)extendedMain[index]
                    : (ushort)(((extendedMain[index] * (32 - deltaFraction)) + (extendedMain[index + 1] * deltaFraction) + 16) >> 5);
            }
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int sourceIndex = vertical ? (y * size) + x : (x * size) + y;
                destination[(y * destinationStride) + x] = temporary[sourceIndex];
            }
        }
    }
}
