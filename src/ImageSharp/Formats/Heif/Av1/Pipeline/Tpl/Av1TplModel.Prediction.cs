// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <content>
/// Builds intra and inter predictions of a model block and measures the rate and distortion of coding its residual.
/// </content>
internal sealed partial class Av1TplModel<TSample, TSearchOperator, TSampleOperator>
{
    /// <summary>
    /// The luma border that the inter prediction clamp assumes on the top and left. Reference: AOM_BORDER_IN_PIXELS.
    /// </summary>
    private const int ReferenceBorder = 288;

    /// <summary>
    /// The number of fractional bits of a scaled prediction position. Reference: SCALE_SUBPEL_BITS.
    /// </summary>
    private const int ScaleSubpelBits = 10;

    /// <summary>
    /// Codes the winner of a block in each plane, luma only when so configured: predicts it (intra in place from the
    /// reconstruction, or inter from the given references), then transforms, quantizes and measures the residual, and
    /// reconstructs when asked. Reference: get_rate_distortion().
    /// </summary>
    private void GetRateDistortion(
        Av1TplSetupInput<TSample> input,
        Av1PredictionMode mode,
        ReferencePicture first,
        ReferencePicture second,
        ReadOnlySpan<Av1MotionVector> vectors,
        int modeInfoRow,
        int modeInfoColumn,
        bool reconstruct,
        out int rate,
        out long reconstructionError,
        out long predictionError)
    {
        rate = 0;
        reconstructionError = 1;
        predictionError = 1;
        bool compound = mode == Av1PredictionMode.NewNewMotionVector;
        bool isInter = mode >= Av1PredictionMode.NearestMotionVector;
        int planes = input.SpeedFeatures.LumaOnlyRateDistortion ? 1 : this.planeCount;
        int entry = GetEntry(this.frameIndex);
        int lumaX = modeInfoColumn * 4;
        int lumaY = modeInfoRow * 4;
        for (int plane = 0; plane < planes; plane++)
        {
            int subX = plane == 0 ? 0 : this.subsamplingX;
            int subY = plane == 0 ? 0 : this.subsamplingY;
            Av1BlockSize planeBlockSize = Av1BlockSize.Block16x16.GetSubsampled(subX, subY);
            int width = planeBlockSize.GetWidth();
            int height = planeBlockSize.GetHeight();
            Av1TransformSize transformSize = planeBlockSize.GetMaximumTransformSize();
            PlaneAccess destination = GetPlane(this.reconstructionPictures[entry], plane);
            int planeX = lumaX >> subX;
            int planeY = lumaY >> subY;
            int destinationIndex = destination.IndexOf(planeX, planeY);
            Span<TSample> destinationSamples = destination.Samples[destinationIndex..];
            if (!isInter)
            {
                this.PredictIntra(input, plane, mode, destination, planeX, planeY, destinationSamples, destination.Stride, transformSize, modeInfoRow, modeInfoColumn);
            }
            else if (compound)
            {
                this.PredictCompound(first.Frame, second.Frame, vectors, plane, lumaX, lumaY, destinationSamples, destination.Stride);
            }
            else
            {
                this.PredictSingle(first.Frame, plane, vectors[0], lumaX, lumaY, destinationSamples, destination.Stride, width, height);
            }

            PlaneAccess source = GetPlane(this.sourcePictures[entry], plane);
            this.TransformQuantizeRateCost(
                source.Samples[source.IndexOf(planeX, planeY)..],
                source.Stride,
                destinationSamples,
                destination.Stride,
                width,
                height,
                transformSize,
                reconstruct,
                out int planeRate,
                out long planeError,
                out long planeSse);

            reconstructionError += planeError;
            predictionError += planeSse;
            rate += planeRate;
        }
    }

