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
        /// The full weight of one prediction in a blend, in 1/64 units.
        /// </summary>
        private const int BlendMaximumAlpha = 64;

        /// <summary>
        /// Searches a new vector for an OBMC block against the source with the neighbor predictions removed. A full-sample search
        /// around the simple-translation vector comes first, then the two-level fractional tree. The OBMC state must be armed for the block.
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

            // For a reference of another size, the full-sample search reads the copy that is resized to the frame size. The fractional
            // search reads the original reference.
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

            // The full-sample stage starts at the rounded start vector. It runs a diamond search, or, when the settings select the
            // refining search, it moves to the best of the four nearest neighbors for at most eight steps.
            Rectangle fullBounds = referenceVector.GetFullPixelSearchBounds(frameBounds);
            Point best = new(
                Math.Clamp((start.Column + 3 + (start.Column >= 0 ? 1 : 0)) >> 3, fullBounds.Left, fullBounds.Right - 1),
                Math.Clamp((start.Row + 3 + (start.Row >= 0 ? 1 : 0)) >> 3, fullBounds.Top, fullBounds.Bottom - 1));

            ObmcCost cost = new(
                reference,
                referencePlane.Stride,
                referenceOrigin,
                weightedSource,
                mask,
                width,
                height,
                motionVectorCosts,
                referenceVector,
                integerReference,
                sadPerBit,
                this.rateMultiplier,
                this.bitDepth.GetBitCount());

            Av1MotionSearchSettings motionSettings = this.picture.Parent.MotionSearchSettings;
            if (!motionSettings.UseRefiningObmcSearch)
            {
                int stepParameter = this.picture.Parent.MotionSearchStepParameter;
                if (motionSettings.AutomaticStepSizeLevel != 0 && frameHeader.ShowFrame)
                {
                    stepParameter = (Av1MotionSearchBase.GetInitialStepParameter(spatialMagnitude) + stepParameter) / 2;
                }

                // The OBMC diamond search visits every stage, also stages that repeat the radius of a stage where it stayed at its center.
                // It does not use the preceding winner.
                Av1MotionSearchSettings.FullPixelSearchMethod method = motionSettings.GetFullPixelMethod(blockSize);
                Av1MotionSearchSites sites = this.blockWorkspace.GetMotionSearchSites(workspaceStorage, method, referencePlane.Stride);
                Point? secondBest = null;
                best = Av1MotionSearchBase.SearchDiamond(ref cost, best, stepParameter, sites, fullBounds, false, ref secondBest).Vector;
            }
            else
            {
                ReadOnlySpan<Point> neighbors = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];
                int bestSad = Av1MotionSearchBase.GetSadCost(ref cost, best);
                for (int iteration = 0; iteration < 8; iteration++)
                {
                    int bestSite = -1;
                    for (int site = 0; site < neighbors.Length; site++)
                    {
                        Point candidate = new(best.X + neighbors[site].X, best.Y + neighbors[site].Y);
                        if (fullBounds.Contains(candidate) &&
                            Av1MotionSearchBase.TryImproveSad(ref cost, candidate, cost.GetReferenceIndex(candidate), ref bestSad))
                        {
                            bestSite = site;
                        }
                    }

                    if (bestSite < 0)
                    {
                        break;
                    }

                    best = new Point(best.X + neighbors[bestSite].X, best.Y + neighbors[bestSite].Y);
                }
            }

            // The fractional stage searches a tree of candidates with upsampled predictions.
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
        /// Measures one fractional candidate and keeps it when it lowers the cost.
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
                // A scaled reference predicts each candidate from the original reference with its scale factors.
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

            int variance = GetObmcVariance(motionSearchPrediction, width, weightedSource, mask, width, height, this.bitDepth.GetBitCount(), out _);
            return variance + Av1RateDistortion.GetMotionSearchCost(this.rateMultiplier, motionVectorCosts.GetCost(vector, referenceVector), 0);
        }

        /// <summary>
        /// Returns the OBMC variance of a packed prediction. For bit depths above eight, the moments round to the 8-bit scale first.
        /// </summary>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="predictionStride">The prediction stride.</param>
        /// <param name="weightedSource">The source with the neighbor predictions removed.</param>
        /// <param name="mask">The OBMC blend weights.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="bitCount">The coded sample precision in bits.</param>
        /// <param name="squaredError">Receives the weighted squared error at the 8-bit scale.</param>
        /// <returns>The OBMC variance. A negative result after the rounding becomes zero.</returns>
        private static int GetObmcVariance(
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height,
            int bitCount,
            out int squaredError)
        {
            TOperator.GetObmcMoments(prediction, predictionStride, weightedSource, mask, width, height, out int sum, out ulong squares);
            int shift = (bitCount - 8) * 2;
            if (shift == 0)
            {
                // At eight bits the squared error and the variance are unsigned 32-bit values. The subtraction wraps instead of clamping at zero.
                uint sse = (uint)squares;
                squaredError = (int)sse;
                return (int)(sse - (uint)((long)sum * sum / (width * height)));
            }

            int normalizedSum = (int)((sum + (1L << ((shift >> 1) - 1))) >> (shift >> 1));
            uint normalizedSquares = (uint)((squares + (1UL << (shift - 1))) >> shift);
            squaredError = (int)normalizedSquares;
            long variance = normalizedSquares - ((long)normalizedSum * normalizedSum / (width * height));
            return variance >= 0 ? (int)variance : 0;
        }

        /// <summary>
        /// Builds the OBMC search target of the luma block. The target is the source scaled by 64 * 64 minus the weighted above and left
        /// neighbor predictions. The mask holds the matching weight of the prediction of the block itself.
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

        /// <summary>
        /// Measures the OBMC error of a full-sample candidate for the shared diamond search and for the refining search. The error compares
        /// the reference block, weighted by the OBMC mask, with the source that has the neighbor predictions removed.
        /// </summary>
        private readonly ref struct ObmcCost : Av1MotionSearchBase.IFullPixelCost
        {
            /// <summary>
            /// The reference plane samples.
            /// </summary>
            private readonly ReadOnlySpan<TSample> reference;

            /// <summary>
            /// The reference plane stride.
            /// </summary>
            private readonly int referenceStride;

            /// <summary>
            /// The index of the block origin in the reference plane.
            /// </summary>
            private readonly int referenceOrigin;

            /// <summary>
            /// The source with the neighbor predictions removed.
            /// </summary>
            private readonly ReadOnlySpan<int> weightedSource;

            /// <summary>
            /// The OBMC blend weights.
            /// </summary>
            private readonly ReadOnlySpan<int> mask;

            /// <summary>
            /// The block width.
            /// </summary>
            private readonly int width;

            /// <summary>
            /// The block height.
            /// </summary>
            private readonly int height;

            /// <summary>
            /// The motion vector rates of the frame precision.
            /// </summary>
            private readonly Av1MotionVectorCosts costs;

            /// <summary>
            /// The reference of the variance-domain vector cost, in eighth samples.
            /// </summary>
            private readonly Av1MotionVector referenceVector;

            /// <summary>
            /// The reference vector rounded to full samples, in eighth samples. The absolute-difference vector cost uses it.
            /// </summary>
            private readonly Av1MotionVector integerReference;

            /// <summary>
            /// The SAD weight of one bit of vector rate.
            /// </summary>
            private readonly int sadPerBit;

            /// <summary>
            /// The rate multiplier of the variance domain.
            /// </summary>
            private readonly int rateMultiplier;

            /// <summary>
            /// The coded sample precision in bits.
            /// </summary>
            private readonly int bitCount;

            /// <summary>
            /// Initializes a new instance of the <see cref="ObmcCost"/> struct.
            /// </summary>
            /// <param name="reference">The reference plane samples.</param>
            /// <param name="referenceStride">The reference plane stride.</param>
            /// <param name="referenceOrigin">The index of the block origin in the reference plane.</param>
            /// <param name="weightedSource">The source with the neighbor predictions removed.</param>
            /// <param name="mask">The OBMC blend weights.</param>
            /// <param name="width">The block width.</param>
            /// <param name="height">The block height.</param>
            /// <param name="costs">The motion vector rates of the frame precision.</param>
            /// <param name="referenceVector">The reference of the variance-domain vector cost, in eighth samples.</param>
            /// <param name="integerReference">The reference vector rounded to full samples, in eighth samples.</param>
            /// <param name="sadPerBit">The SAD weight of one bit of vector rate.</param>
            /// <param name="rateMultiplier">The rate multiplier of the variance domain.</param>
            /// <param name="bitCount">The coded sample precision in bits.</param>
            public ObmcCost(
                ReadOnlySpan<TSample> reference,
                int referenceStride,
                int referenceOrigin,
                ReadOnlySpan<int> weightedSource,
                ReadOnlySpan<int> mask,
                int width,
                int height,
                Av1MotionVectorCosts costs,
                Av1MotionVector referenceVector,
                Av1MotionVector integerReference,
                int sadPerBit,
                int rateMultiplier,
                int bitCount)
            {
                this.reference = reference;
                this.referenceStride = referenceStride;
                this.referenceOrigin = referenceOrigin;
                this.weightedSource = weightedSource;
                this.mask = mask;
                this.width = width;
                this.height = height;
                this.costs = costs;
                this.referenceVector = referenceVector;
                this.integerReference = integerReference;
                this.sadPerBit = sadPerBit;
                this.rateMultiplier = rateMultiplier;
                this.bitCount = bitCount;
            }

            /// <summary>
            /// Gets the reference index of a candidate.
            /// </summary>
            /// <param name="vector">The full-sample vector.</param>
            /// <returns>The reference index of the displaced block origin.</returns>
            public int GetReferenceIndex(Point vector) => this.referenceOrigin + (vector.Y * this.referenceStride) + vector.X;

            /// <summary>
            /// Measures the OBMC SAD of the reference block at a reference index.
            /// </summary>
            /// <param name="referenceIndex">The reference index of the displaced block origin.</param>
            /// <returns>The OBMC SAD.</returns>
            public int GetSad(int referenceIndex)
                => TOperator.SumObmcAbsoluteDifferences(
                    this.reference[referenceIndex..], this.referenceStride, this.weightedSource, this.mask, this.width, this.height);

            /// <summary>
            /// Gets the absolute-difference vector cost of a candidate relative to the rounded reference vector.
            /// </summary>
            /// <param name="vector">The full-sample vector.</param>
            /// <returns>The vector cost, which is never negative.</returns>
            public int GetSadRateCost(Point vector)
            {
                int rate = this.costs.GetCost(new Av1MotionVector(vector.Y * 8, vector.X * 8), this.integerReference);
                return Av1RateDistortion.GetMotionSearchSadCost(this.sadPerBit, rate, 0);
            }

            /// <summary>
            /// Measures the OBMC variance of the reference block at a full-sample vector and its variance-domain vector cost.
            /// </summary>
            /// <param name="vector">The full-sample vector.</param>
            /// <returns>The OBMC variance, the weighted squared error and the vector cost.</returns>
            public Av1MotionSearchBase.FullPixelResult GetVarianceResult(Point vector)
            {
                int variance = GetObmcVariance(
                    this.reference[this.GetReferenceIndex(vector)..],
                    this.referenceStride,
                    this.weightedSource,
                    this.mask,
                    this.width,
                    this.height,
                    this.bitCount,
                    out int squaredError);

                int rate = this.costs.GetCost(new Av1MotionVector(vector.Y * 8, vector.X * 8), this.referenceVector);
                int motionCost = Av1RateDistortion.GetMotionSearchCost(this.rateMultiplier, rate, 0);
                return new Av1MotionSearchBase.FullPixelResult(vector, variance, squaredError, motionCost);
            }
        }
    }
}
