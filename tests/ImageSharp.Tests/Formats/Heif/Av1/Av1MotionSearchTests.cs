// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
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
    /// Checks all search methods at each sample precision.
    /// </summary>
    /// <param name="bits">The coded component precision.</param>
    [Theory]
    [InlineData(8)]
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
        using Av1EncoderBlockWorkspace workspace = new(Configuration.Default, allocateInterMotionCosts: true, allocateDisplacementCosts: false, Av1BlockSize.Block64x64);
        using Av1SymbolEncoder writer = new(Configuration.Default, QIndex, updateCdf: true);
        Av1MotionVectorCosts costs = workspace.GetMotionVectorCosts(Av1MotionVectorPrecision.EighthSample);
        writer.FillMotionVectorCosts(costs);
        int multiplier = Av1RateDistortion.GetRateMultiplier(QIndex, bitDepth, Av1FrameUpdateType.Key, tuning: Av1Tuning.Psnr, realtime: false);
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
                Av1MotionSearchSettings settings = new(speed, false, frameSize, QIndex, false, screenContent, tuning: Av1Tuning.Psnr);
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
                        multiplier,
                        [],
                        []);

                    Av1MotionSearchBase.FullPixelResult result = search.Search(
                        start, 5, method, sites, settings, false, false, false, costList, out Point? secondBest, forceMesh: false, meshPruneDistance: null);

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
            multiplier,
            [],
            [],
            scaledReference: default);

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
