// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// Measures the units of one row. Every unit gets intra prediction. On inter frames, every unit also gets prediction from the three stage
/// references and the previous source.
/// </content>
internal sealed partial class Av1FirstPass<TSample, TOperator>
{
    /// <summary>
    /// Measures the units of one row inside one tile from left to right. The best vector of each unit starts the search of the next unit. The
    /// last nonzero vector after the first unit of the row seeds the next row of the tile.
    /// </summary>
    /// <param name="frame">The frame being measured.</param>
    /// <param name="unitRow">The unit row.</param>
    /// <param name="tileUnitRowStart">The first unit row of the tile.</param>
    /// <param name="tileColumnStart">The first mode-info column of the tile.</param>
    /// <param name="tileColumnEnd">The mode-info column after the tile.</param>
    /// <param name="firstTopMotionVector">The last nonzero vector left by the first unit of the previous row of the tile.</param>
    private void ProcessRow(
        ref FrameContext frame,
        int unitRow,
        int tileUnitRowStart,
        int tileColumnStart,
        int tileColumnEnd,
        ref Av1MotionVector firstTopMotionVector)
    {
        int unitWidth = 1 << frame.UnitLog2;
        int unitSize = unitWidth << 2;
        int unitColumnStart = tileColumnStart >> frame.UnitLog2;
        int unitColumnsInTile = (tileColumnEnd - tileColumnStart + unitWidth - 1) >> frame.UnitLog2;
        int recordStart = (unitRow * frame.UnitColumns) + unitColumnStart;
        Span<FrameStatistics> records = this.unitStatistics.AsSpan(recordStart, unitColumnsInTile);
        Span<int> rawMotionErrors = this.rawMotionErrors.AsSpan(recordStart, unitColumnsInTile);
        int rawMotionErrorCount = 0;
        Av1MotionVector bestReferenceVector = default;
        Av1MotionVector lastNonZeroVector = default;

        // Prediction never reads across a tile edge.
        bool upAvailable = unitRow != tileUnitRowStart;

        // The vector limits keep a whole unit plus two interpolation margins inside the reference border.
        this.SetMotionVectorRowLimits(unitRow << frame.UnitLog2, unitSize >> 2);

        // Zero the 16x16 residual once per row. Smaller units leave the rest of it unwritten, and the intra error of every unit sums all of it.
        this.residual.AsSpan().Clear();

        if (this.calculateWaveletEnergy)
        {
            this.AddWaveletEnergies(ref frame, unitRow, unitColumnStart, unitColumnsInTile, records);
        }

        for (int unitColumnInTile = 0; unitColumnInTile < unitColumnsInTile; unitColumnInTile++)
        {
            int unitColumn = unitColumnStart + unitColumnInTile;
            if (unitColumnInTile == 0)
            {
                lastNonZeroVector = firstTopMotionVector;
            }

            ref FrameStatistics record = ref records[unitColumnInTile];
            int intraError = this.PredictIntra(ref frame, unitRow, unitColumn, upAvailable, unitColumnInTile != 0, ref record);

            if (!frame.IntraOnly)
            {
                int interError = this.PredictInter(
                    ref frame,
                    unitRow,
                    unitColumn,
                    intraError,
                    ref rawMotionErrors[rawMotionErrorCount],
                    bestReferenceVector,
                    out bestReferenceVector,
                    ref lastNonZeroVector,
                    ref record);

                if (unitColumnInTile == 0)
                {
                    firstTopMotionVector = lastNonZeroVector;
                }

                record.CodedError += interError;
                rawMotionErrorCount++;
            }
            else
            {
                record.SecondReferenceCodedError += intraError;
                record.CodedError += intraError;
                record.LongTermCodedError += intraError;
            }
        }
    }

