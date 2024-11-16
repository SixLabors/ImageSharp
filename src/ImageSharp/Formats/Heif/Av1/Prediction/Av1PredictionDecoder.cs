// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1PredictionDecoder
{
    private const int MaxUpsampleSize = 16;

    private readonly ObuSequenceHeader sequenceHeader;
    private readonly ObuFrameHeader frameHeader;
    private readonly bool is16BitPipeline;

    public Av1PredictionDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, bool is16BitPipeline)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.is16BitPipeline = is16BitPipeline;
    }

    public void Decode(
        Av1PartitionInfo partitionInfo,
        Av1Plane plane,
        Av1TransformSize transformSize,
        Av1TileInfo tileInfo,
        Span<byte> pixelBuffer,
        int pixelStride,
        Av1BitDepth bitDepth,
        int blockModeInfoColumnOffset,
        int blockModeInfoRowOffset)
    {
        int bytesPerPixel = (bitDepth == Av1BitDepth.EightBit && !this.is16BitPipeline) ? 2 : 1;
        int stride = pixelStride * bytesPerPixel;
        Span<byte> topNeighbor = pixelBuffer.Slice(-stride);
        Span<byte> leftNeighbor = pixelBuffer.Slice(-1);

        bool is16BitPipeline = this.is16BitPipeline;
        Av1PredictionMode mode = (plane == Av1Plane.Y) ? partitionInfo.ModeInfo.YMode : partitionInfo.ModeInfo.UvMode;

        if (plane != Av1Plane.Y && partitionInfo.ModeInfo.UvMode == Av1PredictionMode.UvChromaFromLuma)
        {
            this.PredictIntraBlock(
                partitionInfo,
                plane,
                transformSize,
                tileInfo,
                pixelBuffer,
                stride,
                topNeighbor,
                leftNeighbor,
                stride,
                mode,
                blockModeInfoColumnOffset,
                blockModeInfoRowOffset,
                bitDepth);

            this.PredictChromaFromLumaBlock(
                partitionInfo,
                partitionInfo.ChromaFromLumaContext,
                ref pixelBuffer,
                stride,
                transformSize,
                plane);

            return;
        }

        this.PredictIntraBlock(
            partitionInfo,
            plane,
            transformSize,
            tileInfo,
            pixelBuffer,
            stride,
            topNeighbor,
            leftNeighbor,
            stride,
            mode,
            blockModeInfoColumnOffset,
            blockModeInfoRowOffset,
            bitDepth);
    }

    private void PredictChromaFromLumaBlock(Av1PartitionInfo partitionInfo, Av1ChromaFromLumaContext? chromaFromLumaContext, ref Span<byte> pixelBuffer, int stride, Av1TransformSize transformSize, Av1Plane plane)
    {
        Av1BlockModeInfo modeInfo = partitionInfo.ModeInfo;
        bool isChromaFromLumaAllowedFlag = IsChromaFromLumaAllowedWithFrameHeader(partitionInfo, this.sequenceHeader.ColorConfig, this.frameHeader);
        DebugGuard.IsTrue(isChromaFromLumaAllowedFlag, "Chroma from Luma should be allowed then computing it.");

        if (chromaFromLumaContext == null)
        {
            throw new InvalidOperationException("CFL context should have been defined already.");
        }

        if (!chromaFromLumaContext.AreParametersComputed)
        {
            chromaFromLumaContext.ComputeParameters(transformSize);
        }

        int alphaQ3 = ChromaFromLumaIndexToAlpha(modeInfo.ChromaFromLumaAlphaIndex, modeInfo.ChromaFromLumaAlphaSign, (Av1Plane)((int)plane - 1));

        // assert((transformSize.GetHeight() - 1) * CFL_BUF_LINE + transformSize.GetWidth() <= CFL_BUF_SQUARE);
        Av1BitDepth bitDepth = this.sequenceHeader.ColorConfig.BitDepth;
        if ((bitDepth != Av1BitDepth.EightBit) || this.is16BitPipeline)
        {
            /* 16 bit pipeline
            svt_cfl_predict_hbd(
                chromaFromLumaContext->recon_buf_q3,
                (uint16_t*)dst,
                dst_stride,
                (uint16_t*)dst,
                dst_stride,
                alpha_q3,
                cc->bit_depth,
                tx_size_wide[tx_size],
                tx_size_high[tx_size]);
            return;*/
        }

        ChromaFromLumaPredict(
            chromaFromLumaContext.Q3Buffer!.DangerousGetSingleSpan(),
            pixelBuffer,
            stride,
            pixelBuffer,
            stride,
            alphaQ3,
            bitDepth,
            transformSize.GetWidth(),
            transformSize.GetHeight());
    }

    private static bool IsChromaFromLumaAllowedWithFrameHeader(Av1PartitionInfo partitionInfo, ObuColorConfig colorConfig, ObuFrameHeader frameHeader)
    {
        Av1BlockModeInfo modeInfo = partitionInfo.ModeInfo;
        Av1BlockSize blockSize = modeInfo.BlockSize;
        DebugGuard.MustBeGreaterThan((int)blockSize, (int)Av1BlockSize.AllSizes, nameof(blockSize));
        if (frameHeader.LosslessArray[modeInfo.SegmentId])
        {
            // In lossless, CfL is available when the partition size is equal to the
            // transform size.
            bool subX = colorConfig.SubSamplingX;
            bool subY = colorConfig.SubSamplingY;
            Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subX, subY);
            return planeBlockSize == Av1BlockSize.Block4x4;
        }

        // Spec: CfL is available to luma partitions lesser than or equal to 32x32
        return blockSize.GetWidth() <= 32 && blockSize.GetHeight() <= 32;
    }

    private static int ChromaFromLumaIndexToAlpha(int alphaIndex, int jointSign, Av1Plane plane)
    {
        int alphaSign = (plane == Av1Plane.U) ? Av1ChromaFromLumaMath.SignU(jointSign) : Av1ChromaFromLumaMath.SignV(jointSign);
        if (alphaSign == Av1ChromaFromLumaMath.SignZero)
        {
            return 0;
        }

        int absAlphaQ3 = (plane == Av1Plane.U) ? Av1ChromaFromLumaMath.IndexU(alphaIndex) : Av1ChromaFromLumaMath.IndexV(alphaIndex);
        return (alphaSign == Av1ChromaFromLumaMath.SignPositive) ? absAlphaQ3 + 1 : -absAlphaQ3 - 1;
    }

    private static int GetScaledLumaQ0(int alphaQ3, short predictedQ3)
    {
        int scaledLumaQ6 = alphaQ3 * predictedQ3;
        return Av1Math.RoundPowerOf2Signed(scaledLumaQ6, 6);
    }

    private static void ChromaFromLumaPredict(Span<short> predictedBufferQ3, Span<byte> predictedBuffer, int predictedStride, Span<byte> destinationBuffer, int destinationStride, int alphaQ3, Av1BitDepth bitDepth, int width, int height)
    {
        // TODO: Make SIMD variant of this method.
        int maxPixelValue = (1 << bitDepth.GetBitCount()) - 1;
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                int alphaQ0 = GetScaledLumaQ0(alphaQ3, predictedBufferQ3[i]);
                destinationBuffer[i] = (byte)Av1Math.Clamp(alphaQ0 + predictedBuffer[i], 0, maxPixelValue);
            }

            destinationBuffer = destinationBuffer[width..];
            predictedBuffer = predictedBuffer[width..];
            predictedBufferQ3 = predictedBufferQ3[width..];
        }
    }

    private void PredictIntraBlock(
        Av1PartitionInfo partitionInfo,
        Av1Plane plane,
        Av1TransformSize transformSize,
        Av1TileInfo tileInfo,
        Span<byte> pixelBuffer,
        int pixelBufferStride,
        Span<byte> topNeighbor,
        Span<byte> leftNeighbor,
        int referenceStride,
        Av1PredictionMode mode,
        int blockModeInfoColumnOffset,
        int blockModeInfoRowOffset,
        Av1BitDepth bitDepth)
    {
        // TODO:are_parameters_computed variable for CFL so that cal part for V plane we can skip,
        // once we compute for U plane, this parameter is block level parameter.
        ObuColorConfig cc = this.sequenceHeader.ColorConfig;
        int subX = plane != Av1Plane.Y ? cc.SubSamplingX ? 1 : 0 : 0;
        int subY = plane != Av1Plane.Y ? cc.SubSamplingY ? 1 : 0 : 0;

        Av1BlockModeInfo modeInfo = partitionInfo.ModeInfo;

        int transformWidth = transformSize.GetWidth();
        int transformHeight = transformSize.GetHeight();

        bool usePalette = modeInfo.GetPaletteSize(plane) > 0;

        if (usePalette)
        {
            return;
        }

        Av1FilterIntraMode filterIntraMode = (plane == Av1Plane.Y && modeInfo.FilterIntraModeInfo.UseFilterIntra)
            ? modeInfo.FilterIntraModeInfo.Mode : Av1FilterIntraMode.FilterIntraModes;

        int angleDelta = modeInfo.AngleDelta[Math.Min(1, (int)plane)];

        Av1BlockSize blockSize = modeInfo.BlockSize;
        bool haveTop = blockModeInfoRowOffset > 0 || (subY > 0 ? partitionInfo.AvailableAboveForChroma : partitionInfo.AvailableAbove);
        bool haveLeft = blockModeInfoColumnOffset > 0 || (subX > 0 ? partitionInfo.AvailableLeftForChroma : partitionInfo.AvailableLeft);

        int modeInfoRow = -partitionInfo.ModeBlockToTopEdge >> (3 + Av1Constants.ModeInfoSizeLog2);
        int modeInfoColumn = -partitionInfo.ModeBlockToLeftEdge >> (3 + Av1Constants.ModeInfoSizeLog2);
        int xrOffset = 0;
        int ydOffset = 0;

        // Distance between right edge of this pred block to frame right edge
        int xr = (partitionInfo.ModeBlockToRightEdge >> (3 + subX)) + (partitionInfo.WidthInPixels[(int)plane] - (blockModeInfoColumnOffset << Av1Constants.ModeInfoSizeLog2) - transformWidth) -
            xrOffset;

        // Distance between bottom edge of this pred block to frame bottom edge
        int yd = (partitionInfo.ModeBlockToBottomEdge >> (3 + subY)) +
            (partitionInfo.HeightInPixels[(int)plane] - (blockModeInfoRowOffset << Av1Constants.ModeInfoSizeLog2) - transformHeight) - ydOffset;
        bool rightAvailable = modeInfoColumn + ((blockModeInfoColumnOffset + transformWidth) << subX) < tileInfo.ModeInfoColumnEnd;
        bool bottomAvailable = (yd > 0) && (modeInfoRow + ((blockModeInfoRowOffset + transformHeight) << subY) < tileInfo.ModeInfoRowEnd);

        Av1PartitionType partition = modeInfo.PartitionType;

        // force 4x4 chroma component block size.
        blockSize = ScaleChromaBlockSize(blockSize, subX == 1, subY == 1);

        bool haveTopRight = IntraHasTopRight(
            this.sequenceHeader.SuperblockSize,
            blockSize,
            modeInfoRow,
            modeInfoColumn,
            haveTop,
            rightAvailable,
            partition,
            transformSize,
            blockModeInfoRowOffset,
            blockModeInfoColumnOffset,
            subX,
            subY);
        bool haveBottomLeft = IntraHasBottomLeft(
            this.sequenceHeader.SuperblockSize,
            blockSize,
            modeInfoRow,
            modeInfoColumn,
            bottomAvailable,
            haveLeft,
            partition,
            transformSize,
            blockModeInfoRowOffset,
            blockModeInfoColumnOffset,
            subX,
            subY);

        bool disableEdgeFilter = !this.sequenceHeader.EnableIntraEdgeFilter;

        // Calling all other intra predictors except CFL & pallate...
        if (bitDepth == Av1BitDepth.EightBit && !this.is16BitPipeline)
        {
            this.DecodeBuildIntraPredictors(
                partitionInfo,
                topNeighbor,
                leftNeighbor,
                (nuint)referenceStride,
                pixelBuffer,
                (nuint)pixelBufferStride,
                mode,
                angleDelta,
                filterIntraMode,
                transformSize,
                disableEdgeFilter,
                haveTop ? Math.Min(transformWidth, xr + transformWidth) : 0,
                haveTopRight ? Math.Min(transformWidth, xr) : 0,
                haveLeft ? Math.Min(transformHeight, yd + transformHeight) : 0,
                haveBottomLeft ? Math.Min(transformHeight, yd) : 0,
                plane);
        }
        else
        {
            /* 16bit
            decode_build_intra_predictors_high(xd,
                (uint16_t*) top_neigh_array, //As per SVT Enc
                (uint16_t*) left_neigh_array,
                ref_stride,// As per SVT Enc
                (uint16_t*) pv_pred_buf,
                pred_stride,
                mode,
                angle_delta,
                filter_intra_mode,
                tx_size,
                disable_edge_filter,
                have_top? AOMMIN(transformWidth, xr + transformWidth) : 0,
                have_top_right? AOMMIN(transformWidth, xr) : 0,
                have_left? AOMMIN(transformHeight, yd + transformHeight) : 0,
                have_bottom_left? AOMMIN(transformHeight, yd) : 0,
                plane,
                bit_depth);
            */
        }
    }

    private static Av1BlockSize ScaleChromaBlockSize(Av1BlockSize blockSize, bool subX, bool subY)
    {
        Av1BlockSize bs = blockSize;
        switch (blockSize)
        {
            case Av1BlockSize.Block4x4:
                if (subX && subY)
                {
                    bs = Av1BlockSize.Block8x8;
                }
                else if (subX)
                {
                    bs = Av1BlockSize.Block8x4;
                }
                else if (subY)
                {
                    bs = Av1BlockSize.Block4x8;
                }

                break;
            case Av1BlockSize.Block4x8:
                if (subX && subY)
                {
                    bs = Av1BlockSize.Block8x8;
                }
                else if (subX)
                {
                    bs = Av1BlockSize.Block8x8;
                }
                else if (subY)
                {
                    bs = Av1BlockSize.Block4x8;
                }

                break;
            case Av1BlockSize.Block8x4:
                if (subX && subY)
                {
                    bs = Av1BlockSize.Block8x8;
                }
                else if (subX)
                {
                    bs = Av1BlockSize.Block8x4;
                }
                else if (subY)
                {
                    bs = Av1BlockSize.Block8x8;
                }

                break;
            case Av1BlockSize.Block4x16:
                if (subX && subY)
                {
                    bs = Av1BlockSize.Block8x16;
                }
                else if (subX)
                {
                    bs = Av1BlockSize.Block8x16;
                }
                else if (subY)
                {
                    bs = Av1BlockSize.Block4x16;
                }

                break;
            case Av1BlockSize.Block16x4:
                if (subX && subY)
                {
                    bs = Av1BlockSize.Block16x8;
                }
                else if (subX)
                {
                    bs = Av1BlockSize.Block16x4;
                }
                else if (subY)
                {
                    bs = Av1BlockSize.Block16x8;
                }

                break;
            default:
                break;
        }

        return bs;
    }

    private static bool IntraHasBottomLeft(Av1BlockSize superblockSize, Av1BlockSize blockSize, int modeInfoRow, int modeInfoColumn, bool bottomAvailable, bool haveLeft, Av1PartitionType partition, Av1TransformSize transformSize, int blockModeInfoRowOffset, int blockModeInfoColumnOffset, int subX, int subY)
    {
        if (!bottomAvailable || !haveLeft)
        {
            return false;
        }

        // Special case for 128x* blocks, when col_off is half the block width.
        // This is needed because 128x* superblocks are divided into 64x* blocks in
        // raster order
        if (blockSize.GetWidth() > 64 && blockModeInfoColumnOffset > 0)
        {
            int planeBlockWidthInUnits64 = 64 >> subX;
            int columnOffset64 = blockModeInfoColumnOffset % planeBlockWidthInUnits64;
            if (columnOffset64 == 0)
            {
                // We are at the left edge of top-right or bottom-right 64x* block.
                int planeBlockHeightInUnits64 = 64 >> subY;
                int rowOffset64 = blockModeInfoRowOffset % planeBlockHeightInUnits64;
                int planeBlockHeightInUnits = Math.Min(blockSize.Get4x4HighCount() >> subY, planeBlockHeightInUnits64);

                // Check if all bottom-left pixels are in the left 64x* block (which is
                // already coded).
                return rowOffset64 + transformSize.Get4x4HighCount() < planeBlockHeightInUnits;
            }
        }

        if (blockModeInfoColumnOffset > 0)
        {
            // Bottom-left pixels are in the bottom-left block, which is not available.
            return false;
        }
        else
        {
            int blockHeightInUnits = blockSize.GetHeight() >> Av1TransformSize.Size4x4.GetBlockHeightLog2();
            int planeBlockHeightInUnits = Math.Max(blockHeightInUnits >> subY, 1);
            int bottomLeftUnitCount = transformSize.Get4x4HighCount();

            // All bottom-left pixels are in the left block, which is already available.
            if (blockModeInfoRowOffset + bottomLeftUnitCount < planeBlockHeightInUnits)
            {
                return true;
            }

            int blockWidthInModeInfoLog2 = blockSize.Get4x4WidthLog2();
            int blockHeightInModeInfoLog2 = blockSize.Get4x4HeightLog2();
            int superblockModeInfoSize = superblockSize.Get4x4HighCount();
            int blockRowInSuperblock = (modeInfoRow & (superblockModeInfoSize - 1)) >> blockHeightInModeInfoLog2;
            int blockColumnInSuperblock = (modeInfoColumn & (superblockModeInfoSize - 1)) >> blockWidthInModeInfoLog2;

            // Leftmost column of superblock: so bottom-left pixels maybe in the left
            // and/or bottom-left superblocks. But only the left superblock is
            // available, so check if all required pixels fall in that superblock.
            if (blockColumnInSuperblock == 0)
            {
                int blockStartRowOffset = blockRowInSuperblock << (blockHeightInModeInfoLog2 + Av1Constants.ModeInfoSizeLog2 - Av1TransformSize.Size4x4.GetBlockWidthLog2()) >> subY;
                int rowOffsetInSuperblock = blockStartRowOffset + blockModeInfoRowOffset;
                int superblockHeightInUnits = superblockModeInfoSize >> subY;
                return rowOffsetInSuperblock + bottomLeftUnitCount < superblockHeightInUnits;
            }

            // Bottom row of superblock (and not the leftmost column): so bottom-left
            // pixels fall in the bottom superblock, which is not available yet.
            if (((blockRowInSuperblock + 1) << blockHeightInModeInfoLog2) >= superblockModeInfoSize)
            {
                return false;
            }

            // General case (neither leftmost column nor bottom row): check if the
            // bottom-left block is coded before the current block.
            int thisBlockIndex = ((blockRowInSuperblock + 0) << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2 - blockWidthInModeInfoLog2)) + blockColumnInSuperblock + 0;
            return Av1BottomRightTopLeftConstants.HasBottomLeft(partition, blockSize, thisBlockIndex);
        }
    }

    private static bool IntraHasTopRight(Av1BlockSize superblockSize, Av1BlockSize blockSize, int modeInfoRow, int modeInfoColumn, bool haveTop, bool rightAvailable, Av1PartitionType partition, Av1TransformSize transformSize, int blockModeInfoRowOffset, int blockModeInfoColumnOffset, int subX, int subY)
    {
        if (!haveTop || !rightAvailable)
        {
            return false;
        }

        int blockWideInUnits = blockSize.GetWidth() >> 2;
        int planeBlockWidthInUnits = Math.Max(blockWideInUnits >> subX, 1);
        int topRightUnitCount = transformSize.Get4x4WideCount();

        if (blockModeInfoRowOffset > 0)
        { // Just need to check if enough pixels on the right.
            if (blockSize.GetWidth() > 64)
            {
                // Special case: For 128x128 blocks, the transform unit whose
                // top-right corner is at the center of the block does in fact have
                // pixels available at its top-right corner.
                if (blockModeInfoRowOffset == 64 >> subY &&
                    blockModeInfoColumnOffset + topRightUnitCount == 64 >> subX)
                {
                    return true;
                }

                int planeBlockWidthInUnits64 = 64 >> subX;
                int blockModeInfoColumnOffset64 = blockModeInfoColumnOffset % planeBlockWidthInUnits64;
                return blockModeInfoColumnOffset64 + topRightUnitCount < planeBlockWidthInUnits64;
            }

            return blockModeInfoColumnOffset + topRightUnitCount < planeBlockWidthInUnits;
        }
        else
        {
            // All top-right pixels are in the block above, which is already available.
            if (blockModeInfoColumnOffset + topRightUnitCount < planeBlockWidthInUnits)
            {
                return true;
            }

            int blockWidthInModeInfoLog2 = blockSize.Get4x4WidthLog2();
            int blockHeightInModeInfeLog2 = blockSize.Get4x4HeightLog2();
            int superBlockModeInfoSize = superblockSize.Get4x4HighCount();
            int blockRowInSuperblock = (modeInfoRow & (superBlockModeInfoSize - 1)) >> blockHeightInModeInfeLog2;
            int blockColumnInSuperBlock = (modeInfoColumn & (superBlockModeInfoSize - 1)) >> blockWidthInModeInfoLog2;

            // Top row of superblock: so top-right pixels are in the top and/or
            // top-right superblocks, both of which are already available.
            if (blockRowInSuperblock == 0)
            {
                return true;
            }

            // Rightmost column of superblock (and not the top row): so top-right pixels
            // fall in the right superblock, which is not available yet.
            if (((blockColumnInSuperBlock + 1) << blockWidthInModeInfoLog2) >= superBlockModeInfoSize)
            {
                return false;
            }

            // General case (neither top row nor rightmost column): check if the
            // top-right block is coded before the current block.
            int thisBlockIndex = ((blockRowInSuperblock + 0) << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2 - blockWidthInModeInfoLog2)) + blockColumnInSuperBlock + 0;
            return Av1BottomRightTopLeftConstants.HasTopRight(partition, blockSize, thisBlockIndex);
        }
    }

    private void DecodeBuildIntraPredictors(
        Av1PartitionInfo partitionInfo,
        Span<byte> aboveNeighbor,
        Span<byte> leftNeighbor,
        nuint referenceStride,
        Span<byte> destination,
        nuint destinationStride,
        Av1PredictionMode mode,
        int angleDelta,
        Av1FilterIntraMode filterIntraMode,
        Av1TransformSize transformSize,
        bool disableEdgeFilter,
        int topPixelCount,
        int topRightPixelCount,
        int leftPixelCount,
        int bottomLeftPixelCount,
        Av1Plane plane)
    {
        Span<byte> aboveData = stackalloc byte[(Av1Constants.MaxTransformSize * 2) + 32];
        Span<byte> leftData = stackalloc byte[(Av1Constants.MaxTransformSize * 2) + 32];
        Span<byte> aboveRow = aboveData[16..];
        Span<byte> leftColumn = leftData[16..];
        int transformWidth = transformSize.GetWidth();
        int transformHeight = transformSize.GetHeight();
        bool isDirectionalMode = mode.IsDirectional();
        Av1NeighborNeed need = mode.GetNeighborNeed();
        bool needLeft = (need & Av1NeighborNeed.Left) == Av1NeighborNeed.Left;
        bool needAbove = (need & Av1NeighborNeed.Above) == Av1NeighborNeed.Above;
        bool needAboveLeft = (need & Av1NeighborNeed.AboveLeft) == Av1NeighborNeed.AboveLeft;
        int angle = 0;
        bool useFilterIntra = filterIntraMode != Av1FilterIntraMode.FilterIntraModes;

        if (isDirectionalMode)
        {
            angle = mode.ToAngle() + (angleDelta * Av1Constants.AngleStep);
            if (angle <= 90)
            {
                needAbove = true;
                needLeft = false;
                needAboveLeft = true;
            }
            else if (angle < 180)
            {
                needAbove = true;
                needLeft = true;
                needAboveLeft = true;
            }
            else
            {
                needAbove = false;
                needLeft = true;
                needAboveLeft = true;
            }
        }

        if (useFilterIntra)
        {
            needAbove = true;
            needLeft = true;
            needAboveLeft = true;
        }

        DebugGuard.MustBeGreaterThanOrEqualTo(topPixelCount, 0, nameof(topPixelCount));
        DebugGuard.MustBeGreaterThanOrEqualTo(topRightPixelCount, 0, nameof(topRightPixelCount));
        DebugGuard.MustBeGreaterThanOrEqualTo(leftPixelCount, 0, nameof(leftPixelCount));
        DebugGuard.MustBeGreaterThanOrEqualTo(bottomLeftPixelCount, 0, nameof(bottomLeftPixelCount));

        if ((!needAbove && leftPixelCount == 0) || (!needLeft && topPixelCount == 0))
        {
            byte val;
            if (needLeft)
            {
                val = (byte)((topPixelCount > 0) ? aboveNeighbor[0] : 129);
            }
            else
            {
                val = (byte)((leftPixelCount > 0) ? leftNeighbor[0] : 127);
            }

            ref byte destinationRef = ref destination[0];
            for (int i = 0; i < transformHeight; ++i)
            {
                Unsafe.InitBlock(ref destinationRef, val, (uint)transformWidth);
                destinationRef = ref Unsafe.Add(ref destinationRef, destinationStride);
            }

            return;
        }

        // NEED_LEFT
        if (needLeft)
        {
            bool needBottom = (need & Av1NeighborNeed.BottomLeft) == Av1NeighborNeed.BottomLeft;
            if (useFilterIntra)
            {
                needBottom = false;
            }

            if (isDirectionalMode)
            {
                needBottom = angle > 180;
            }

            uint numLeftPixelsNeeded = (uint)(transformHeight + (needBottom ? transformWidth : 0));
            int i = 0;
            if (leftPixelCount > 0)
            {
                for (; i < leftPixelCount; i++)
                {
                    leftColumn[i] = leftNeighbor[i * (int)referenceStride];
                }

                if (needBottom && bottomLeftPixelCount > 0)
                {
                    Guard.IsTrue(i == transformHeight, nameof(i), string.Empty);
                    for (; i < transformHeight + bottomLeftPixelCount; i++)
                    {
                        leftColumn[i] = leftNeighbor[i * (int)referenceStride];
                    }
                }

                if (i < numLeftPixelsNeeded)
                {
                    Unsafe.InitBlock(ref leftColumn[i], leftColumn[i - 1], numLeftPixelsNeeded - (uint)i);
                }
            }
            else
            {
                if (topPixelCount > 0)
                {
                    Unsafe.InitBlock(ref leftColumn[0], aboveNeighbor[0], numLeftPixelsNeeded);
                }
                else
                {
                    Unsafe.InitBlock(ref leftColumn[0], 129, numLeftPixelsNeeded);
                }
            }
        }

        // NEED_ABOVE
        if (needAbove)
        {
            bool needRight = (need & Av1NeighborNeed.AboveRight) == Av1NeighborNeed.AboveRight;
            if (useFilterIntra)
            {
                needRight = false;
            }

            if (isDirectionalMode)
            {
                needRight = angle < 90;
            }

            uint numTopPixelsNeeded = (uint)(transformWidth + (needRight ? transformHeight : 0));
            if (topPixelCount > 0)
            {
                Unsafe.CopyBlock(ref aboveRow[0], ref aboveNeighbor[0], (uint)topPixelCount);
                int i = topPixelCount;
                if (needRight && topPixelCount > 0)
                {
                    Guard.IsTrue(topPixelCount == transformWidth, nameof(topPixelCount), string.Empty);
                    Unsafe.CopyBlock(ref aboveRow[transformWidth], ref aboveNeighbor[transformWidth], (uint)topPixelCount);
                    i += topPixelCount;
                }

                if (i < numTopPixelsNeeded)
                {
                    Unsafe.InitBlock(ref aboveRow[i], aboveRow[i - 1], numTopPixelsNeeded - (uint)i);
                }
            }
            else
            {
                if (leftPixelCount > 0)
                {
                    Unsafe.InitBlock(ref aboveRow[0], leftNeighbor[0], numTopPixelsNeeded);
                }
                else
                {
                    Unsafe.InitBlock(ref aboveRow[0], 127, numTopPixelsNeeded);
                }
            }
        }

        if (needAboveLeft)
        {
            if (topPixelCount > 0 && leftPixelCount > 0)
            {
                aboveRow[-1] = aboveNeighbor[-1];
            }
            else if (topPixelCount > 0)
            {
                aboveRow[-1] = aboveNeighbor[0];
            }
            else if (leftPixelCount > 0)
            {
                aboveRow[-1] = leftNeighbor[0];
            }
            else
            {
                aboveRow[-1] = 128;
            }

            leftColumn[-1] = aboveRow[-1];
        }

        if (useFilterIntra)
        {
            Av1PredictorFactory.FilterIntraPredictor(destination, destinationStride, transformSize, aboveRow, leftColumn, filterIntraMode);
            return;
        }

        if (isDirectionalMode)
        {
            bool upsampleAbove = false;
            bool upsampleLeft = false;
            if (!disableEdgeFilter)
            {
                bool needRight = angle < 90;
                bool needBottom = angle > 180;

                bool filterType = GetFilterType(partitionInfo, plane);

                if (angle is not 90 and not 180)
                {
                    int ab_le = needAboveLeft ? 1 : 0;
                    if (needAbove && needLeft && (transformWidth + transformHeight >= 24))
                    {
                        FilterIntraEdgeCorner(aboveRow, leftColumn);
                    }

                    if (needAbove && topPixelCount > 0)
                    {
                        int strength = IntraEdgeFilterStrength(transformWidth, transformHeight, angle - 90, filterType);
                        int pixelCount = topPixelCount + ab_le + (needRight ? transformHeight : 0);
                        FilterIntraEdge(ref Unsafe.Subtract(ref aboveRow[0], ab_le), pixelCount, strength);
                    }

                    if (needLeft && leftPixelCount > 0)
                    {
                        int strength = IntraEdgeFilterStrength(transformHeight, transformWidth, angle - 180, filterType);
                        int pixelCount = leftPixelCount + ab_le + (needBottom ? transformWidth : 0);
                        FilterIntraEdge(ref Unsafe.Subtract(ref leftColumn[0], ab_le), pixelCount, strength);
                    }
                }

                upsampleAbove = UseIntraEdgeUpsample(transformWidth, transformHeight, angle - 90, filterType);
                if (needAbove && upsampleAbove)
                {
                    int pixelCount = transformWidth + (needRight ? transformHeight : 0);

                    UpsampleIntraEdge(aboveRow, pixelCount);
                }

                upsampleLeft = UseIntraEdgeUpsample(transformHeight, transformWidth, angle - 180, filterType);
                if (needLeft && upsampleLeft)
                {
                    int pixelCount = transformHeight + (needBottom ? transformWidth : 0);

                    UpsampleIntraEdge(leftColumn, pixelCount);
                }
            }

            Av1PredictorFactory.DirectionalPredictor(destination, destinationStride, transformSize, aboveRow, leftColumn, upsampleAbove, upsampleLeft, angle);
            return;
        }

        // predict
        if (mode == Av1PredictionMode.DC)
        {
            Av1PredictorFactory.DcPredictor(leftPixelCount > 0, topPixelCount > 0, transformSize, destination, destinationStride, aboveRow, leftColumn);
        }
        else
        {
            Av1PredictorFactory.GeneralPredictor(mode, transformSize, destination, destinationStride, aboveRow, leftColumn);
        }
    }

    private static void UpsampleIntraEdge(Span<byte> buffer, int count)
    {
        // TODO: Consider creating SIMD version

        // interpolate half-sample positions
        Guard.MustBeLessThanOrEqualTo(count, MaxUpsampleSize, nameof(count));

        Span<byte> input = stackalloc byte[MaxUpsampleSize + 3];
        byte beforeBuffer = Unsafe.Subtract(ref buffer[0], 1);

        // copy p[-1..(sz-1)] and extend first and last samples
        input[0] = beforeBuffer;
        input[1] = beforeBuffer;
        for (int i = 0; i < count; i++)
        {
            input[i + 2] = buffer[i];
        }

        input[count + 2] = buffer[count - 1];

        // interpolate half-sample edge positions
        buffer[-2] = input[0];
        for (int i = 0; i < count; i++)
        {
            int s = -input[i] + (9 * input[i + 1]) + (9 * input[i + 2]) - input[i + 3];
            s = Av1Math.Clamp((s + 8) >> 4, 0, 255);
            buffer[(2 * i) - 1] = (byte)s;
            buffer[2 * i] = input[i + 2];
        }
    }

    private static bool UseIntraEdgeUpsample(int width, int height, int delta, bool type)
    {
        int d = Math.Abs(delta);
        int widthHeight = width + height;
        if (d is <= 0 or >= 40)
        {
            return false;
        }

        return type ? (widthHeight <= 8) : (widthHeight <= 16);
    }

    private static void FilterIntraEdge(ref byte buffer, int count, int strength)
    {
        // TODO: Consider creating SIMD version
        if (strength == 0)
        {
            return;
        }

        int[][] kernel = [
            [0, 4, 8, 4, 0], [0, 5, 6, 5, 0], [2, 4, 4, 4, 2]
        ];
        int filt = strength - 1;
        Span<byte> edge = stackalloc byte[129];

        Unsafe.CopyBlock(ref edge[0], ref buffer, (uint)count);
        for (int i = 1; i < count; i++)
        {
            int s = 0;
            for (int j = 0; j < 5; j++)
            {
                int k = i - 2 + j;
                k = (k < 0) ? 0 : k;
                k = (k > count - 1) ? count - 1 : k;
                s += edge[k] * kernel[filt][j];
            }

            s = (s + 8) >> 4;
            Unsafe.Add(ref buffer, i) = (byte)s;
        }
    }

    private static int IntraEdgeFilterStrength(int width, int height, int delta, bool filterType)
    {
        int d = Math.Abs(delta);
        int strength = 0;
        int widthHeight = width + height;
        if (!filterType)
        {
            if (widthHeight <= 8)
            {
                if (d >= 56)
                {
                    strength = 1;
                }
            }
            else if (widthHeight <= 12)
            {
                if (d >= 40)
                {
                    strength = 1;
                }
            }
            else if (widthHeight <= 16)
            {
                if (d >= 40)
                {
                    strength = 1;
                }
            }
            else if (widthHeight <= 24)
            {
                if (d >= 8)
                {
                    strength = 1;
                }

                if (d >= 16)
                {
                    strength = 2;
                }

                if (d >= 32)
                {
                    strength = 3;
                }
            }
            else if (widthHeight <= 32)
            {
                if (d >= 1)
                {
                    strength = 1;
                }

                if (d >= 4)
                {
                    strength = 2;
                }

                if (d >= 32)
                {
                    strength = 3;
                }
            }
            else
            {
                if (d >= 1)
                {
                    strength = 3;
                }
            }
        }
        else
        {
            if (widthHeight <= 8)
            {
                if (d >= 40)
                {
                    strength = 1;
                }

                if (d >= 64)
                {
                    strength = 2;
                }
            }
            else if (widthHeight <= 16)
            {
                if (d >= 20)
                {
                    strength = 1;
                }

                if (d >= 48)
                {
                    strength = 2;
                }
            }
            else if (widthHeight <= 24)
            {
                if (d >= 4)
                {
                    strength = 3;
                }
            }
            else
            {
                if (d >= 1)
                {
                    strength = 3;
                }
            }
        }

        return strength;
    }

    private static void FilterIntraEdgeCorner(Span<byte> above, Span<byte> left)
    {
        int[] kernel = [5, 6, 5];

        ref byte aboveRef = ref above[0];
        ref byte leftRef = ref left[0];
        ref byte abovePreviousRef = ref Unsafe.Subtract(ref aboveRef, 1);
        ref byte leftPreviousRef = ref Unsafe.Subtract(ref leftRef, 1);
        int s = (leftRef * kernel[0]) + (abovePreviousRef * kernel[1]) + (aboveRef * kernel[2]);
        s = (s + 8) >> 4;
        abovePreviousRef = (byte)s;
        leftPreviousRef = (byte)s;
    }

    private static bool GetFilterType(Av1PartitionInfo partitionInfo, Av1Plane plane)
    {
        Av1BlockModeInfo? above;
        Av1BlockModeInfo? left;
        if (plane == Av1Plane.Y)
        {
            above = partitionInfo.AboveModeInfo;
            left = partitionInfo.LeftModeInfo;
        }
        else
        {
            above = partitionInfo.AboveModeInfoForChroma;
            left = partitionInfo.LeftModeInfoForChroma;
        }

        bool aboveIsSmooth = (above != null) && IsSmooth(above, plane);
        bool leftIsSmooth = (left != null) && IsSmooth(left, plane);
        return aboveIsSmooth || leftIsSmooth;
    }

    private static bool IsSmooth(Av1BlockModeInfo modeInfo, Av1Plane plane)
    {
        if (plane == Av1Plane.Y)
        {
            Av1PredictionMode mode = modeInfo.YMode;
            return mode is Av1PredictionMode.Smooth or
                Av1PredictionMode.SmoothVertical or
                Av1PredictionMode.SmoothHorizontal;
        }
        else
        {
            // Inter mode not supported here.
            Av1PredictionMode uvMode = modeInfo.UvMode;
            return uvMode is Av1PredictionMode.Smooth or
                Av1PredictionMode.SmoothVertical or
                Av1PredictionMode.SmoothHorizontal;
        }
    }
}