    /// <summary>
    /// Subtracts the prediction, transforms the residual with the DCT, quantizes it with the luma quantizer of the model,
    /// and returns the estimated rate, the quantization error and the residual energy, each at least one.
    /// Reference: txfm_quant_rdcost() with get_quantize_error() and rate_estimator().
    /// </summary>
    private void TransformQuantizeRateCost(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        Span<TSample> destination,
        int destinationStride,
        int width,
        int height,
        Av1TransformSize transformSize,
        bool reconstruct,
        out int rate,
        out long reconstructionError,
        out long sse)
    {
        int count = width * height;
        TSampleOperator.Subtract(source, sourceStride, destination, destinationStride, this.residual, width, height);
        Av1ForwardTransformer.Transform2d(
            this.residual,
            this.coefficients,
            (uint)width,
            Av1TransformType.DctDct,
            transformSize,
            this.bitDepth.GetBitCount(),
            this.transformWorkspace);

        // Every plane uses the luma quantizer, without a quantization matrix. Reference: the get_quantize_error() call
        // with plane 0 and av1_setup_quant().
        Span<int> coefficientSpan = this.coefficients.AsSpan(0, count);
        Span<int> quantizedSpan = this.quantized.AsSpan(0, count);
        Span<int> dequantizedSpan = this.dequantized.AsSpan(0, count);
        int endOfBlock = Av1ForwardQuantizer.QuantizeLossy(
            coefficientSpan,
            quantizedSpan,
            dequantizedSpan,
            transformSize,
            Av1TransformType.DctDct,
            this.qIndex,
            0,
            0,
            this.bitDepth);

        // The error is scaled down by four below 32x32 transforms, and normalized to 8-bit precision first.
        reconstructionError = Av1TransformBlockEncoder.GetTransformErrorCore(coefficientSpan, dequantizedSpan, transformSize, this.bitDepth, out sse);
        reconstructionError = Math.Max(reconstructionError, 1);
        sse = Math.Max(sse, 1);
        rate = EstimateRate(quantizedSpan, endOfBlock, transformSize);
        if (reconstruct && endOfBlock > 0)
        {
            TSampleOperator.Reconstruct(dequantizedSpan, destination, destinationStride, transformSize, endOfBlock, this.bitDepth, this.transformWorkspace);
        }
    }

    /// <summary>
    /// Estimates the coefficient rate as one bit plus, per coefficient in scan order up to the end of block, the bit
    /// length of its level plus one, one more bit, and a sign bit for a nonzero level. Reference: rate_estimator().
    /// </summary>
    /// <remarks>
    /// Every coefficient past the end of block is zero and adds only its one bit, and a sum does not depend on its
    /// order. So the rate is one bit per scan position up to the end of block plus the level bits of every coefficient
    /// that the quantizer wrote, in raster order.
    /// </remarks>
    private static int EstimateRate(ReadOnlySpan<int> quantized, int endOfBlock, Av1TransformSize transformSize)
    {
        int count = transformSize.GetAdjusted().GetSize2d();
        int rate = 1 + endOfBlock + Av1CoefficientMeasures.SumLevelBits(quantized[..count]);
        return rate << Av1TplModelConstants.ProbabilityCostShift;
    }

    /// <summary>
    /// Predicts one reference with the regular filter, at the position that the encoder prediction clamps to the frame
    /// extension. Reference: av1_enc_build_one_inter_predictor() with av1_init_inter_params(), init_subpel_params() and
    /// get_conv_params().
    /// </summary>
    /// <param name="reference">The reference picture.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="vector">The vector in eighth luma samples.</param>
    /// <param name="lumaX">The block column in luma samples.</param>
    /// <param name="lumaY">The block row in luma samples.</param>
    /// <param name="destination">The prediction destination.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="visibleSize">
    /// Whether the clamp uses the visible size of the reference, as the pre-plane buffers of the joint motion search
    /// do, instead of the 8-aligned size of the frame buffer. Reference: the y_crop_width of av1_setup_pred_block().
    /// </param>
    private void PredictSingle(
        Av1EncoderFrame<TSample> reference,
        int plane,
        Av1MotionVector vector,
        int lumaX,
        int lumaY,
        Span<TSample> destination,
        int destinationStride,
        int width,
        int height,
        bool visibleSize = false)
    {
        int subX = plane == 0 ? 0 : this.subsamplingX;
        int subY = plane == 0 ? 0 : this.subsamplingY;
        PlaneAccess referencePlane = GetPlane(reference, plane);
        int planeWidth = visibleSize ? (this.width + subX) >> subX : (this.ModeInfoColumns * 4) >> subX;
        int planeHeight = visibleSize ? (this.height + subY) >> subY : (this.ModeInfoRows * 4) >> subY;
        GetClampedPosition(lumaX >> subX, vector.Column, subX, planeWidth, out int column, out int horizontalPhase);
        GetClampedPosition(lumaY >> subY, vector.Row, subY, planeHeight, out int row, out int verticalPhase);
        TSampleOperator.PredictTranslational(
            referencePlane.Samples,
            referencePlane.Stride,
            referencePlane.IndexOf(column, row),
            destination,
            destinationStride,
            width,
            height,
            horizontalPhase,
            verticalPhase,
            this.bitDepth.GetBitCount(),
            this.convolutionScratch);
    }