    /// <summary>
    /// Gets the block measured for a unit. A unit whose right or bottom half lies outside the frame keeps only its inside half or quarter.
    /// </summary>
    /// <param name="firstPassBlockSize">The unit size.</param>
    /// <param name="unitLog2">The base-two logarithm of the unit size in 4x4 units.</param>
    /// <param name="unitRow">The unit row.</param>
    /// <param name="unitColumn">The unit column.</param>
    /// <returns>The measured block size.</returns>
    private Av1BlockSize GetBlockSize(Av1BlockSize firstPassBlockSize, int unitLog2, int unitRow, int unitColumn)
    {
        int unitSize = 1 << unitLog2;
        bool isHalfWidth = (unitSize * unitColumn) + (unitSize / 2) >= this.miColumns;
        bool isHalfHeight = (unitSize * unitRow) + (unitSize / 2) >= this.miRows;
        if (isHalfWidth && isHalfHeight)
        {
            return Av1PartitionType.Split.GetBlockSubSize(firstPassBlockSize);
        }

        if (isHalfWidth)
        {
            return Av1PartitionType.Vertical.GetBlockSubSize(firstPassBlockSize);
        }

        return isHalfHeight ? Av1PartitionType.Horizontal.GetBlockSubSize(firstPassBlockSize) : firstPassBlockSize;
    }

    /// <summary>
    /// Measures the DC intra prediction error of a unit with 4x4 transforms and adds its skip, image-start, intra and brightness contributions.
    /// At the speeds that disable reconstruction, the prediction reads the source and the source becomes the reconstruction. Otherwise every
    /// transform block is coded and reconstructed at the first-pass quantizer.
    /// </summary>
    /// <param name="frame">The frame being measured.</param>
    /// <param name="unitRow">The unit row.</param>
    /// <param name="unitColumn">The unit column.</param>
    /// <param name="upAvailable">Whether the row above is inside the tile.</param>
    /// <param name="leftAvailable">Whether the unit to the left is inside the tile.</param>
    /// <param name="record">The unit record.</param>
    /// <returns>The intra error including the intra surcharge.</returns>
    private int PredictIntra(ref FrameContext frame, int unitRow, int unitColumn, bool upAvailable, bool leftAvailable, ref FrameStatistics record)
    {
        Av1BlockSize blockSize = this.GetBlockSize(frame.FirstPassBlockSize, frame.UnitLog2, unitRow, unitColumn);
        int blockWidth = blockSize.GetWidth();
        int blockHeight = blockSize.GetHeight();
        int x = (unitColumn << frame.UnitLog2) << 2;
        int y = (unitRow << frame.UnitLog2) << 2;

        // Border padding measures the distance to the visible frame. Without border padding, the distance is to the eight-aligned coded frame.
        int boundaryWidth = this.doBorderPad ? this.width : this.miColumns << 2;
        int boundaryHeight = this.doBorderPad ? this.height : this.miRows << 2;
        this.pixelsToRightEdge = boundaryWidth - (x + blockWidth);
        this.pixelsToBottomEdge = boundaryHeight - (y + blockHeight);

        int sourceIndex = frame.SourceOrigin + (y * frame.SourceStride) + x;
        int reconstructionIndex = frame.ReconstructionOrigin + (y * frame.ReconstructionStride) + x;
        if (this.disableReconstruction)
        {
            this.PredictLumaFromSource(ref frame, sourceIndex, reconstructionIndex, blockWidth, blockHeight, upAvailable, leftAvailable);
        }
        else
        {
            this.EncodeIntraLuma(ref frame, sourceIndex, reconstructionIndex, blockWidth, blockHeight, upAvailable, leftAvailable);
        }

        // The squares of all 256 residual entries sum in 32-bit unsigned arithmetic. A twelve-bit residual can carry that sum past the signed
        // range before the precision shift.
        int intraError = unchecked((int)(uint)Av1ResidualBuilder.SumSquares(this.residual));
        int precisionShift = this.bitDepth.GetBitCount() - 8;
        intraError >>= 2 * precisionShift;

        if (intraError < LowIntraThreshold)
        {
            record.IntraSkipCount++;
        }
        else if (unitColumn > 0 && record.ImageDataStartRow == InvalidRow)
        {
            record.ImageDataStartRow = unitRow;
        }

        // A unit with a low intra error adds more than one to the intra factor.
        double logIntra = Av1FirstPassMath.Log1P(intraError);
        if (logIntra < 10.0)
        {
            record.IntraFactor += 1.0 + ((10.0 - logIntra) * 0.05);
        }
        else
        {
            record.IntraFactor += 1.0;
        }

        // A dark unit with a low intra error adds more than one to the brightness factor. The first source sample gives the eight-bit level.
        int level = TOperator.ToInt32(frame.Source[sourceIndex]) >> precisionShift;
        if (level < DarkThreshold && logIntra < 9.0)
        {
            record.BrightnessFactor += 1.0 + (0.01 * (DarkThreshold - level));
        }
        else
        {
            record.BrightnessFactor += 1.0;
        }

        // The surcharge matches the cost of a zero vector, so that a plain frame does not become a key frame.
        intraError += IntraModePenalty;
        record.IntraError += intraError;

        // When the wavelet energy is enabled, the row adds it to every record before the units are predicted. Otherwise the record holds the
        // marker of an unmeasured energy.
        if (!this.calculateWaveletEnergy)
        {
            record.FrameAverageWaveletEnergy = InvalidWaveletEnergy;
        }

        return intraError;
    }

