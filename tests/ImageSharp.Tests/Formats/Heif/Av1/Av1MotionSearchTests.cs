// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using Xunit;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchSettings;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies retained search geometry and the statistics published by complete integer search paths.
/// </summary>
public class Av1MotionSearchTests
{
    /// <summary>
    /// Exercises the coordinated search across three differential references and exports its retained state.
    /// </summary>
    /// <param name="bits">The coded component precision.</param>
    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void SingleReferenceSearchRetainsCoordinatedDecisions(int bits)
    {
        if (bits == 8)
        {
            VerifySingleReferenceSearches<byte, Av1MotionSearchBase.ByteOperator>(Av1BitDepth.EightBit, bits);
        }
        else
        {
            VerifySingleReferenceSearches<ushort, Av1MotionSearchBase.UInt16Operator>(
                bits == 10 ? Av1BitDepth.TenBit : Av1BitDepth.TwelveBit,
                bits);
        }
    }

    /// <summary>
    /// Checks exact integer matches and publishes textured, fractional, and repeated-reference decisions.
    /// </summary>
    private static void VerifySingleReferenceSearches<TSample, TOperator>(Av1BitDepth bitDepth, int bits)
        where TSample : unmanaged, IBinaryInteger<TSample>
        where TOperator : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    {
        const int QIndex = 90;
        const int ReferenceStride = 192;
        const int ReferenceOrigin = (64 * ReferenceStride) + 64;
        using Av1EncoderBlockWorkspace workspace = new(Configuration.Default, allocateInterMotionCosts: true);
        using Av1SymbolEncoder writer = new(Configuration.Default, 64, QIndex, updateCdf: true);
        Av1MotionVectorCosts costs = workspace.GetMotionVectorCosts(Av1MotionVectorPrecision.EighthSample);
        writer.FillMotionVectorCosts(costs);
        int multiplier = Av1RateDistortion.GetKeyFrameRateMultiplier(QIndex, bitDepth);
        TSample[] prediction = new TSample[136 * 128];
        short[] residual = new short[128 * 128];
        short[] scratch = new short[136 * 128];
        int[] quantized = new int[64 * 64];
        byte[] contexts = new byte[32];
        int maximum = (1 << bits) - 1;
        foreach (Av1BlockSize blockSize in new[] { Av1BlockSize.Block8x8, Av1BlockSize.Block16x16, Av1BlockSize.Block64x64 })
        {
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int sourceStride = width + 3;
            TSample[] source = new TSample[sourceStride * height];
            TSample[] reference = new TSample[ReferenceStride * ReferenceStride];
            for (int pattern = 0; pattern < 7; pattern++)
            {
                uint random = (uint)(173 + pattern);
                for (int index = 0; index < reference.Length; index++)
                {
                    random = unchecked((random * 1664525) + 1013904223);
                    reference[index] = TSample.CreateChecked((random >> 16) & (uint)maximum);
                }

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = ReferenceOrigin + ((y + 3) * ReferenceStride) + x - 2;
                        int sample = int.CreateChecked(reference[offset]);
                        if (pattern == 1)
                        {
                            sample = (sample + int.CreateChecked(reference[offset + 1]) + 1) >> 1;
                        }
                        else if (pattern >= 2)
                        {
                            sample = (sample + ((x * 13) ^ (y * 37)) + pattern) & maximum;
                        }

                        source[(y * sourceStride) + x] = TSample.CreateChecked(sample);
                    }
                }

                TSample[] originalSource = source.ToArray();
                TSample[] originalReference = reference.ToArray();
                foreach (int speed in new[] { 0, 1, 2, 3, 4, 5, 8 })
                {
                    Size frameSize = pattern == 3 ? new Size(1280, 720) : new Size(640, 480);
                    Av1MotionSearchSettings settings = new((HeifEncodingSpeed)speed, false, frameSize, QIndex, false, false);
                    foreach (bool forceInteger in new[] { false, true })
                    {
                        int frameStep = 5;
                        int spatialMagnitude = pattern == 2 ? 128 : 16;
                        int searchRange = pattern == 3 ? 4 : int.MaxValue;
                        Rectangle bounds = Rectangle.FromLTRB(-24, -24, 25, 25);
                        Av1MotionSearchBase.SingleReferenceSearch<TSample, TOperator> search = new(
                            source,
                            sourceStride,
                            reference,
                            ReferenceStride,
                            ReferenceOrigin,
                            blockSize,
                            bounds,
                            workspace,
                            prediction,
                            residual,
                            scratch,
                            quantized,
                            writer,
                            contexts,
                            contexts,
                            bitDepth,
                            QIndex,
                            0,
                            0,
                            false,
                            multiplier,
                            512,
                            1024,
                            256,
                            Av1InterpolationFilter.Regular,
                            Av1InterpolationFilter.Regular,
                            costs);

                        Av1MotionSearchBase.SingleReferenceState state = default;
                        for (int referenceIndex = 0; referenceIndex < 3; referenceIndex++)
                        {
                            Av1MotionVector referenceVector = referenceIndex == 1
                                ? new Av1MotionVector(21, -13) : new Av1MotionVector(24, -16);

                            Point start = new(
                                (referenceVector.Column + 3 + (referenceVector.Column >= 0 ? 1 : 0)) >> 3,
                                (referenceVector.Row + 3 + (referenceVector.Row >= 0 ? 1 : 0)) >> 3);

                            int analysisWidth = pattern >= 4 ? width / 16 : 0;
                            Av1MotionVector[] temporal = new Av1MotionVector[analysisWidth * analysisWidth];
                            for (int index = 0; index < temporal.Length; index++)
                            {
                                // Repeated cells accumulate votes; the final pattern interrupts analysis after a prefix.
                                temporal[index] = pattern == 6 && index == temporal.Length / 2
                                    ? new Av1MotionVector(short.MinValue, short.MinValue)
                                    : new Av1MotionVector(8 * (index % 3 == 0 ? 16 : -16), 8 * (index % 2 == 0 ? 16 : -16));
                            }

                            Av1MotionSearchBase.StartingCandidate[] starts = new Av1MotionSearchBase.StartingCandidate[temporal.Length + 1];
                            int startCount = Av1MotionSearchBase.CollectStartingCandidates(
                                start, temporal, analysisWidth, new Size(analysisWidth, analysisWidth), starts, out int totalWeight);

                            int drlRate = referenceIndex * 256;
                            bool valid = search.Search(
                                settings,
                                frameStep,
                                spatialMagnitude,
                                true,
                                searchRange,
                                forceInteger,
                                true,
                                false,
                                referenceIndex,
                                referenceVector,
                                drlRate,
                                starts.AsSpan(0, startCount),
                                totalWeight,
                                ref state,
                                out Av1MotionSearchBase.FractionalResult result);

                            ref Av1MotionSearchBase.ReferenceSearchResult retained = ref state.References[referenceIndex];
                            Assert.Equal(valid, retained.IsValid);
                            if (referenceIndex == 0)
                            {
                                Assert.True(valid);
                                if (pattern == 0)
                                {
                                    Assert.Equal(new Av1MotionVector(24, -16), result.Vector);
                                    Assert.Equal(0, result.SquaredError);
                                }
                            }

                            if (valid && forceInteger)
                            {
                                Assert.Equal(0, result.Vector.Row & 7);
                                Assert.Equal(0, result.Vector.Column & 7);
                            }
                        }

                        Assert.Equal(originalSource, source);
                        Assert.Equal(originalReference, reference);
                        Assert.All(contexts, value => Assert.Equal(0, value));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks all search methods at each sample precision.
    /// </summary>
    /// <param name="bits">The coded component precision.</param>
    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void FullPixelSearchPublishesScalarVerifiedStatistics(int bits)
    {
        if (bits == 8)
        {
            VerifySearches<byte, Av1MotionSearchBase.ByteOperator>(Av1BitDepth.EightBit, bits);
        }
        else
        {
            VerifySearches<ushort, Av1MotionSearchBase.UInt16Operator>(bits == 10 ? Av1BitDepth.TenBit : Av1BitDepth.TwelveBit, bits);
        }
    }

    /// <summary>
    /// Checks all retained coordinate offsets after initial configuration and a stride change.
    /// </summary>
    [Fact]
    public void SearchSitesPreserveShapeAcrossStrideChanges()
    {
        using Av1EncoderBlockWorkspace workspace = new(Configuration.Default, allocateInterMotionCosts: true);
        for (int methodIndex = 0; methodIndex <= (int)FullPixelSearchMethod.VeryFastDiamond; methodIndex++)
        {
            FullPixelSearchMethod method = (FullPixelSearchMethod)methodIndex;
            foreach (int stride in new[] { 192, 224 })
            {
                Av1MotionSearchSites sites = workspace.GetMotionSearchSites(method, stride);
                Assert.Equal(method == FullPixelSearchMethod.NStep ? 15 : method == FullPixelSearchMethod.EightPointNStep ? 16 : 11, sites.StageCount);
                for (int stage = 0; stage < sites.StageCount; stage++)
                {
                    ReadOnlySpan<Av1MotionSearchSites.Site> entries = sites.GetSites(stage);
                    int first = method <= FullPixelSearchMethod.ClampedDiamond ? 1 : 0;
                    for (int index = first; index < first + sites.GetCandidateCount(stage); index++)
                    {
                        Assert.Equal((entries[index].Row * stride) + entries[index].Column, entries[index].Offset);
                    }
                }

                Assert.Equal(1, sites.GetRadius(0));
                int outerRadius = method is FullPixelSearchMethod.NStep or FullPixelSearchMethod.EightPointNStep
                    ? 210 : method == FullPixelSearchMethod.ClampedDiamond ? 256 : 1024;

                Assert.Equal(outerRadius, sites.GetRadius(sites.StageCount - 1));
            }
        }

        Assert.Equal(8, Unsafe.SizeOf<Av1MotionSearchSites.Site>());
        Assert.Equal(794, Av1MotionSearchSites.StorageLength);
    }

    /// <summary>
    /// Exercises exact matches, textured residuals, alternate-row policies, and fractional spatial references.
    /// </summary>
    private static void VerifySearches<TSample, TOperator>(Av1BitDepth bitDepth, int bits)
        where TSample : unmanaged, IBinaryInteger<TSample>
        where TOperator : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    {
        const int QIndex = 90;
        const int ReferenceStride = 192;
        const int ReferenceOrigin = (64 * ReferenceStride) + 64;
        int maximum = (1 << bits) - 1;
        using Av1EncoderBlockWorkspace workspace = new(Configuration.Default, allocateInterMotionCosts: true);
        using Av1SymbolEncoder writer = new(Configuration.Default, 64, QIndex, updateCdf: true);
        Av1MotionVectorCosts costs = workspace.GetMotionVectorCosts(Av1MotionVectorPrecision.EighthSample);
        writer.FillMotionVectorCosts(costs);
        int multiplier = Av1RateDistortion.GetKeyFrameRateMultiplier(QIndex, bitDepth);
        int sadPerBit = Av1RateDistortion.GetMotionSearchSadPerBit(QIndex, bitDepth);
        int[] costList = new int[5];
        foreach (int width in new[] { 8, 16, 64 })
        {
            int height = width;
            int sourceStride = width + 3;
            TSample[] source = new TSample[sourceStride * height];
            TSample[] reference = new TSample[ReferenceStride * ReferenceStride];
            for (int pattern = 0; pattern < 6; pattern++)
            {
                HeifEncodingSpeed speed = pattern is 0 or 3 or 4
                    ? HeifEncodingSpeed.Level0 : pattern == 1 ? HeifEncodingSpeed.Level5 : HeifEncodingSpeed.Level8;

                Size frameSize = pattern is 2 or 5 ? new Size(1280, 720) : new Size(320, 240);
                bool screenContent = pattern == 4;
                Av1MotionSearchSettings settings = new(speed, false, frameSize, QIndex, false, screenContent);
                uint state = (uint)(173 + pattern);
                for (int index = 0; index < reference.Length; index++)
                {
                    state = unchecked((state * 1664525) + 1013904223);
                    reference[index] = TSample.CreateChecked((state >> 16) & (uint)maximum);
                }

                Point start = pattern == 4 ? new(40, -40) : new(-2, 3);
                Point predictionOffset = pattern == 3 ? new(-1, 3) : pattern == 4 ? new(-24, 24) : start;
                Av1MotionVector referenceVector = pattern == 0 ? new Av1MotionVector(24, -16) : new Av1MotionVector(21, -13);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int sample = int.CreateChecked(reference[ReferenceOrigin + ((y + predictionOffset.Y) * ReferenceStride) + x + predictionOffset.X]);
                        if (pattern is 1 or 2)
                        {
                            // Independent signed perturbations retain nonzero prediction variance and high-depth rounding residues.
                            sample = (sample + ((x * 13) ^ (y * 37)) + pattern) & maximum;
                        }
                        else if (pattern == 5 && (y & 1) != 0)
                        {
                            // Even rows match exactly while odd rows disagree, forcing the alternate-row reliability check.
                            sample = maximum - sample;
                        }

                        source[(y * sourceStride) + x] = TSample.CreateChecked(sample);
                    }
                }

                Rectangle frameBounds = Rectangle.FromLTRB(-24, -24, 25, 25);
                Rectangle bounds = referenceVector.GetFullPixelSearchBounds(frameBounds);
                for (int methodIndex = 0; methodIndex <= (int)FullPixelSearchMethod.VeryFastDiamond; methodIndex++)
                {
                    FullPixelSearchMethod method = (FullPixelSearchMethod)methodIndex;
                    Av1MotionSearchSites sites = workspace.GetMotionSearchSites(method, ReferenceStride);
                    Av1MotionSearchBase.FullPixelSearch<TSample, TOperator> search = new(
                        source,
                        sourceStride,
                        reference,
                        ReferenceStride,
                        ReferenceOrigin,
                        new Size(width, height),
                        bounds,
                        referenceVector,
                        costs,
                        bitDepth,
                        sadPerBit,
                        multiplier);

                    Av1MotionSearchBase.FullPixelResult result = search.Search(
                        start, 5, method, sites, settings, false, false, costList, out Point? secondBest);

                    Assert.True(bounds.Contains(result.Vector));
                    if (secondBest.HasValue)
                    {
                        Assert.True(bounds.Contains(secondBest.Value));
                    }

                    long sum = 0;
                    long squares = 0;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int prediction = int.CreateChecked(
                                reference[ReferenceOrigin + ((y + result.Vector.Y) * ReferenceStride) + x + result.Vector.X]);

                            long residual = int.CreateChecked(source[(y * sourceStride) + x]) - prediction;
                            sum += residual;
                            squares += residual * residual;
                        }
                    }

                    if (bits != 8)
                    {
                        sum = (sum + (1L << (bits - 9))) >> (bits - 8);
                        squares = (squares + (1L << (((bits - 8) * 2) - 1))) >> ((bits - 8) * 2);
                    }

                    int expectedVariance = (int)Math.Max(squares - ((sum * sum) / (width * height)), 0);
                    Av1MotionVector resultVector = new(result.Vector.Y * 8, result.Vector.X * 8);
                    int syntaxRate = writer.GetMotionVectorCost(resultVector, referenceVector, Av1MotionVectorPrecision.EighthSample);
                    int expectedMotionCost = (int)((((long)syntaxRate * Math.Max(multiplier >> 6, 1)) + 8192) >> 14);
                    Assert.Equal(expectedVariance, result.Variance);
                    Assert.Equal((int)squares, result.SquaredError);
                    Assert.Equal(expectedMotionCost, result.MotionCost);
                    if (pattern == 0)
                    {
                        Assert.Equal(start, result.Vector);
                        Assert.Equal(0, result.SquaredError);
                    }

                    if (method == FullPixelSearchMethod.NStep)
                    {
                        VerifyFractionalSearches<TSample, TOperator>(
                            bitDepth,
                            pattern,
                            width,
                            source,
                            sourceStride,
                            reference,
                            ReferenceStride,
                            ReferenceOrigin,
                            frameBounds,
                            referenceVector,
                            costs,
                            multiplier,
                            result,
                            costList,
                            writer);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Exercises fractional policy, retained statistics, precision stops, and repeated-center termination.
    /// </summary>
    private static void VerifyFractionalSearches<TSample, TOperator>(
        Av1BitDepth bitDepth,
        int pattern,
        int width,
        TSample[] source,
        int sourceStride,
        TSample[] reference,
        int referenceStride,
        int referenceOrigin,
        Rectangle frameBounds,
        Av1MotionVector referenceVector,
        Av1MotionVectorCosts costs,
        int multiplier,
        Av1MotionSearchBase.FullPixelResult integerResult,
        int[] costList,
        Av1SymbolEncoder writer)
        where TSample : unmanaged
        where TOperator : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    {
        TSample[] prediction = new TSample[Av1TranslationalInterPredictor.SearchPredictionBufferLength];
        Av1MotionVector[] centers = new Av1MotionVector[3];
        Av1MotionVector start = new(integerResult.Vector.Y * 8, integerResult.Vector.X * 8);
        Rectangle bounds = referenceVector.GetSubpixelSearchBounds(frameBounds);
        Av1MotionSearchBase.FractionalSearch<TSample, TOperator> search = new(
            source,
            sourceStride,
            reference,
            referenceStride,
            referenceOrigin,
            prediction,
            new Size(width, width),
            bounds,
            referenceVector,
            costs,
            bitDepth,
            multiplier);

        foreach (FractionalSearchMethod method in Enum.GetValues<FractionalSearchMethod>())
        {
            foreach (int taps in new[] { 2, 4, 8 })
            {
                for (int variant = 0; variant < 4; variant++)
                {
                    SearchPrecision precision = (SearchPrecision)variant;
                    bool allowHighPrecision = (pattern & 1) == 0;
                    int iterations = (pattern & 1) + 1;
                    bool retainStatistics = pattern % 3 != 0;
                    bool retainCosts = pattern % 3 != 1;
                    Array.Fill(centers, new Av1MotionVector(short.MinValue, short.MinValue));
                    int cost = search.Search(
                        start,
                        retainStatistics ? integerResult : null,
                        method,
                        precision,
                        allowHighPrecision,
                        iterations,
                        taps,
                        retainCosts ? costList : ReadOnlySpan<int>.Empty,
                        centers,
                        out Av1MotionSearchBase.FractionalResult result);

                    Assert.True(bounds.Contains(result.Vector.Column, result.Vector.Row));
                    int syntaxRate = writer.GetMotionVectorCost(result.Vector, referenceVector, Av1MotionVectorPrecision.EighthSample);
                    int expectedMotionCost = (int)((((long)syntaxRate * Math.Max(multiplier >> 6, 1)) + 8192) >> 14);
                    Assert.Equal(expectedMotionCost, result.MotionCost);
                    Assert.Equal(result.Cost, cost);

                    // Repeating the same start must stop at the retained first precision center. Integer-only
                    // searches never visit that center and therefore still publish their ordinary total cost.
                    int repeatedCost = search.Search(
                        start,
                        retainStatistics ? integerResult : null,
                        method,
                        precision,
                        allowHighPrecision,
                        iterations,
                        taps,
                        retainCosts ? costList : ReadOnlySpan<int>.Empty,
                        centers,
                        out Av1MotionSearchBase.FractionalResult repeated);

                    Assert.Equal(precision == SearchPrecision.Integer ? cost : int.MaxValue, repeatedCost);
                }
            }
        }
    }
}