    /// <summary>
    /// Predicts a compound pair by averaging the unrounded intermediates of both references. Reference: the compound
    /// branches of mode_estimation() and get_rate_distortion(), with av1_init_comp_mode() and
    /// get_conv_params_no_round().
    /// </summary>
    private void PredictCompound(
        Av1EncoderFrame<TSample> first,
        Av1EncoderFrame<TSample> second,
        ReadOnlySpan<Av1MotionVector> vectors,
        int plane,
        int lumaX,
        int lumaY,
        Span<TSample> destination,
        int destinationStride)
    {
        int subX = plane == 0 ? 0 : this.subsamplingX;
        int subY = plane == 0 ? 0 : this.subsamplingY;
        int width = Av1TplModelConstants.BlockSize >> subX;
        int height = Av1TplModelConstants.BlockSize >> subY;
        for (int reference = 0; reference < 2; reference++)
        {
            PlaneAccess referencePlane = GetPlane(reference == 0 ? first : second, plane);
            GetClampedPosition(lumaX >> subX, vectors[reference].Column, subX, (this.ModeInfoColumns * 4) >> subX, out int column, out int horizontalPhase);
            GetClampedPosition(lumaY >> subY, vectors[reference].Row, subY, (this.ModeInfoRows * 4) >> subY, out int row, out int verticalPhase);
            TSampleOperator.PredictCompoundIntermediate(
                referencePlane.Samples,
                referencePlane.Stride,
                referencePlane.IndexOf(column, row),
                reference == 0 ? this.firstIntermediate : this.secondIntermediate,
                width,
                width,
                height,
                horizontalPhase,
                verticalPhase,
                this.bitDepth.GetBitCount(),
                this.convolutionScratch);
        }

        TSampleOperator.AverageCompound(
            destination,
            destinationStride,
            this.firstIntermediate,
            this.secondIntermediate,
            width,
            height,
            this.bitDepth.GetBitCount());
    }

    /// <summary>
    /// Converts a plane position and a vector component to the integer position and sixteenth-sample phase of the
    /// prediction, clamped to the extension the encoder assumes around the reference. Reference: init_subpel_params()
    /// without scaling, with the top and left limits of AOM_LEFT_TOP_MARGIN_SCALED() and the bottom and right limits of
    /// the reference size plus AOM_INTERP_EXTEND.
    /// </summary>
    /// <param name="position">The block position in plane samples.</param>
    /// <param name="component">The vector component in eighth luma samples.</param>
    /// <param name="subsampling">The plane subsampling in this direction.</param>
    /// <param name="planeSize">The 8-aligned plane size in this direction. Reference: y_width or uv_width.</param>
    /// <param name="integer">Receives the integer sample position.</param>
    /// <param name="phase">Receives the phase in sixteenth samples.</param>
    private static void GetClampedPosition(int position, int component, int subsampling, int planeSize, out int integer, out int phase)
    {
        const int ExtraBits = ScaleSubpelBits - 4;
        int scaled = ((position << 4) + (component * (1 << (1 - subsampling)))) * (1 << ExtraBits);
        scaled += 1 << (ExtraBits - 1);
        int low = -(((ReferenceBorder >> subsampling) - Av1TplModelConstants.InterpolationExtension) << ScaleSubpelBits);
        int high = (planeSize + Av1TplModelConstants.InterpolationExtension) << ScaleSubpelBits;
        scaled = Math.Clamp(scaled, low, high);
        integer = scaled >> ScaleSubpelBits;
        phase = (scaled & ((1 << ScaleSubpelBits) - 1)) >> ExtraBits;
    }

