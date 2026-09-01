// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 deblocking across every filter width, sample precision, orientation, and intrinsic tier.
/// </summary>
[Trait("Format", "Avif")]
public class Av1DeblockingFilterTests
{
    /// <summary>
    /// The hardware configurations required to exercise packed filtering and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations = HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The padded plane width used to expose horizontal and vertical edge traversal.
    /// </summary>
    private const int Stride = 32;

    /// <summary>
    /// The first Q-side coordinate, leaving the widest kernel addressable on every side.
    /// </summary>
    private const int EdgeCoordinate = 12;

    /// <summary>
    /// Verifies the AV1 reference-category default deltas used to derive frame-edge filter levels.
    /// </summary>
    [Fact]
    public void LoopFilterReferenceDeltasMatchAv1Defaults()
    {
        ObuLoopFilterParameters parameters = new();

        Assert.Equal([1, 0, 0, 0, -1, 0, -1, -1], parameters.ReferenceDeltas);
    }

    /// <summary>
    /// Verifies exact filtering and untouched padding against an independent scalar definition.
    /// </summary>
    [Fact]
    public void FilterMatchesIndependentDefinitionAcrossIntrinsicTiers()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateFilters, Configurations);

    /// <summary>
    /// Verifies skipped inter-edge decisions and reference and mode level deltas through the production frame filter.
    /// </summary>
    [Fact]
    public void DecodeFrameMatchesReference()
    {
        ValidateInterEdgeAndDeltaDecisions(
            Av1PredictionMode.GlobalMotionVector,
            Av1ReferenceFrameType.Last,
            17);

        ValidateInterEdgeAndDeltaDecisions(
            Av1PredictionMode.NewMotionVector,
            Av1ReferenceFrameType.Last,
            21);

        ValidateInterEdgeAndDeltaDecisions(
            Av1PredictionMode.GlobalMotionVector,
            Av1ReferenceFrameType.Golden,
            22);
    }

    /// <summary>
    /// Exercises mixed flatness, high-edge-variance, disabled-mask, direction, and bit-depth cases.
    /// </summary>
    private static void ValidateFilters()
    {
        int[] filterLengths = [4, 6, 8, 14];
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            int scale = 1 << (bitDepth - 8);
            int[][] mixedWindows = CreateMixedWindows(scale);
            int[][] disabledWindows = CreateDisabledWindows(bitDepth);

            foreach (bool vertical in new[] { true, false })
            {
                foreach (int filterLength in filterLengths)
                {
                    if (bitDepth == 8)
                    {
                        AssertByteFilter(vertical, filterLength, mixedWindows);
                        AssertByteFilter(vertical, filterLength, disabledWindows);
                    }
                    else
                    {
                        AssertUInt16Filter(vertical, filterLength, bitDepth, mixedWindows);
                        AssertUInt16Filter(vertical, filterLength, bitDepth, disabledWindows);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Filters two adjacent skipped inter blocks and compares the visible luma plane with the scalar definition.
    /// </summary>
    private static void ValidateInterEdgeAndDeltaDecisions(
        Av1PredictionMode mode,
        Av1ReferenceFrameType referenceFrame,
        int expectedLevel)
    {
        const int width = Stride;
        const int height = 8;
        const int edge = 16;
        const int baseLevel = 20;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = width,
            MaxFrameHeight = height,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                BitDepth = Av1BitDepth.EightBit,
            },
        };

        ObuFrameHeader frameHeader = new()
        {
            FrameType = ObuFrameType.InterFrame,
            ModeInfoColumnCount = width >> Av1Constants.ModeInfoSizeLog2,
            ModeInfoRowCount = height >> Av1Constants.ModeInfoSizeLog2,
            FrameSize = new ObuFrameSize
            {
                FrameWidth = width,
                FrameHeight = height,
            },
        };

        ObuLoopFilterParameters filterParameters = frameHeader.LoopFilterParameters;
        filterParameters.FilterLevel[0] = baseLevel;
        filterParameters.ReferenceDeltaModeEnabled = true;
        filterParameters.ReferenceDeltas[(int)Av1ReferenceFrameType.Last] = -3;
        filterParameters.ReferenceDeltas[(int)Av1ReferenceFrameType.Golden] = 2;
        filterParameters.ModeDeltas[1] = 4;

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblock = frameInfo.GetSuperblock(Point.Empty);
        Av1BlockModeInfo leftModeInfo = new(Av1BlockSize.Block16x8, Point.Empty)
        {
            Skip = true,
            YMode = mode,
        };

        Av1BlockModeInfo rightModeInfo = new(Av1BlockSize.Block16x8, new Point(4, 0))
        {
            Skip = true,
            YMode = mode,
        };

        leftModeInfo.ReferenceFrames[0] = referenceFrame;
        rightModeInfo.ReferenceFrames[0] = referenceFrame;
        frameInfo.UpdateModeInfo(leftModeInfo, superblock);
        frameInfo.UpdateModeInfo(rightModeInfo, superblock);

        using Av1LoopFilterContext loopFilterContext =
            new(frameBuffer.MemoryAllocator, sequenceHeader, frameHeader);

        loopFilterContext.SetTransformSize(Av1Plane.Y, Point.Empty, Av1TransformSize.Size8x8);
        loopFilterContext.SetTransformSize(Av1Plane.Y, new Point(2, 0), Av1TransformSize.Size8x8);
        loopFilterContext.SetTransformSize(Av1Plane.Y, new Point(4, 0), Av1TransformSize.Size8x8);
        loopFilterContext.SetTransformSize(Av1Plane.Y, new Point(6, 0), Av1TransformSize.Size8x8);

        byte[] expected = new byte[width * height];
        for (int row = 0; row < height; row++)
        {
            Span<byte> expectedRow = expected.AsSpan(row * width, width);
            Span<byte> actualRow = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(row);
            expectedRow[..edge].Fill(100);
            expectedRow[edge..].Fill(130);
            actualRow[..edge].Fill(100);
            actualRow[edge..].Fill(130);
        }

        int limit = expectedLevel;
        int boundaryLimit = (2 * (expectedLevel + 2)) + limit;
        int highEdgeVarianceThreshold = expectedLevel >> 4;
        ApplyReference(expected, true, edge, 8, limit, boundaryLimit, highEdgeVarianceThreshold, 8);
        ApplyReference(expected, true, (4 * width) + edge, 8, limit, boundaryLimit, highEdgeVarianceThreshold, 8);

        Av1LoopFilterDecoder decoder = new(sequenceHeader, frameHeader, frameInfo, frameBuffer, loopFilterContext);
        decoder.DecodeFrame();

        for (int row = 0; row < height; row++)
        {
            ReadOnlySpan<byte> expectedRow = expected.AsSpan(row * width, width);
            ReadOnlySpan<byte> actualRow = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(row);
            Assert.Equal(expectedRow, actualRow);
        }
    }

    /// <summary>
    /// Verifies one eight-bit filter configuration against the independent definition.
    /// </summary>
    private static void AssertByteFilter(bool vertical, int filterLength, int[][] windows)
    {
        byte[] expected = Enumerable.Repeat((byte)231, Stride * Stride).ToArray();
        Populate(expected, vertical, windows);
        byte[] actual = (byte[])expected.Clone();
        int q0Offset = (EdgeCoordinate * Stride) + EdgeCoordinate;

        ApplyReference(expected, vertical, q0Offset, filterLength, 20, 60, 3, 8);
        if (vertical)
        {
            Av1DeblockingFilter.FilterVertical(actual, q0Offset, Stride, filterLength, 20, 60, 3);
        }
        else
        {
            Av1DeblockingFilter.FilterHorizontal(actual, q0Offset, Stride, filterLength, 20, 60, 3);
        }

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Verifies one high-bit-depth filter configuration against the independent definition.
    /// </summary>
    private static void AssertUInt16Filter(bool vertical, int filterLength, int bitDepth, int[][] windows)
    {
        ushort[] expected = Enumerable.Repeat((ushort)60000, Stride * Stride).ToArray();
        Populate(expected, vertical, windows);
        ushort[] actual = (ushort[])expected.Clone();
        int q0Offset = (EdgeCoordinate * Stride) + EdgeCoordinate;

        ApplyReference(expected, vertical, q0Offset, filterLength, 20, 60, 3, bitDepth);
        if (vertical)
        {
            Av1DeblockingFilter.FilterVertical(actual, q0Offset, Stride, filterLength, 20, 60, 3, bitDepth);
        }
        else
        {
            Av1DeblockingFilter.FilterHorizontal(actual, q0Offset, Stride, filterLength, 20, 60, 3, bitDepth);
        }

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Creates four lanes that independently select the wide, shorter-wide, narrow, and high-variance results.
    /// </summary>
    private static int[][] CreateMixedWindows(int scale)
        =>
        [
            Scale([100, 100, 100, 100, 100, 100, 100, 101, 101, 101, 101, 101, 101, 101], scale),
            Scale([94, 94, 94, 100, 100, 100, 100, 101, 101, 101, 101, 107, 107, 107], scale),
            Scale([86, 88, 90, 92, 94, 96, 98, 102, 104, 106, 108, 110, 112, 114], scale),
            Scale([85, 85, 85, 85, 85, 85, 100, 104, 119, 119, 119, 119, 119, 119], scale)
        ];

    /// <summary>
    /// Creates four lanes whose cross-edge discontinuity disables every filter width.
    /// </summary>
    private static int[][] CreateDisabledWindows(int bitDepth)
    {
        int maximum = (1 << bitDepth) - 1;
        int[] window = [0, 0, 0, 0, 0, 0, 0, maximum, maximum, maximum, maximum, maximum, maximum, maximum];
        return [(int[])window.Clone(), (int[])window.Clone(), (int[])window.Clone(), (int[])window.Clone()];
    }

    /// <summary>
    /// Scales an eight-bit-domain sample window to the requested coded precision.
    /// </summary>
    private static int[] Scale(int[] values, int scale)
    {
        for (int index = 0; index < values.Length; index++)
        {
            values[index] *= scale;
        }

        return values;
    }

    /// <summary>
    /// Places four eight-bit p6..q6 windows along one padded edge.
    /// </summary>
    private static void Populate(Span<byte> samples, bool vertical, int[][] windows)
    {
        for (int index = 0; index < 4; index++)
        {
            for (int sample = 0; sample < 14; sample++)
            {
                int distance = sample - 7;
                int offset = vertical
                    ? ((EdgeCoordinate + index) * Stride) + EdgeCoordinate + distance
                    : ((EdgeCoordinate + distance) * Stride) + EdgeCoordinate + index;

                samples[offset] = (byte)windows[index][sample];
            }
        }
    }

    /// <summary>
    /// Places four 16-bit p6..q6 windows along one padded edge.
    /// </summary>
    private static void Populate(Span<ushort> samples, bool vertical, int[][] windows)
    {
        for (int index = 0; index < 4; index++)
        {
            for (int sample = 0; sample < 14; sample++)
            {
                int distance = sample - 7;
                int offset = vertical
                    ? ((EdgeCoordinate + index) * Stride) + EdgeCoordinate + distance
                    : ((EdgeCoordinate + distance) * Stride) + EdgeCoordinate + index;

                samples[offset] = (ushort)windows[index][sample];
            }
        }
    }

    /// <summary>
    /// Applies the scalar AV1 definition to four eight-bit samples along one edge.
    /// </summary>
    private static void ApplyReference(
        Span<byte> samples,
        bool vertical,
        int q0Offset,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
    {
        for (int index = 0; index < 4; index++)
        {
            int[] window = LoadWindow(samples, vertical, q0Offset, index);
            FilterReference(window, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, bitDepth);
            StoreWindow(samples, vertical, q0Offset, index, filterLength, window);
        }
    }

    /// <summary>
    /// Applies the scalar AV1 definition to four 16-bit samples along one edge.
    /// </summary>
    private static void ApplyReference(
        Span<ushort> samples,
        bool vertical,
        int q0Offset,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
    {
        for (int index = 0; index < 4; index++)
        {
            int[] window = LoadWindow(samples, vertical, q0Offset, index);
            FilterReference(window, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, bitDepth);
            StoreWindow(samples, vertical, q0Offset, index, filterLength, window);
        }
    }

    /// <summary>
    /// Loads one eight-bit p6..q6 window independently of the production edge operators.
    /// </summary>
    private static int[] LoadWindow(ReadOnlySpan<byte> samples, bool vertical, int q0Offset, int index)
    {
        int[] result = new int[14];
        for (int sample = 0; sample < result.Length; sample++)
        {
            int distance = sample - 7;
            int offset = vertical ? q0Offset + (index * Stride) + distance : q0Offset + (distance * Stride) + index;
            result[sample] = samples[offset];
        }

        return result;
    }

    /// <summary>
    /// Loads one 16-bit p6..q6 window independently of the production edge operators.
    /// </summary>
    private static int[] LoadWindow(ReadOnlySpan<ushort> samples, bool vertical, int q0Offset, int index)
    {
        int[] result = new int[14];
        for (int sample = 0; sample < result.Length; sample++)
        {
            int distance = sample - 7;
            int offset = vertical ? q0Offset + (index * Stride) + distance : q0Offset + (distance * Stride) + index;
            result[sample] = samples[offset];
        }

        return result;
    }

    /// <summary>
    /// Stores every potentially modified sample from one eight-bit reference window.
    /// </summary>
    private static void StoreWindow(Span<byte> samples, bool vertical, int q0Offset, int index, int filterLength, ReadOnlySpan<int> window)
    {
        int radius = filterLength switch
        {
            4 or 6 => 2,
            8 => 3,
            _ => 6
        };

        for (int distance = -radius; distance < radius; distance++)
        {
            int offset = vertical ? q0Offset + (index * Stride) + distance : q0Offset + (distance * Stride) + index;
            samples[offset] = (byte)window[distance + 7];
        }
    }

    /// <summary>
    /// Stores every potentially modified sample from one 16-bit reference window.
    /// </summary>
    private static void StoreWindow(Span<ushort> samples, bool vertical, int q0Offset, int index, int filterLength, ReadOnlySpan<int> window)
    {
        int radius = filterLength switch
        {
            4 or 6 => 2,
            8 => 3,
            _ => 6
        };

        for (int distance = -radius; distance < radius; distance++)
        {
            int offset = vertical ? q0Offset + (index * Stride) + distance : q0Offset + (distance * Stride) + index;
            samples[offset] = (ushort)window[distance + 7];
        }
    }

    /// <summary>
    /// Selects and applies the normative scalar kernel for one p6..q6 window.
    /// </summary>
    private static void FilterReference(
        Span<int> samples,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
    {
        int scale = 1 << (bitDepth - 8);
        int scaledLimit = limit * scale;
        int scaledBoundaryLimit = boundaryLimit * scale;
        bool filterEnabled = IsFilterEnabled(samples, filterLength, scaledLimit, scaledBoundaryLimit);

        if (filterLength == 6 && filterEnabled && IsFlat(samples, filterLength, scale))
        {
            int p2 = samples[4];
            int p1 = samples[5];
            int p0 = samples[6];
            int q0 = samples[7];
            int q1 = samples[8];
            int q2 = samples[9];
            samples[5] = ((3 * p2) + (2 * p1) + (2 * p0) + q0 + 4) >> 3;
            samples[6] = (p2 + (2 * p1) + (2 * p0) + (2 * q0) + q1 + 4) >> 3;
            samples[7] = (p1 + (2 * p0) + (2 * q0) + (2 * q1) + q2 + 4) >> 3;
            samples[8] = (p0 + (2 * q0) + (2 * q1) + (3 * q2) + 4) >> 3;
            return;
        }

        bool flat = filterLength >= 8 && IsFlat(samples, filterLength, scale);
        if (filterLength == 14 && filterEnabled && flat && IsOuterFlat(samples, scale))
        {
            ApplyWideReference(samples);
            return;
        }

        if (filterLength >= 8 && filterEnabled && flat)
        {
            int p3 = samples[3];
            int p2 = samples[4];
            int p1 = samples[5];
            int p0 = samples[6];
            int q0 = samples[7];
            int q1 = samples[8];
            int q2 = samples[9];
            int q3 = samples[10];
            samples[4] = ((3 * p3) + (2 * p2) + p1 + p0 + q0 + 4) >> 3;
            samples[5] = ((2 * p3) + p2 + (2 * p1) + p0 + q0 + q1 + 4) >> 3;
            samples[6] = (p3 + p2 + p1 + (2 * p0) + q0 + q1 + q2 + 4) >> 3;
            samples[7] = (p2 + p1 + p0 + (2 * q0) + q1 + q2 + q3 + 4) >> 3;
            samples[8] = (p1 + p0 + q0 + (2 * q1) + q2 + (2 * q3) + 4) >> 3;
            samples[9] = (p0 + q0 + q1 + (2 * q2) + (3 * q3) + 4) >> 3;
            return;
        }

        ApplyNarrowReference(samples, filterEnabled, highEdgeVarianceThreshold * scale, bitDepth);
    }

    /// <summary>
    /// Evaluates the AV1 filter mask for the selected reference width.
    /// </summary>
    private static bool IsFilterEnabled(ReadOnlySpan<int> samples, int filterLength, int limit, int boundaryLimit)
    {
        bool enabled = Math.Abs(samples[5] - samples[6]) <= limit
            && Math.Abs(samples[8] - samples[7]) <= limit
            && ((2 * Math.Abs(samples[6] - samples[7])) + (Math.Abs(samples[5] - samples[8]) >> 1)) <= boundaryLimit;

        if (filterLength >= 6)
        {
            enabled = enabled
                && Math.Abs(samples[4] - samples[5]) <= limit
                && Math.Abs(samples[9] - samples[8]) <= limit;
        }

        if (filterLength >= 8)
        {
            enabled = enabled
                && Math.Abs(samples[3] - samples[4]) <= limit
                && Math.Abs(samples[10] - samples[9]) <= limit;
        }

        return enabled;
    }

    /// <summary>
    /// Evaluates the AV1 inner flatness mask for the selected reference width.
    /// </summary>
    private static bool IsFlat(ReadOnlySpan<int> samples, int filterLength, int threshold)
    {
        bool flat = Math.Abs(samples[5] - samples[6]) <= threshold
            && Math.Abs(samples[8] - samples[7]) <= threshold
            && Math.Abs(samples[4] - samples[6]) <= threshold
            && Math.Abs(samples[9] - samples[7]) <= threshold;

        return filterLength == 6
            ? flat
            : flat && Math.Abs(samples[3] - samples[6]) <= threshold && Math.Abs(samples[10] - samples[7]) <= threshold;
    }

    /// <summary>
    /// Evaluates the AV1 outer flatness mask for the fourteen-tap reference kernel.
    /// </summary>
    private static bool IsOuterFlat(ReadOnlySpan<int> samples, int threshold)
        => Math.Abs(samples[0] - samples[6]) <= threshold
            && Math.Abs(samples[1] - samples[6]) <= threshold
            && Math.Abs(samples[2] - samples[6]) <= threshold
            && Math.Abs(samples[11] - samples[7]) <= threshold
            && Math.Abs(samples[12] - samples[7]) <= threshold
            && Math.Abs(samples[13] - samples[7]) <= threshold;

    /// <summary>
    /// Applies the signed-saturating four-tap reference equations.
    /// </summary>
    private static void ApplyNarrowReference(Span<int> samples, bool filterEnabled, int highEdgeVarianceThreshold, int bitDepth)
    {
        if (!filterEnabled)
        {
            return;
        }

        int offset = 128 << (bitDepth - 8);
        int minimum = -offset;
        int maximum = offset - 1;
        int p1 = samples[5] - offset;
        int p0 = samples[6] - offset;
        int q0 = samples[7] - offset;
        int q1 = samples[8] - offset;
        bool highVariance = Math.Abs(samples[5] - samples[6]) > highEdgeVarianceThreshold
            || Math.Abs(samples[8] - samples[7]) > highEdgeVarianceThreshold;

        int filter = highVariance ? Math.Clamp(p1 - q1, minimum, maximum) : 0;
        filter = Math.Clamp(filter + (3 * (q0 - p0)), minimum, maximum);
        int filter1 = Math.Clamp(filter + 4, minimum, maximum) >> 3;
        int filter2 = Math.Clamp(filter + 3, minimum, maximum) >> 3;
        samples[7] = Math.Clamp(q0 - filter1, minimum, maximum) + offset;
        samples[6] = Math.Clamp(p0 + filter2, minimum, maximum) + offset;

        int outerFilter = highVariance ? 0 : (filter1 + 1) >> 1;
        samples[8] = Math.Clamp(q1 - outerFilter, minimum, maximum) + offset;
        samples[5] = Math.Clamp(p1 + outerFilter, minimum, maximum) + offset;
    }

    /// <summary>
    /// Applies the thirteen-tap reference equations to the twelve modifiable samples.
    /// </summary>
    private static void ApplyWideReference(Span<int> samples)
    {
        int p6 = samples[0];
        int p5 = samples[1];
        int p4 = samples[2];
        int p3 = samples[3];
        int p2 = samples[4];
        int p1 = samples[5];
        int p0 = samples[6];
        int q0 = samples[7];
        int q1 = samples[8];
        int q2 = samples[9];
        int q3 = samples[10];
        int q4 = samples[11];
        int q5 = samples[12];
        int q6 = samples[13];

        samples[1] = ((7 * p6) + (2 * p5) + (2 * p4) + p3 + p2 + p1 + p0 + q0 + 8) >> 4;
        samples[2] = ((5 * p6) + (2 * p5) + (2 * p4) + (2 * p3) + p2 + p1 + p0 + q0 + q1 + 8) >> 4;
        samples[3] = ((4 * p6) + p5 + (2 * p4) + (2 * p3) + (2 * p2) + p1 + p0 + q0 + q1 + q2 + 8) >> 4;
        samples[4] = ((3 * p6) + p5 + p4 + (2 * p3) + (2 * p2) + (2 * p1) + p0 + q0 + q1 + q2 + q3 + 8) >> 4;
        samples[5] = ((2 * p6) + p5 + p4 + p3 + (2 * p2) + (2 * p1) + (2 * p0) + q0 + q1 + q2 + q3 + q4 + 8) >> 4;
        samples[6] = (p6 + p5 + p4 + p3 + p2 + (2 * p1) + (2 * p0) + (2 * q0) + q1 + q2 + q3 + q4 + q5 + 8) >> 4;
        samples[7] = (p5 + p4 + p3 + p2 + p1 + (2 * p0) + (2 * q0) + (2 * q1) + q2 + q3 + q4 + q5 + q6 + 8) >> 4;
        samples[8] = (p4 + p3 + p2 + p1 + p0 + (2 * q0) + (2 * q1) + (2 * q2) + q3 + q4 + q5 + (2 * q6) + 8) >> 4;
        samples[9] = (p3 + p2 + p1 + p0 + q0 + (2 * q1) + (2 * q2) + (2 * q3) + q4 + q5 + (3 * q6) + 8) >> 4;
        samples[10] = (p2 + p1 + p0 + q0 + q1 + (2 * q2) + (2 * q3) + (2 * q4) + q5 + (4 * q6) + 8) >> 4;
        samples[11] = (p1 + p0 + q0 + q1 + q2 + (2 * q3) + (2 * q4) + (2 * q5) + (5 * q6) + 8) >> 4;
        samples[12] = (p0 + q0 + q1 + q2 + q3 + (2 * q4) + (2 * q5) + (7 * q6) + 8) >> 4;
    }
}