    /// <summary>
    /// Predicts every 4x4 transform block of a unit from the neighboring source samples and writes the residual. Then it copies the source block
    /// into the reconstruction, which later frames use as their reference.
    /// </summary>
    /// <param name="frame">The frame being measured.</param>
    /// <param name="sourceIndex">The source index of the block origin.</param>
    /// <param name="reconstructionIndex">The reconstruction index of the block origin.</param>
    /// <param name="blockWidth">The block width.</param>
    /// <param name="blockHeight">The block height.</param>
    /// <param name="upAvailable">Whether the row above is inside the tile.</param>
    /// <param name="leftAvailable">Whether the column to the left is inside the tile.</param>
    private void PredictLumaFromSource(
        ref FrameContext frame,
        int sourceIndex,
        int reconstructionIndex,
        int blockWidth,
        int blockHeight,
        bool upAvailable,
        bool leftAvailable)
    {
        Span<TSample> left = stackalloc TSample[4];
        Span<TSample> prediction = stackalloc TSample[16];
        int stride = frame.SourceStride;
        for (int row = 0; row < blockHeight; row += 4)
        {
            for (int column = 0; column < blockWidth; column += 4)
            {
                // The prediction reads the source, so it does not depend on earlier blocks of the unit.
                int index = sourceIndex + (row * stride) + column;
                bool hasAbove = row > 0 || upAvailable;
                bool hasLeft = column > 0 || leftAvailable;
                ReadOnlySpan<TSample> above = hasAbove ? frame.Source.Slice(index - stride, 4) : default;
                if (hasLeft)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        left[i] = frame.Source[index + (i * stride) - 1];
                    }
                }

                TOperator.PredictDc(hasLeft, hasAbove, prediction, 4, above, left, 4, 4, this.bitDepth.GetBitCount());
                Span<short> residual = this.residual.AsSpan((row * blockWidth) + column);
                TOperator.Subtract(frame.Source[index..], stride, prediction, 4, residual, blockWidth, 4, 4);
                this.PadBorderResidual(residual, blockWidth, blockWidth, blockHeight, column, row, 4, 4);
            }
        }

        // The source replaces the reconstruction.
        for (int row = 0; row < blockHeight; row++)
        {
            frame.Source.Slice(sourceIndex + (row * stride), blockWidth)
                .CopyTo(frame.Reconstruction[(reconstructionIndex + (row * frame.ReconstructionStride))..]);
        }
    }

    /// <summary>
    /// Codes every 4x4 transform block of a unit with DC prediction from the reconstruction, regular quantization without coefficient
    /// optimization, and reconstruction in place.
    /// </summary>
    /// <param name="frame">The frame being measured.</param>
    /// <param name="sourceIndex">The source index of the block origin.</param>
    /// <param name="reconstructionIndex">The reconstruction index of the block origin.</param>
    /// <param name="blockWidth">The block width.</param>
    /// <param name="blockHeight">The block height.</param>
    /// <param name="upAvailable">Whether the row above is inside the tile.</param>
    /// <param name="leftAvailable">Whether the column to the left is inside the tile.</param>
    private void EncodeIntraLuma(
        ref FrameContext frame,
        int sourceIndex,
        int reconstructionIndex,
        int blockWidth,
        int blockHeight,
        bool upAvailable,
        bool leftAvailable)
    {
        Span<TSample> left = stackalloc TSample[4];
        int sourceStride = frame.SourceStride;
        int stride = frame.ReconstructionStride;
        for (int row = 0; row < blockHeight; row += 4)
        {
            for (int column = 0; column < blockWidth; column += 4)
            {
                // The neighbors are reconstructed samples of this and earlier units, so the blocks run in order.
                int index = reconstructionIndex + (row * stride) + column;
                bool hasAbove = row > 0 || upAvailable;
                bool hasLeft = column > 0 || leftAvailable;
                ReadOnlySpan<TSample> above = hasAbove ? frame.Reconstruction.Slice(index - stride, 4) : default;
                if (hasLeft)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        left[i] = frame.Reconstruction[index + (i * stride) - 1];
                    }
                }

                Span<TSample> reconstruction = frame.Reconstruction[index..];
                TOperator.PredictDc(hasLeft, hasAbove, reconstruction, stride, above, left, 4, 4, this.bitDepth.GetBitCount());
                Span<short> residual = this.residual.AsSpan((row * blockWidth) + column);
                TOperator.Subtract(
                    frame.Source[(sourceIndex + (row * sourceStride) + column)..], sourceStride, reconstruction, stride, residual, blockWidth, 4, 4);

                // The statistics stage always pads with the DCT rule, whatever transform type a coding pass picks for the block.
                this.PadBorderResidual(residual, blockWidth, blockWidth, blockHeight, column, row, 4, 4);
                this.CodeTransformBlock(residual, blockWidth, reconstruction, stride);
            }
        }
    }

    /// <summary>
    /// Codes the residual of a unit with an inter prediction. The residual of the whole block is padded as one region. Then every 4x4 transform
    /// block is quantized and added to the prediction.
    /// </summary>
    /// <param name="frame">The frame being measured.</param>
    /// <param name="sourceIndex">The source index of the block origin.</param>
    /// <param name="reconstructionIndex">The reconstruction index of the block origin, holding the prediction.</param>
    /// <param name="blockWidth">The block width.</param>
    /// <param name="blockHeight">The block height.</param>
    private void EncodeInterLuma(ref FrameContext frame, int sourceIndex, int reconstructionIndex, int blockWidth, int blockHeight)
    {
        int stride = frame.ReconstructionStride;
        Span<short> residual = this.residual.AsSpan(0, blockWidth * blockHeight);
        TOperator.Subtract(
            frame.Source[sourceIndex..], frame.SourceStride, frame.Reconstruction[reconstructionIndex..], stride, residual, blockWidth, blockWidth, blockHeight);

        this.PadBorderResidual(residual, blockWidth, blockWidth, blockHeight, 0, 0, blockWidth, blockHeight);
        for (int row = 0; row < blockHeight; row += 4)
        {
            for (int column = 0; column < blockWidth; column += 4)
            {
                this.CodeTransformBlock(
                    this.residual.AsSpan((row * blockWidth) + column),
                    blockWidth,
                    frame.Reconstruction[(reconstructionIndex + (row * stride) + column)..],
                    stride);
            }
        }
    }

    /// <summary>
    /// Transforms a 4x4 residual with the DCT and quantizes it with the regular quantizer at the first-pass quantizer index. When any
    /// coefficient is nonzero, the inverse transform adds the result to the prediction.
    /// </summary>
    /// <param name="residual">The residual at the transform origin.</param>
    /// <param name="residualStride">The residual row stride.</param>
    /// <param name="reconstruction">The prediction and reconstruction at the transform origin.</param>
    /// <param name="reconstructionStride">The reconstruction row stride.</param>
    private void CodeTransformBlock(ReadOnlySpan<short> residual, int residualStride, Span<TSample> reconstruction, int reconstructionStride)
    {
        Av1ForwardTransformer.Transform2d(
            residual,
            this.transformed,
            (uint)residualStride,
            Av1TransformType.DctDct,
            Av1TransformSize.Size4x4,
            this.bitDepth.GetBitCount(),
            this.transformWorkspace);

        // The statistics compressor builds its quantizer tables once, at creation, and never rebuilds them. The encoder applies the sharpness
        // setting after that, so the first pass always rounds with the tables of sharpness zero.
        int endOfBlock = Av1ForwardQuantizer.QuantizeRegular(
            this.transformed,
            this.quantized,
            this.dequantized,
            Av1TransformSize.Size4x4,
            Av1TransformType.DctDct,
            this.qIndex,
            0,
            0,
            this.bitDepth,
            0);

        if (endOfBlock > 0)
        {
            TOperator.Reconstruct(
                this.dequantized, reconstruction, reconstructionStride, Av1TransformSize.Size4x4, endOfBlock, this.bitDepth, this.transformWorkspace);
        }
    }

    /// <summary>
    /// Replaces the residual of a region outside the visible frame by the mean of its visible part when border padding is enabled.
    /// </summary>
    /// <param name="residual">The residual at the region origin.</param>
    /// <param name="residualStride">The residual row stride.</param>
    /// <param name="blockWidth">The width of the block containing the region.</param>
    /// <param name="blockHeight">The height of the block containing the region.</param>
    /// <param name="column">The region column within the block, in samples.</param>
    /// <param name="row">The region row within the block, in samples.</param>
    /// <param name="columns">The region width.</param>
    /// <param name="rows">The region height.</param>
    private void PadBorderResidual(Span<short> residual, int residualStride, int blockWidth, int blockHeight, int column, int row, int columns, int rows)
    {
        if (!this.doBorderPad || (this.pixelsToBottomEdge >= 0 && this.pixelsToRightEdge >= 0))
        {
            return;
        }

        int visibleRows = this.pixelsToBottomEdge >= 0 ? rows : Math.Clamp(this.pixelsToBottomEdge + blockHeight - row, 0, rows);
        int visibleColumns = this.pixelsToRightEdge >= 0 ? columns : Math.Clamp(this.pixelsToRightEdge + blockWidth - column, 0, columns);
        if (visibleColumns < columns || visibleRows < rows)
        {
            Av1ResidualBuilder.FillResidueOutsideFrame(residual, residualStride, columns, rows, visibleColumns, visibleRows, Av1TransformType.DctDct);
        }
    }

    /// <summary>
    /// Measures the inter errors of a unit. The LAST search starts from the vector of the previous unit and, when that vector is nonzero, also
    /// from zero. The LAST2 and GOLDEN searches start from zero once those references hold older frames. The best LAST error competes with the
    /// intra error. A winning vector is coded and accumulated.
    /// </summary>
    /// <param name="frame">The frame being measured.</param>
    /// <param name="unitRow">The unit row.</param>
    /// <param name="unitColumn">The unit column.</param>
    /// <param name="intraError">The intra error including its surcharge.</param>
    /// <param name="rawMotionError">Receives the zero-motion error against the previous source.</param>
    /// <param name="referenceVector">The vector that starts the LAST search.</param>
    /// <param name="bestVector">Receives the winning vector, or zero when intra wins.</param>
    /// <param name="lastNonZeroVector">The last nonzero vector of the row.</param>
    /// <param name="record">The unit record.</param>
    /// <returns>The coded error of the unit.</returns>
    private int PredictInter(
        ref FrameContext frame,
        int unitRow,
        int unitColumn,
        int intraError,
        ref int rawMotionError,
        Av1MotionVector referenceVector,
        out Av1MotionVector bestVector,
        ref Av1MotionVector lastNonZeroVector,
        ref FrameStatistics record)
    {
        int interError = intraError;
        Av1BlockSize blockSize = this.GetBlockSize(frame.FirstPassBlockSize, frame.UnitLog2, unitRow, unitColumn);
        int unitSize = 4 << frame.UnitLog2;
        int unitRows = GetUnitRows(frame.UnitLog2, this.macroblockRows);
        int unitColumns = GetUnitColumns(frame.UnitLog2, this.macroblockColumns);
        int x = unitColumn * unitSize;
        int y = unitRow * unitSize;
        int sourceIndex = frame.SourceOrigin + (y * frame.SourceStride) + x;
        int reconstructionIndex = frame.ReconstructionOrigin + (y * frame.ReconstructionStride) + x;
        Point vector = Point.Empty;

        // The column limits use the unit width even for a halved block.
        this.SetMotionVectorColumnLimits(unitColumn << frame.UnitLog2, unitSize >> 2);

        int motionError = this.GetPredictionError(blockSize, ref frame, sourceIndex, frame.Last, reconstructionIndex, frame.ReconstructionStride);

        // A small zero-motion error against the previous source skips the search of the reconstruction.
        rawMotionError = this.GetPredictionError(
            blockSize,
            ref frame,
            sourceIndex,
            frame.PreviousSource,
            frame.PreviousSourceOrigin + (y * frame.PreviousSourceStride) + x,
            frame.PreviousSourceStride);

        if (rawMotionError > this.skipMotionSearchThreshold)
        {
            this.SearchFirstPassMotion(ref frame, frame.Last, blockSize, unitRow, unitColumn, referenceVector, ref vector, ref motionError);

            // A nonzero starting vector is followed by a search centered on zero.
            if (!this.skipZeroMotionSearch && !referenceVector.IsZero)
            {
                Point zeroVector = Point.Empty;
                int zeroError = int.MaxValue;
                this.SearchFirstPassMotion(ref frame, frame.Last, blockSize, unitRow, unitColumn, default, ref zeroVector, ref zeroError);
                if (zeroError < motionError)
                {
                    motionError = zeroError;
                    vector = zeroVector;
                }
            }
        }

        if (this.frameNumber > 2)
        {
            Point last2Vector = Point.Empty;
            int last2Error = this.GetPredictionError(blockSize, ref frame, sourceIndex, frame.Last2, reconstructionIndex, frame.ReconstructionStride);
            this.SearchFirstPassMotion(ref frame, frame.Last2, blockSize, unitRow, unitColumn, default, ref last2Vector, ref last2Error);
            record.LongTermCodedError += Math.Min(last2Error, intraError);
        }

        int goldenError = motionError;
        if (this.frameNumber > 1)
        {
            Point goldenVector = Point.Empty;
            goldenError = this.GetPredictionError(blockSize, ref frame, sourceIndex, frame.Golden, reconstructionIndex, frame.ReconstructionStride);
            this.SearchFirstPassMotion(ref frame, frame.Golden, blockSize, unitRow, unitColumn, default, ref goldenVector, ref goldenError);
        }

        if (goldenError < motionError && goldenError < intraError)
        {
            record.SecondReferenceCount++;
        }

        // Before GOLDEN holds an older frame, the second-reference error takes the LAST error and not its minimum with the intra error. The
        // stage keeps this asymmetry so that its statistics stay the same as those of other AV1 encoders.
        record.SecondReferenceCodedError += this.frameNumber > 1 ? Math.Min(goldenError, intraError) : motionError;

        bestVector = default;
        if (motionError <= intraError)
        {
            // Count units where intra and inter are close and both low, which marks black bars and cropped content. Also count units where intra
            // is not much worse, which limits the golden-frame interval.
            if (((intraError - IntraModePenalty) * 9 <= motionError * 10) && (intraError < 2 * IntraModePenalty))
            {
                record.NeutralCount += 1.0;
            }
            else if ((intraError > NeutralCountIntraThreshold) && (intraError < NeutralCountIntraFactor * motionError))
            {
                // The small constant keeps the ratio finite for a zero intra error.
                record.NeutralCount += motionError / ((double)intraError + 0.000001);
            }

            bestVector = new Av1MotionVector(vector.Y * 8, vector.X * 8);
            interError = motionError;

            if (!this.disableReconstruction)
            {
                // A full-sample vector predicts by a copy of the displaced block. The predictor addresses the base of the plane that the block
                // used last. The return to LAST after the other searches moves only the block position, not that base. From the third frame on,
                // the GOLDEN search runs last, so the LAST vector predicts from GOLDEN. The stage keeps this behavior so that its statistics stay
                // the same as those of other AV1 encoders.
                ReadOnlySpan<TSample> predictionPlane = this.frameNumber > 1 ? frame.Golden : frame.Last;
                int blockWidth = blockSize.GetWidth();
                int blockHeight = blockSize.GetHeight();
                int referenceIndex = reconstructionIndex + (vector.Y * frame.ReconstructionStride) + vector.X;
                for (int row = 0; row < blockHeight; row++)
                {
                    predictionPlane.Slice(referenceIndex + (row * frame.ReconstructionStride), blockWidth)
                        .CopyTo(frame.Reconstruction[(reconstructionIndex + (row * frame.ReconstructionStride))..]);
                }

                this.EncodeInterLuma(ref frame, sourceIndex, reconstructionIndex, blockWidth, blockHeight);
            }

            record.SumMotionVectorRow += bestVector.Row;
            record.SumMotionVectorRowAbsolute += Math.Abs(bestVector.Row);
            record.SumMotionVectorColumn += bestVector.Column;
            record.SumMotionVectorColumnAbsolute += Math.Abs(bestVector.Column);
            record.SumMotionVectorRowSquares += bestVector.Row * bestVector.Row;
            record.SumMotionVectorColumnSquares += bestVector.Column * bestVector.Column;
            record.InterCount++;

            AccumulateMotionVectorStatistics(bestVector, vector, unitRow, unitColumn, unitRows, unitColumns, ref lastNonZeroVector, ref record);
        }

        return interError;
    }

    /// <summary>
    /// Counts a nonzero vector, whether it differs from the previous nonzero vector, and whether each component points toward or away from the
    /// frame center.
    /// </summary>
    /// <param name="bestVector">The winning vector in eighth samples.</param>
    /// <param name="vector">The winning vector in full samples.</param>
    /// <param name="unitRow">The unit row.</param>
    /// <param name="unitColumn">The unit column.</param>
    /// <param name="unitRows">The number of unit rows.</param>
    /// <param name="unitColumns">The number of unit columns.</param>
    /// <param name="lastNonZeroVector">The last nonzero vector of the row.</param>
    /// <param name="record">The unit record.</param>
    private static void AccumulateMotionVectorStatistics(
        Av1MotionVector bestVector,
        Point vector,
        int unitRow,
        int unitColumn,
        int unitRows,
        int unitColumns,
        ref Av1MotionVector lastNonZeroVector,
        ref FrameStatistics record)
    {
        if (bestVector.IsZero)
        {
            return;
        }

        record.MotionVectorCount++;
        if (bestVector != lastNonZeroVector)
        {
            record.NewMotionVectorCount++;
        }

        lastNonZeroVector = bestVector;

        // A component that points away from the frame center adds one to the inward count. A component that points toward the frame center
        // subtracts one. The middle unit row and unit column count neither way.
        if (unitRow < unitRows / 2)
        {
            record.SumInVectors += vector.Y > 0 ? -1 : vector.Y < 0 ? 1 : 0;
        }
        else if (unitRow > unitRows / 2)
        {
            record.SumInVectors += vector.Y > 0 ? 1 : vector.Y < 0 ? -1 : 0;
        }

        if (unitColumn < unitColumns / 2)
        {
            record.SumInVectors += vector.X > 0 ? -1 : vector.X < 0 ? 1 : 0;
        }
        else if (unitColumn > unitColumns / 2)
        {
            record.SumInVectors += vector.X > 0 ? 1 : vector.X < 0 ? -1 : 0;
        }
    }

    /// <summary>
    /// Measures the squared error of a source block against a reference block at zero motion. Only the 8x8, 16x8 and 8x16 blocks have their own
    /// measure. Every other block, including those smaller than 8x8, is measured over 16x16. High bit depths round the error to eight-bit
    /// precision.
    /// </summary>
    /// <param name="blockSize">The measured block size.</param>
    /// <param name="frame">The frame being measured.</param>
    /// <param name="sourceIndex">The source index of the block origin.</param>
    /// <param name="reference">The reference plane.</param>
    /// <param name="referenceIndex">The reference index of the block origin.</param>
    /// <param name="referenceStride">The reference row stride.</param>
    /// <returns>The squared error.</returns>
    private int GetPredictionError(
        Av1BlockSize blockSize,
        ref FrameContext frame,
        int sourceIndex,
        ReadOnlySpan<TSample> reference,
        int referenceIndex,
        int referenceStride)
    {
        (int width, int height) = blockSize switch
        {
            Av1BlockSize.Block8x8 => (8, 8),
            Av1BlockSize.Block16x8 => (16, 8),
            Av1BlockSize.Block8x16 => (8, 16),
            _ => (16, 16)
        };

        TOperator.GetMoments(
            frame.Source[sourceIndex..], frame.SourceStride, reference[referenceIndex..], referenceStride, width, height, out _, out long squares);

        // The high-bit-depth measures round to eight-bit precision.
        int shift = 2 * (this.bitDepth.GetBitCount() - 8);
        if (shift != 0)
        {
            squares = (squares + (1L << (shift - 1))) >> shift;
        }

        return unchecked((int)(uint)squares);
    }

    /// <summary>
    /// Sets the row limits of every search in a unit row.
    /// </summary>
    /// <param name="miRow">The unit row in 4x4 units.</param>
    /// <param name="miHeight">The unit height in 4x4 units.</param>
    private void SetMotionVectorRowLimits(int miRow, int miHeight)
    {
        // Each bound is the tighter of two limits. The first keeps the displaced block two interpolation margins inside the frame border. The
        // second lets the whole block leave the coded frame by at most two interpolation margins.
        int minimum1 = -((miRow * 4) + this.border - (2 * InterpolationExtend));
        int minimum2 = -(((miRow + miHeight) * 4) + (2 * InterpolationExtend));
        this.motionLimits.RowMinimum = Math.Max(minimum1, minimum2);
        int maximum1 = ((this.miRows - miRow - miHeight) * 4) + this.border - (2 * InterpolationExtend);
        int maximum2 = ((this.miRows - miRow) * 4) + (2 * InterpolationExtend);
        this.motionLimits.RowMaximum = Math.Min(maximum1, maximum2);
    }

    /// <summary>
    /// Sets the column limits of every search in a unit. The bounds follow the same rule as <see cref="SetMotionVectorRowLimits"/>.
    /// </summary>
    /// <param name="miColumn">The unit column in 4x4 units.</param>
    /// <param name="miWidth">The unit width in 4x4 units.</param>
    private void SetMotionVectorColumnLimits(int miColumn, int miWidth)
    {
        int minimum1 = -((miColumn * 4) + this.border - (2 * InterpolationExtend));
        int minimum2 = -(((miColumn + miWidth) * 4) + (2 * InterpolationExtend));
        this.motionLimits.ColumnMinimum = Math.Max(minimum1, minimum2);
        int maximum1 = ((this.miColumns - miColumn - miWidth) * 4) + this.border - (2 * InterpolationExtend);
        int maximum2 = ((this.miColumns - miColumn) * 4) + (2 * InterpolationExtend);
        this.motionLimits.ColumnMaximum = Math.Min(maximum1, maximum2);
    }
}