    /// <summary>
    /// Predicts an intra block from the model reconstruction, with the edge availability, sample counts and edge filter
    /// of the reference encoder. The block reads the stale partition of its mode-information record, and the smooth
    /// state of its neighbors' records. Reference: av1_predict_intra_block() for a 16x16 block with its transform at
    /// the origin, with build_non_directional_intra_predictors() and build_directional_and_filter_intra_predictors().
    /// </summary>
    private void PredictIntra(
        Av1TplSetupInput<TSample> input,
        int plane,
        Av1PredictionMode mode,
        PlaneAccess reconstruction,
        int planeX,
        int planeY,
        Span<TSample> destination,
        int destinationStride,
        Av1TransformSize transformSize,
        int modeInfoRow,
        int modeInfoColumn)
    {
        int subX = plane == 0 ? 0 : this.subsamplingX;
        int subY = plane == 0 ? 0 : this.subsamplingY;
        int transformWidth = transformSize.GetWidth();
        int transformHeight = transformSize.GetHeight();
        int bitCount = this.bitDepth.GetBitCount();
        int baseValue = 128 << (bitCount - 8);
        Span<TSample> samples = reconstruction.Samples;
        int stride = reconstruction.Stride;
        int blockIndex = reconstruction.IndexOf(planeX, planeY);
        int aboveIndex = blockIndex - stride;
        int leftIndex = blockIndex - 1;

        // The distances to the frame edges come from set_mi_row_col() for the whole 16x16 luma block; the prediction
        // covers the whole plane block, so the block and transform extents cancel.
        const int ModeInfoSize = Av1TplModelConstants.BlockSize >> 2;
        int toRightEdge = (this.ModeInfoColumns - ModeInfoSize - modeInfoColumn) * 4 * 8;
        int toBottomEdge = (this.ModeInfoRows - ModeInfoSize - modeInfoRow) * 4 * 8;
        int rightDistance = toRightEdge >> (3 + subX);
        int bottomDistance = toBottomEdge >> (3 + subY);
        bool haveTop = this.upAvailable;
        bool haveLeft = this.leftAvailable;
        int topCount = haveTop ? Math.Clamp(rightDistance + transformWidth, 0, transformWidth) : 0;
        int leftCount = haveLeft ? Math.Clamp(bottomDistance + transformHeight, 0, transformHeight) : 0;

        Span<TSample> aboveData = this.aboveEdge;
        Span<TSample> leftData = this.leftEdge;
        Span<TSample> aboveRow = aboveData[EdgePrefix..];
        Span<TSample> leftColumn = leftData[EdgePrefix..];

        if (!mode.IsDirectional())
        {
            // DC, smooth and Paeth need both edges; Paeth also needs the corner.
            leftData.Fill(TSampleOperator.CreateSample(baseValue + 1));
            if (leftCount > 0)
            {
                int i = 0;
                for (; i < leftCount; i++)
                {
                    leftColumn[i] = samples[leftIndex + (i * stride)];
                }

                leftColumn[i..transformHeight].Fill(leftColumn[i - 1]);
            }
            else if (topCount > 0)
            {
                leftColumn[..transformHeight].Fill(samples[aboveIndex]);
            }

            aboveData.Fill(TSampleOperator.CreateSample(baseValue - 1));
            if (topCount > 0)
            {
                samples.Slice(aboveIndex, topCount).CopyTo(aboveRow);
                aboveRow[topCount..transformWidth].Fill(aboveRow[topCount - 1]);
            }
            else if (leftCount > 0)
            {
                aboveRow[..transformWidth].Fill(samples[leftIndex]);
            }

            if (mode == Av1PredictionMode.Paeth)
            {
                this.SetCorner(samples, aboveIndex, leftIndex, topCount, leftCount, baseValue);
            }

            if (mode == Av1PredictionMode.DC)
            {
                TSampleOperator.PredictDc(leftCount > 0, topCount > 0, destination, destinationStride, aboveRow, leftColumn, transformWidth, transformHeight, bitCount);
            }
            else
            {
                TSampleOperator.PredictNonDirectional(mode, destination, destinationStride, aboveRow, leftColumn, transformWidth, transformHeight);
            }

            return;
        }

        int angle = mode.ToAngle();
        bool needAbove = angle < 180;
        bool needLeft = angle > 90;

        // Every directional mode needs the corner.
        bool needTopRight = angle < 90;
        bool needBottomLeft = angle > 180;
        int transformWidthUnits = transformSize.Get4x4WideCount();
        int transformHeightUnits = transformSize.Get4x4HighCount();
        bool rightAvailable = modeInfoColumn + (transformWidthUnits << subX) < this.ModeInfoColumns;
        bool bottomAvailable = bottomDistance > 0 && modeInfoRow + (transformHeightUnits << subY) < this.ModeInfoRows;
        int gridColumn = modeInfoColumn >> Av1TplModelConstants.BlockModeInfoLog2;
        int gridRow = modeInfoRow >> Av1TplModelConstants.BlockModeInfoLog2;
        Av1PartitionType partition = this.modeInfo.GetPartition(gridColumn, gridRow);
        int haveTopRight = needTopRight
            ? (Av1IntraReferenceAvailability.HasTopRight(input.SuperblockSize, Av1BlockSize.Block16x16, modeInfoRow, modeInfoColumn, haveTop, rightAvailable, partition, transformSize, 0, 0, subX, subY) ? 1 : 0)
            : -1;

        int haveBottomLeft = needBottomLeft
            ? (Av1IntraReferenceAvailability.HasBottomLeft(input.SuperblockSize, Av1BlockSize.Block16x16, modeInfoRow, modeInfoColumn, bottomAvailable, haveLeft, partition, transformSize, 0, 0, subX, subY) ? 1 : 0)
            : -1;

        // A smooth neighbor above or to the left selects the stronger edge filter. The chroma neighbors of a 16x16
        // block are the records of the same neighboring blocks.
        bool filterType = (haveTop && this.modeInfo.IsSmooth(gridColumn, gridRow - 1, plane)) ||
            (haveLeft && this.modeInfo.IsSmooth(gridColumn - 1, gridRow, plane));

        int topRightCount = haveTopRight > 0 ? Math.Clamp(rightDistance, 0, transformWidth) : haveTopRight;
        int bottomLeftCount = haveBottomLeft > 0 ? Math.Clamp(bottomDistance, 0, transformHeight) : haveBottomLeft;

        // A sole needed edge that is missing gives a constant block.
        if ((!needAbove && leftCount == 0) || (!needLeft && topCount == 0))
        {
            TSample value = needLeft
                ? (topCount > 0 ? samples[aboveIndex] : TSampleOperator.CreateSample(baseValue + 1))
                : (leftCount > 0 ? samples[leftIndex] : TSampleOperator.CreateSample(baseValue - 1));

            for (int row = 0; row < transformHeight; row++)
            {
                destination.Slice(row * destinationStride, transformWidth).Fill(value);
            }

            return;
        }

        leftData.Fill(TSampleOperator.CreateSample(baseValue + 1));
        aboveData.Fill(TSampleOperator.CreateSample(baseValue - 1));
        if (needLeft)
        {
            int needed = transformHeight + (bottomLeftCount >= 0 ? transformWidth : 0);
            if (leftCount > 0)
            {
                int i = 0;
                for (; i < leftCount; i++)
                {
                    leftColumn[i] = samples[leftIndex + (i * stride)];
                }

                if (bottomLeftCount > 0)
                {
                    for (; i < transformHeight + bottomLeftCount; i++)
                    {
                        leftColumn[i] = samples[leftIndex + (i * stride)];
                    }
                }

                if (i < needed)
                {
                    leftColumn[i..needed].Fill(leftColumn[i - 1]);
                }
            }
            else if (topCount > 0)
            {
                leftColumn[..needed].Fill(samples[aboveIndex]);
            }
        }

        if (needAbove)
        {
            int needed = transformWidth + (topRightCount >= 0 ? transformHeight : 0);
            if (topCount > 0)
            {
                samples.Slice(aboveIndex, topCount).CopyTo(aboveRow);
                int i = topCount;
                if (topRightCount > 0)
                {
                    samples.Slice(aboveIndex + transformWidth, topRightCount).CopyTo(aboveRow[transformWidth..]);
                    i += topRightCount;
                }

                if (i < needed)
                {
                    aboveRow[i..needed].Fill(aboveRow[i - 1]);
                }
            }
            else if (leftCount > 0)
            {
                aboveRow[..needed].Fill(samples[leftIndex]);
            }
        }

        this.SetCorner(samples, aboveIndex, leftIndex, topCount, leftCount, baseValue);

        bool upsampleAbove = false;
        bool upsampleLeft = false;
        if (input.EnableIntraEdgeFilter)
        {
            TSampleOperator.PrepareDirectionalEdges(
                aboveRow,
                leftColumn,
                transformWidth,
                transformHeight,
                angle,
                topCount,
                leftCount,
                filterType,
                bitCount,
                this.edgeScratch,
                out upsampleAbove,
                out upsampleLeft);
        }

        TSampleOperator.PredictDirectional(
            destination,
            destinationStride,
            transformSize,
            aboveRow,
            leftColumn,
            upsampleAbove,
            upsampleLeft,
            angle,
            this.directionalScratch);
    }

    /// <summary>
    /// Writes the shared corner before both edges: the reconstructed corner, else the first sample of the available
    /// edge, else the midpoint. Reference: the need_above_left step of the intra edge builders.
    /// </summary>
    private void SetCorner(Span<TSample> samples, int aboveIndex, int leftIndex, int topCount, int leftCount, int baseValue)
    {
        TSample corner = topCount > 0 && leftCount > 0
            ? samples[aboveIndex - 1]
            : topCount > 0
                ? samples[aboveIndex]
                : leftCount > 0 ? samples[leftIndex] : TSampleOperator.CreateSample(baseValue);

        this.aboveEdge[EdgePrefix - 1] = corner;
        this.leftEdge[EdgePrefix - 1] = corner;
    }
}
