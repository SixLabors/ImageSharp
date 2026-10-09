// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
    {
        /// <summary>
        /// The weight of one prediction in a blend, in 1/64 units. Reference: AOM_BLEND_A64_MAX_ALPHA.
        /// </summary>
        private const int BlendMaximumAlpha = 64;

        /// <summary>
        /// Searches a new vector for an OBMC block against the source with the neighbors' predictions removed: a
        /// full-sample refinement around the simple-translation vector, then the two-level fractional tree. The OBMC
        /// state must be armed for the block. Reference: av1_single_motion_search() with OBMC_CAUSAL, using
        /// calc_target_weighted_pred(), av1_obmc_full_pixel_search() and av1_find_best_obmc_sub_pixel_tree_up().
        /// </summary>
        /// <param name="motionSearchPrediction">The motion search prediction buffer.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="referenceFrame">The reference of the block.</param>
        /// <param name="start">The simple-translation vector that starts the search.</param>
        /// <param name="referenceVector">The reference of the new vector.</param>
        /// <param name="spatialMagnitude">The largest full-sample magnitude of the reference's spatial predictors.</param>
        /// <returns>The searched vector.</returns>
        private Av1MotionVector SearchObmcVector(
            Span<TSample> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1ReferenceFrameType referenceFrame,
            Av1MotionVector start,
            Av1MotionVector referenceVector,
            int spatialMagnitude)
        {
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Span<int> weightedSource = this.blockWorkspace.ObmcWeightedSource.AsSpan(0, width * height);
            Span<int> mask = this.blockWorkspace.ObmcMask.AsSpan(0, width * height);
            this.CalculateObmcTarget(
                blockSize, weightedSource, mask, filterRows, modeInfoGrid, modeInfoAllocation, displacementVectors, sourceLuma, sourceBlue, sourceRed);

            // The full-sample search reads a reference of another size through its copy resized to the frame size, and
            // the fractional search the reference itself. Reference: the scaled_ref_frame of
            // av1_single_motion_search().
            this.obmcSearchReference = referenceFrame;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Av1PlaneRegion<TSample> referencePlane = this.searchReferences.Span[(int)referenceFrame].CodedView.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> reference = referencePlane.Samples;
            int referenceOrigin = ((referencePlane.Bounds.Y + blockOrigin.Y) * referencePlane.Stride) + referencePlane.Bounds.X + blockOrigin.X;
            Size frameSize = new(
                this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2,
                this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(blockOrigin, new Size(width, height)),
                frameSize,
                Math.Min(referencePlane.Bounds.X, referencePlane.Bounds.Y));

            int sadPerBit = Av1RateDistortion.GetMotionSearchSadPerBit(this.blockQIndex, this.bitDepth);
            Av1MotionVector integerReference = new(
                ((referenceVector.Row + 3 + (referenceVector.Row >= 0 ? 1 : 0)) >> 3) * 8,
                ((referenceVector.Column + 3 + (referenceVector.Column >= 0 ? 1 : 0)) >> 3) * 8);

            // Full-sample refinement around the rounded start. Reference: the fast_obmc_search branch of
            // av1_obmc_full_pixel_search() with obmc_refining_search_sad().
            Rectangle fullBounds = referenceVector.GetFullPixelSearchBounds(frameBounds);
            Point best = new(
                Math.Clamp((start.Column + 3 + (start.Column >= 0 ? 1 : 0)) >> 3, fullBounds.Left, fullBounds.Right - 1),
                Math.Clamp((start.Row + 3 + (start.Row >= 0 ? 1 : 0)) >> 3, fullBounds.Top, fullBounds.Bottom - 1));

            int startRate = motionVectorCosts.GetCost(new Av1MotionVector(best.Y * 8, best.X * 8), integerReference);
            int bestSad = GetObmcSad(reference, referencePlane.Stride, referenceOrigin, best, weightedSource, mask, width, height) +
                Av1RateDistortion.GetMotionSearchSadCost(sadPerBit, startRate, 0);

            ReadOnlySpan<Point> neighbors = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];
            Av1MotionSearchSettings motionSettings = this.picture.Parent.MotionSearchSettings;
            if (!motionSettings.UseRefiningObmcSearch)
            {
                int stepParameter = this.picture.Parent.MotionSearchStepParameter;
                if (motionSettings.AutomaticStepSizeLevel != 0 && frameHeader.ShowFrame)
                {
                    stepParameter = (Av1MotionSearchBase.GetInitialStepParameter(spatialMagnitude) + stepParameter) / 2;
                }

                Av1MotionSearchSettings.FullPixelSearchMethod method = motionSettings.GetFullPixelMethod(blockSize);
                Av1MotionSearchSites sites = this.blockWorkspace.GetMotionSearchSites(workspaceStorage, method, referencePlane.Stride);
                best = this.SearchObmcDiamond(
                    reference,
                    referencePlane.Stride,
                    referenceOrigin,
                    best,
                    stepParameter,
                    sites,
                    fullBounds,
                    referenceVector,
                    integerReference,
                    in motionVectorCosts,
                    sadPerBit,
                    weightedSource,
                    mask,
                    width,
                    height);
            }
            else
            {
                for (int iteration = 0; iteration < 8; iteration++)
                {
                    int bestSite = -1;
                    for (int site = 0; site < neighbors.Length; site++)
                    {
                        Point candidate = new(best.X + neighbors[site].X, best.Y + neighbors[site].Y);
                        if (!fullBounds.Contains(candidate))
                        {
                            continue;
                        }

                        int sad = GetObmcSad(reference, referencePlane.Stride, referenceOrigin, candidate, weightedSource, mask, width, height);
                        if (sad < bestSad)
                        {
                            sad += Av1RateDistortion.GetMotionSearchSadCost(
                                sadPerBit, motionVectorCosts.GetCost(new Av1MotionVector(candidate.Y * 8, candidate.X * 8), integerReference), 0);

                            if (sad < bestSad)
                            {
                                bestSad = sad;
                                bestSite = site;
                            }
                        }
                    }

                    if (bestSite < 0)
                    {
                        break;
                    }

                    best = new Point(best.X + neighbors[bestSite].X, best.Y + neighbors[bestSite].Y);
                }
            }

            // Fractional tree with upsampled predictions. Reference: av1_find_best_obmc_sub_pixel_tree_up() with
            // obmc_first_level_check() and obmc_second_level_check_v2().
            Av1MotionSearchSettings settings = this.picture.Parent.MotionSearchSettings;
            Rectangle fractionalBounds = referenceVector.GetSubpixelSearchBounds(frameBounds);
            int taps = settings.FractionalInterpolationTaps;
            Av1MotionVector bestVector = new(best.Y * 8, best.X * 8);
            int bestError = this.GetObmcSubpixelCost(
                reference,
                referencePlane.Stride,
                referenceOrigin,
                bestVector,
                referenceVector,
                weightedSource,
                mask,
                motionSearchPrediction,
                filterRows,
                in motionVectorCosts,
                width,
                height,
                taps);

            int rounds = Math.Min(3 - (int)settings.FractionalPrecision, frameHeader.AllowHighPrecisionMotionVector ? 3 : 2);
            for (int iteration = 0, step = 4; iteration < rounds; iteration++, step >>= 1)
            {
                Av1MotionVector center = bestVector;
                int left = this.CheckObmcVector(
                    new Av1MotionVector(center.Row, center.Column - step),
                    fractionalBounds,
                    reference,
                    referencePlane.Stride,
                    referenceOrigin,
                    referenceVector,
                    weightedSource,
                    mask,
                    motionSearchPrediction,
                    filterRows,
                    in motionVectorCosts,
                    width,
                    height,
                    taps,
                    ref bestVector,
                    ref bestError,
                    out _);

                int right = this.CheckObmcVector(
                    new Av1MotionVector(center.Row, center.Column + step),
                    fractionalBounds,
                    reference,
                    referencePlane.Stride,
                    referenceOrigin,
                    referenceVector,
                    weightedSource,
                    mask,
                    motionSearchPrediction,
                    filterRows,
                    in motionVectorCosts,
                    width,
                    height,
                    taps,
                    ref bestVector,
                    ref bestError,
                    out _);

                int up = this.CheckObmcVector(
                    new Av1MotionVector(center.Row - step, center.Column),
                    fractionalBounds,
                    reference,
                    referencePlane.Stride,
                    referenceOrigin,
                    referenceVector,
                    weightedSource,
                    mask,
                    motionSearchPrediction,
                    filterRows,
                    in motionVectorCosts,
                    width,
                    height,
                    taps,
                    ref bestVector,
                    ref bestError,
                    out _);

                int down = this.CheckObmcVector(
                    new Av1MotionVector(center.Row + step, center.Column),
                    fractionalBounds,
                    reference,
                    referencePlane.Stride,
                    referenceOrigin,
                    referenceVector,
                    weightedSource,
                    mask,
                    motionSearchPrediction,
                    filterRows,
                    in motionVectorCosts,
                    width,
                    height,
                    taps,
                    ref bestVector,
                    ref bestError,
                    out _);

                int diagonalRow = up <= down ? -step : step;
                int diagonalColumn = left <= right ? -step : step;
                this.CheckObmcVector(
                    new Av1MotionVector(center.Row + diagonalRow, center.Column + diagonalColumn),
                    fractionalBounds,
                    reference,
                    referencePlane.Stride,
                    referenceOrigin,
                    referenceVector,
                    weightedSource,
                    mask,
                    motionSearchPrediction,
                    filterRows,
                    in motionVectorCosts,
                    width,
                    height,
                    taps,
                    ref bestVector,
                    ref bestError,
                    out _);

                if (bestVector != center && settings.FractionalIterationsPerStep > 1)
                {
                    if (bestVector.Row == center.Row)
                    {
                        diagonalRow = -diagonalRow;
                    }
                    else if (bestVector.Column == center.Column)
                    {
                        diagonalColumn = -diagonalColumn;
                    }

                    Av1MotionVector rowBias = new(bestVector.Row + diagonalRow, bestVector.Column);
                    Av1MotionVector columnBias = new(bestVector.Row, bestVector.Column + diagonalColumn);
                    Av1MotionVector diagonalBias = new(bestVector.Row + diagonalRow, bestVector.Column + diagonalColumn);
                    this.CheckObmcVector(
                        rowBias,
                        fractionalBounds,
                        reference,
                        referencePlane.Stride,
                        referenceOrigin,
                        referenceVector,
                        weightedSource,
                        mask,
                        motionSearchPrediction,
                        filterRows,
                        in motionVectorCosts,
                        width,
                        height,
                        taps,
                        ref bestVector,
                        ref bestError,
                        out bool rowBetter);

                    this.CheckObmcVector(
                        columnBias,
                        fractionalBounds,
                        reference,
                        referencePlane.Stride,
                        referenceOrigin,
                        referenceVector,
                        weightedSource,
                        mask,
                        motionSearchPrediction,
                        filterRows,
                        in motionVectorCosts,
                        width,
                        height,
                        taps,
                        ref bestVector,
                        ref bestError,
                        out bool columnBetter);

                    if (rowBetter || columnBetter)
                    {
                        this.CheckObmcVector(
                            diagonalBias,
                            fractionalBounds,
                            reference,
                            referencePlane.Stride,
                            referenceOrigin,
                            referenceVector,
                            weightedSource,
                            mask,
                            motionSearchPrediction,
                            filterRows,
                            in motionVectorCosts,
                            width,
                            height,
                            taps,
                            ref bestVector,
                            ref bestError,
                            out _);
                    }
                }
            }

            return bestVector;
        }

        /// <summary>
        /// Runs the diamond search from the start at the given step, then restarts it from the start at each finer
        /// step that the earlier searches did not settle at their start, and keeps the vector with the lowest OBMC
        /// variance plus vector cost. Reference: obmc_full_pixel_diamond() with get_obmc_mvpred_var().
        /// </summary>
        /// <param name="reference">The reference plane samples.</param>
        /// <param name="referenceStride">The reference plane stride.</param>
        /// <param name="referenceOrigin">The index of the block origin in the reference plane.</param>
        /// <param name="start">The full-sample vector that starts the search.</param>
        /// <param name="stepParameter">The coarsest search stage.</param>
        /// <param name="sites">The search sites of every stage.</param>
        /// <param name="bounds">The full-sample search range.</param>
        /// <param name="referenceVector">The reference of the new vector.</param>
        /// <param name="integerReference">The reference vector rounded to full samples.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="sadPerBit">The SAD weight of one bit of vector rate.</param>
        /// <param name="weightedSource">The source with the neighbor predictions removed.</param>
        /// <param name="mask">The OBMC blend weights.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <returns>The selected full-sample vector.</returns>
        private readonly Point SearchObmcDiamond(
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Point start,
            int stepParameter,
            Av1MotionSearchSites sites,
            Rectangle bounds,
            Av1MotionVector referenceVector,
            Av1MotionVector integerReference,
            in Av1MotionVectorCosts motionVectorCosts,
            int sadPerBit,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
        {
            Point best = SearchObmcDiamondSteps(
                reference,
                referenceStride,
                referenceOrigin,
                start,
                stepParameter,
                sites,
                bounds,
                integerReference,
                in motionVectorCosts,
                sadPerBit,
                weightedSource,
                mask,
                width,
                height,
                out int stage);

            int bestCost = this.GetObmcFullPixelCost(
                reference,
                referenceStride,
                referenceOrigin,
                best,
                referenceVector,
                in motionVectorCosts,
                weightedSource,
                mask,
                width,
                height);

            int furtherStages = sites.StageCount - 1 - stepParameter;
            int centeredStages = 0;
            while (stage < furtherStages)
            {
                stage++;
                if (centeredStages != 0)
                {
                    centeredStages--;
                    continue;
                }

                Point candidate = SearchObmcDiamondSteps(
                    reference,
                    referenceStride,
                    referenceOrigin,
                    start,
                    stepParameter + stage,
                    sites,
                    bounds,
                    integerReference,
                    in motionVectorCosts,
                    sadPerBit,
                    weightedSource,
                    mask,
                    width,
                    height,
                    out centeredStages);

                int cost = this.GetObmcFullPixelCost(
                    reference,
                    referenceStride,
                    referenceOrigin,
                    candidate,
                    referenceVector,
                    in motionVectorCosts,
                    weightedSource,
                    mask,
                    width,
                    height);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Visits the sites of each stage from the given step down to the finest, moving to the site with the lowest
        /// OBMC SAD plus vector cost, and counts the stages that leave the search at its start.
        /// Reference: obmc_diamond_search_sad().
        /// </summary>
        /// <param name="reference">The reference plane samples.</param>
        /// <param name="referenceStride">The reference plane stride.</param>
        /// <param name="referenceOrigin">The index of the block origin in the reference plane.</param>
        /// <param name="start">The full-sample vector that starts the search.</param>
        /// <param name="stepParameter">The coarsest search stage.</param>
        /// <param name="sites">The search sites of every stage.</param>
        /// <param name="bounds">The full-sample search range.</param>
        /// <param name="integerReference">The reference vector rounded to full samples.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="sadPerBit">The SAD weight of one bit of vector rate.</param>
        /// <param name="weightedSource">The source with the neighbor predictions removed.</param>
        /// <param name="mask">The OBMC blend weights.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="centeredStages">The number of stages that leave the search at its start.</param>
        /// <returns>The selected full-sample vector.</returns>
        private static Point SearchObmcDiamondSteps(
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Point start,
            int stepParameter,
            Av1MotionSearchSites sites,
            Rectangle bounds,
            Av1MotionVector integerReference,
            in Av1MotionVectorCosts motionVectorCosts,
            int sadPerBit,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height,
            out int centeredStages)
        {
            centeredStages = 0;
            Point best = start;
            int startRate = motionVectorCosts.GetCost(new Av1MotionVector(best.Y * 8, best.X * 8), integerReference);
            int bestSad = GetObmcSad(reference, referenceStride, referenceOrigin, best, weightedSource, mask, width, height) +
                Av1RateDistortion.GetMotionSearchSadCost(sadPerBit, startRate, 0);

            for (int stage = sites.StageCount - stepParameter - 1; stage >= 0; stage--)
            {
                ReadOnlySpan<Av1MotionSearchSites.Site> stageSites = sites.GetSites(stage);
                int bestSite = 0;
                for (int index = 1; index <= sites.GetCandidateCount(stage); index++)
                {
                    Point candidate = new(best.X + stageSites[index].Column, best.Y + stageSites[index].Row);
                    if (!bounds.Contains(candidate))
                    {
                        continue;
                    }

                    int sad = GetObmcSad(reference, referenceStride, referenceOrigin, candidate, weightedSource, mask, width, height);
                    if (sad < bestSad)
                    {
                        sad += Av1RateDistortion.GetMotionSearchSadCost(
                            sadPerBit, motionVectorCosts.GetCost(new Av1MotionVector(candidate.Y * 8, candidate.X * 8), integerReference), 0);

                        if (sad < bestSad)
                        {
                            bestSad = sad;
                            bestSite = index;
                        }
                    }
                }

                if (bestSite != 0)
                {
                    best = new Point(best.X + stageSites[bestSite].Column, best.Y + stageSites[bestSite].Row);
                }
                else if (best == start)
                {
                    centeredStages++;
                }
            }

            return best;
        }

        /// <summary>
        /// Returns the OBMC variance of the reference block at a full-sample vector plus the vector cost.
        /// Reference: get_obmc_mvpred_var().
        /// </summary>
        /// <param name="reference">The reference plane samples.</param>
        /// <param name="referenceStride">The reference plane stride.</param>
        /// <param name="referenceOrigin">The index of the block origin in the reference plane.</param>
        /// <param name="vector">The full-sample vector.</param>
        /// <param name="referenceVector">The reference of the new vector.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="weightedSource">The source with the neighbor predictions removed.</param>
        /// <param name="mask">The OBMC blend weights.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <returns>The variance plus the vector cost.</returns>
        private readonly int GetObmcFullPixelCost(
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Point vector,
            Av1MotionVector referenceVector,
            in Av1MotionVectorCosts motionVectorCosts,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
        {
            Av1MotionVector fullVector = new(vector.Y * 8, vector.X * 8);
            int index = referenceOrigin + (vector.Y * referenceStride) + vector.X;
            int variance = this.GetObmcVariance(reference[index..], referenceStride, weightedSource, mask, width, height);
            return variance + Av1RateDistortion.GetMotionSearchCost(this.rateMultiplier, motionVectorCosts.GetCost(fullVector, referenceVector), 0);
        }

        /// <summary>
        /// Measures one fractional candidate and keeps it when it lowers the cost. Reference: obmc_check_better().
        /// </summary>
        /// <param name="vector">The fractional candidate vector.</param>
        /// <param name="bounds">The fractional search range.</param>
        /// <param name="reference">The reference plane samples.</param>
        /// <param name="referenceStride">The reference plane stride.</param>
        /// <param name="referenceOrigin">The index of the block origin in the reference plane.</param>
        /// <param name="referenceVector">The reference of the new vector.</param>
        /// <param name="weightedSource">The source with the neighbor predictions removed.</param>
        /// <param name="mask">The OBMC blend weights.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the candidate.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="taps">The interpolation filter length.</param>
        /// <param name="bestVector">The best vector so far.</param>
        /// <param name="bestError">The cost of the best vector so far.</param>
        /// <param name="improved">Whether the candidate replaced the best vector.</param>
        /// <returns>The candidate cost, or <see cref="int.MaxValue"/> when it lies outside the search range.</returns>
        private readonly int CheckObmcVector(
            Av1MotionVector vector,
            Rectangle bounds,
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Av1MotionVector referenceVector,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            Span<TSample> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            int width,
            int height,
            int taps,
            ref Av1MotionVector bestVector,
            ref int bestError,
            out bool improved)
        {
            improved = false;
            if (!bounds.Contains(vector.Column, vector.Row))
            {
                return int.MaxValue;
            }

            int cost = this.GetObmcSubpixelCost(
                reference,
                referenceStride,
                referenceOrigin,
                vector,
                referenceVector,
                weightedSource,
                mask,
                motionSearchPrediction,
                filterRows,
                in motionVectorCosts,
                width,
                height,
                taps);

            if (cost < bestError)
            {
                bestError = cost;
                bestVector = vector;
                improved = true;
            }

            return cost;
        }

        /// <summary>
        /// Returns the OBMC variance of the upsampled prediction at a vector plus the vector cost.
        /// Reference: upsampled_obmc_pref_error() and mv_err_cost_().
        /// </summary>
        /// <param name="reference">The reference plane samples.</param>
        /// <param name="referenceStride">The reference plane stride.</param>
        /// <param name="referenceOrigin">The index of the block origin in the reference plane.</param>
        /// <param name="vector">The fractional vector.</param>
        /// <param name="referenceVector">The reference of the new vector.</param>
        /// <param name="weightedSource">The source with the neighbor predictions removed.</param>
        /// <param name="mask">The OBMC blend weights.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the vector.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="taps">The interpolation filter length.</param>
        /// <returns>The variance plus the vector cost.</returns>
        private readonly int GetObmcSubpixelCost(
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Av1MotionVector vector,
            Av1MotionVector referenceVector,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            Span<TSample> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            int width,
            int height,
            int taps)
        {
            if (this.IsScaledReference(this.obmcSearchReference))
            {
                // A scaled reference predicts each candidate from the reference itself with its scale factors.
                // Reference: aom_upsampled_pred_scaled() in upsampled_obmc_pref_error().
                this.GetScaledSearchReference(this.obmcSearchReference, this.obmcBlockOrigin, filterRows)
                    .Predict<TOperator>(vector, motionSearchPrediction, new Size(width, height), this.bitDepth.GetBitCount());
            }
            else
            {
                int index = referenceOrigin + ((vector.Row >> 3) * referenceStride) + (vector.Column >> 3);
                TOperator.Predict(
                    reference,
                    referenceStride,
                    index,
                    motionSearchPrediction,
                    width,
                    height,
                    vector.Column & 7,
                    vector.Row & 7,
                    taps,
                    this.bitDepth.GetBitCount());
            }

            int variance = this.GetObmcVariance(motionSearchPrediction, width, weightedSource, mask, width, height);
            return variance + Av1RateDistortion.GetMotionSearchCost(this.rateMultiplier, motionVectorCosts.GetCost(vector, referenceVector), 0);
        }

        /// <summary>
        /// Returns the OBMC SAD of the reference block at a full-sample vector. Reference: aom_obmc_sad and
        /// aom_highbd_obmc_sad.
        /// </summary>
        private static int GetObmcSad(
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Point vector,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
        {
            int index = referenceOrigin + (vector.Y * referenceStride) + vector.X;
            return TOperator.SumObmcAbsoluteDifferences(reference[index..], referenceStride, weightedSource, mask, width, height);
        }

        /// <summary>
        /// Returns the OBMC variance of a packed prediction, normalized to eight bits for higher depths.
        /// Reference: aom_obmc_variance and aom_highbd_{8,10,12}_obmc_variance.
        /// </summary>
        private readonly int GetObmcVariance(
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
        {
            TOperator.GetObmcMoments(prediction, predictionStride, weightedSource, mask, width, height, out int sum, out ulong squares);
            int shift = (this.bitDepth.GetBitCount() - 8) * 2;
            if (shift == 0)
            {
                uint sse = (uint)squares;
                return (int)(sse - (uint)((long)sum * sum / (width * height)));
            }

            int normalizedSum = (int)((sum + (1L << ((shift >> 1) - 1))) >> (shift >> 1));
            uint normalizedSquares = (uint)((squares + (1UL << (shift - 1))) >> shift);
            long variance = normalizedSquares - ((long)normalizedSum * normalizedSum / (width * height));
            return variance >= 0 ? (int)variance : 0;
        }

        /// <summary>
        /// Builds the OBMC search target of the luma block: the source scaled by 64 * 64 minus the weighted above and
        /// left neighbor predictions, and the matching weight of the block's own prediction.
        /// Reference: calc_target_weighted_pred() with calc_target_weighted_pred_above() and
        /// calc_target_weighted_pred_left().
        /// </summary>
        /// <param name="blockSize">The block size.</param>
        /// <param name="weightedSource">Receives the weighted source, one entry per luma sample.</param>
        /// <param name="mask">Receives the prediction weights, one entry per luma sample.</param>
        /// <param name="filterRows">The intermediate rows of the neighbor prediction filters.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        private void CalculateObmcTarget(
            Av1BlockSize blockSize,
            Span<int> weightedSource,
            Span<int> mask,
            Span<short> filterRows,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed)
        {
            ReadOnlySpan<int> maximumNeighbors = [0, 1, 2, 3, 4, 4];
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Point position = new(this.obmcBlockOrigin.X >> Av1Constants.ModeInfoSizeLog2, this.obmcBlockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Av1BitDepth bitDepth = this.picture.Sequence.SequenceHeader.ColorConfig.BitDepth;
            Span<TSample> neighborPrediction = stackalloc TSample[64 * 32];
            weightedSource.Clear();
            mask.Fill(BlendMaximumAlpha);

            // A neighbor and its vector share the allocation index at the grid cell.
            int stride = this.picture.ModeInfoStride;
            if (this.obmcAboveAvailable)
            {
                int overlap = Math.Min(height, 64) >> 1;
                ReadOnlySpan<byte> weights = Av1ObmcMask.Get(overlap);
                int endColumn = Math.Min(position.X + blockSize.Get4x4WideCount(), this.picture.Parent.FrameHeader.ModeInfoColumnCount);
                int limit = maximumNeighbors[BitOperations.Log2((uint)blockSize.Get4x4WideCount())];
                int count = 0;
                int step;
                for (int column = position.X; column < endColumn && count < limit; column += step)
                {
                    int neighborIndex = modeInfoGrid[((position.Y - 1) * stride) + column];
                    step = Math.Min(modeInfoAllocation[neighborIndex].Block.BlockSize.Get4x4WideCount(), 16);
                    if (step == 1)
                    {
                        column &= ~1;
                        neighborIndex = modeInfoGrid[((position.Y - 1) * stride) + column + 1];
                        step = 2;
                    }

                    ref readonly Av1EncoderBlockModeInfo neighbor = ref modeInfoAllocation[neighborIndex].Block;
                    if (neighbor.ReferenceFrame <= Av1ReferenceFrameType.Intra && !neighbor.UseIntraBlockCopy)
                    {
                        continue;
                    }

                    count++;
                    int neighborWidth = Math.Min(blockSize.Get4x4WideCount(), step) << Av1Constants.ModeInfoSizeLog2;
                    int predictionHeight = Math.Clamp(height >> 1, 4, 32);
                    int blockColumn = (column - position.X) << Av1Constants.ModeInfoSizeLog2;
                    this.PredictObmcNeighbor(
                        Av1Plane.Y,
                        0,
                        0,
                        new Av1MotionVector(displacementVectors[neighborIndex].Row, displacementVectors[neighborIndex].Column),
                        neighbor,
                        new Point(column << Av1Constants.ModeInfoSizeLog2, position.Y << Av1Constants.ModeInfoSizeLog2),
                        neighborWidth,
                        predictionHeight,
                        neighborPrediction,
                        filterRows,
                        bitDepth);

                    for (int row = 0; row < overlap; row++)
                    {
                        int offset = (row * width) + blockColumn;
                        TOperator.WeightObmcAbove(
                            neighborPrediction[(row * neighborWidth)..],
                            weights[row],
                            weightedSource[offset..],
                            mask[offset..],
                            neighborWidth);
                    }
                }
            }

            TOperator.ScaleObmcTarget(weightedSource, mask);

            if (this.obmcLeftAvailable)
            {
                int overlap = Math.Min(width, 64) >> 1;
                ReadOnlySpan<byte> weights = Av1ObmcMask.Get(overlap);
                int endRow = Math.Min(position.Y + blockSize.Get4x4HighCount(), this.picture.Parent.FrameHeader.ModeInfoRowCount);
                int limit = maximumNeighbors[BitOperations.Log2((uint)blockSize.Get4x4HighCount())];
                int count = 0;
                int step;
                for (int row = position.Y; row < endRow && count < limit; row += step)
                {
                    int neighborIndex = modeInfoGrid[(row * stride) + position.X - 1];
                    step = Math.Min(modeInfoAllocation[neighborIndex].Block.BlockSize.Get4x4HighCount(), 16);
                    if (step == 1)
                    {
                        row &= ~1;
                        neighborIndex = modeInfoGrid[((row + 1) * stride) + position.X - 1];
                        step = 2;
                    }

                    ref readonly Av1EncoderBlockModeInfo neighbor = ref modeInfoAllocation[neighborIndex].Block;
                    if (neighbor.ReferenceFrame <= Av1ReferenceFrameType.Intra && !neighbor.UseIntraBlockCopy)
                    {
                        continue;
                    }

                    count++;
                    int neighborHeight = Math.Min(blockSize.Get4x4HighCount(), step) << Av1Constants.ModeInfoSizeLog2;
                    int predictionWidth = Math.Clamp(width >> 1, 4, 32);
                    int blockRow = (row - position.Y) << Av1Constants.ModeInfoSizeLog2;
                    this.PredictObmcNeighbor(
                        Av1Plane.Y,
                        0,
                        0,
                        new Av1MotionVector(displacementVectors[neighborIndex].Row, displacementVectors[neighborIndex].Column),
                        neighbor,
                        new Point(position.X << Av1Constants.ModeInfoSizeLog2, row << Av1Constants.ModeInfoSizeLog2),
                        predictionWidth,
                        neighborHeight,
                        neighborPrediction,
                        filterRows,
                        bitDepth);

                    for (int y = 0; y < neighborHeight; y++)
                    {
                        int offset = (blockRow + y) * width;
                        TOperator.WeightObmcLeft(neighborPrediction[(y * predictionWidth)..], weights, weightedSource[offset..], mask[offset..]);
                    }
                }
            }

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> sourceSamples = Av1TransformBlockEncoder.GetPlaneSpan(sourceLuma, sourcePlane, this.obmcBlockOrigin);
            for (int row = 0; row < height; row++)
            {
                TOperator.SubtractObmcSource(sourceSamples[(row * sourcePlane.Stride)..], weightedSource.Slice(row * width, width));
            }
        }
    }
}
