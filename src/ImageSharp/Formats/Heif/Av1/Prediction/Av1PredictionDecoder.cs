// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Reconstructs AV1 intra-predicted transform blocks from neighboring samples and decoded mode information.
/// </summary>
/// <remarks>
/// This type implements the intra prediction portion of the AV1 reconstruction process for 8-, 10-, and 12-bit samples.
/// </remarks>
internal class Av1PredictionDecoder
{
    /// <summary>
    /// The largest edge length for which AV1 permits intra-edge upsampling.
    /// </summary>
    private const int MaxUpsampleSize = 16;

    /// <summary>
    /// The sequence-level syntax that controls chroma sampling, bit depth, superblock size, and intra-edge filtering.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The frame-level syntax that controls segment lossless state and prediction behavior.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1PredictionDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The decoded sequence header for the current image.</param>
    /// <param name="frameHeader">The decoded frame header for the current image.</param>
    public Av1PredictionDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
    }

    /// <summary>
    /// Reconstructs an 8-bit intra-predicted transform block.
    /// </summary>
    /// <param name="partitionInfo">The decoded partition and mode state for the containing block.</param>
    /// <param name="plane">The color plane being reconstructed.</param>
    /// <param name="transformSize">The dimensions of the transform block.</param>
    /// <param name="tileInfo">The tile boundaries used to determine neighboring-sample availability.</param>
    /// <param name="pixelBuffer">The sample buffer beginning at the row above the destination block.</param>
    /// <param name="pixelStride">The distance, in samples, between pixel rows.</param>
    /// <param name="bitDepth">The bit depth of the reconstructed samples.</param>
    /// <param name="blockModeInfoColumnOffset">The transform block's horizontal offset within the mode-information block.</param>
    /// <param name="blockModeInfoRowOffset">The transform block's vertical offset within the mode-information block.</param>
    /// <remarks>Corresponds to <c>svt_av1_predict_intra</c> in SVT-AV1.</remarks>
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
        => this.DecodeCore(
            partitionInfo,
            plane,
            transformSize,
            tileInfo,
            pixelBuffer,
            pixelStride,
            bitDepth,
            blockModeInfoColumnOffset,
            blockModeInfoRowOffset);

    /// <summary>
    /// Reconstructs a 10-bit or 12-bit intra-predicted transform block.
    /// </summary>
    /// <param name="partitionInfo">The decoded partition and mode state for the containing block.</param>
    /// <param name="plane">The color plane being reconstructed.</param>
    /// <param name="transformSize">The dimensions of the transform block.</param>
    /// <param name="tileInfo">The tile boundaries used to determine neighboring-sample availability.</param>
    /// <param name="pixelBuffer">The sample buffer beginning at the row above the destination block.</param>
    /// <param name="pixelStride">The distance, in samples, between pixel rows.</param>
    /// <param name="bitDepth">The bit depth of the reconstructed samples.</param>
    /// <param name="blockModeInfoColumnOffset">The transform block's horizontal offset within the mode-information block.</param>
    /// <param name="blockModeInfoRowOffset">The transform block's vertical offset within the mode-information block.</param>
    /// <remarks>Implements the intra prediction portion of section 7.11.2 of the AV1 specification.</remarks>
    public void Decode(
        Av1PartitionInfo partitionInfo,
        Av1Plane plane,
        Av1TransformSize transformSize,
        Av1TileInfo tileInfo,
        Span<short> pixelBuffer,
        int pixelStride,
        Av1BitDepth bitDepth,
        int blockModeInfoColumnOffset,
        int blockModeInfoRowOffset)
        => this.DecodeCore(
            partitionInfo,
            plane,
            transformSize,
            tileInfo,
            pixelBuffer,
            pixelStride,
            bitDepth,
            blockModeInfoColumnOffset,
            blockModeInfoRowOffset);

    /// <summary>
    /// Reconstructs an intra-predicted transform block in its native sample representation.
    /// </summary>
    /// <typeparam name="T">The 8-bit or high-bit-depth sample type.</typeparam>
    /// <param name="partitionInfo">The decoded partition and mode state for the containing block.</param>
    /// <param name="plane">The color plane being reconstructed.</param>
    /// <param name="transformSize">The dimensions of the transform block.</param>
    /// <param name="tileInfo">The tile boundaries used to determine neighboring-sample availability.</param>
    /// <param name="pixelBuffer">The sample buffer beginning at the row above the destination block.</param>
    /// <param name="pixelStride">The distance, in samples, between pixel rows.</param>
    /// <param name="bitDepth">The bit depth of the reconstructed samples.</param>
    /// <param name="blockModeInfoColumnOffset">The transform block's horizontal offset within the mode-information block.</param>
    /// <param name="blockModeInfoRowOffset">The transform block's vertical offset within the mode-information block.</param>
    private void DecodeCore<T>(
        Av1PartitionInfo partitionInfo,
        Av1Plane plane,
        Av1TransformSize transformSize,
        Av1TileInfo tileInfo,
        Span<T> pixelBuffer,
        int pixelStride,
        Av1BitDepth bitDepth,
        int blockModeInfoColumnOffset,
        int blockModeInfoRowOffset)
        where T : unmanaged, IBinaryInteger<T>
    {
        int stride = pixelStride;

        // Unlike SVT's separate destination and reference pointers, this span begins at the
        // previous row. That layout exposes the top, top-left, and strided left samples without copying.
        Span<T> topNeighbor = pixelBuffer;
        Span<T> leftNeighbor = pixelBuffer[(stride - 1)..];
        Span<T> startOfPixels = pixelBuffer[stride..];

        Av1PredictionMode mode = (plane == Av1Plane.Y) ? partitionInfo.ModeInfo.YMode : partitionInfo.ModeInfo.UvMode;

        if (plane != Av1Plane.Y && partitionInfo.ModeInfo.UvMode == Av1PredictionMode.UvChromaFromLuma)
        {
            this.PredictIntraBlock(
                partitionInfo,
                plane,
                transformSize,
                tileInfo,
                startOfPixels,
                stride,
                topNeighbor,
                leftNeighbor,
                stride,
                Av1PredictionMode.DC,
                blockModeInfoColumnOffset,
                blockModeInfoRowOffset,
                bitDepth);

            this.PredictChromaFromLumaBlock(
                partitionInfo,
                partitionInfo.ChromaFromLumaContext,
                startOfPixels,
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
            startOfPixels,
            stride,
            topNeighbor,
            leftNeighbor,
            stride,
            mode,
            blockModeInfoColumnOffset,
            blockModeInfoRowOffset,
            bitDepth);
    }

    /// <summary>
    /// Applies chroma-from-luma scaling to the DC prediction for one chroma transform block.
    /// </summary>
    /// <typeparam name="T">The 8-bit or high-bit-depth sample type.</typeparam>
    /// <param name="partitionInfo">The decoded partition and mode state for the containing block.</param>
    /// <param name="chromaFromLumaContext">The block-level luma prediction context shared by the chroma planes.</param>
    /// <param name="pixelBuffer">The DC-predicted chroma samples that receive the luma-derived adjustment.</param>
    /// <param name="stride">The distance, in samples, between pixel rows.</param>
    /// <param name="transformSize">The dimensions of the chroma transform block.</param>
    /// <param name="plane">The U or V plane being reconstructed.</param>
    private void PredictChromaFromLumaBlock<T>(Av1PartitionInfo partitionInfo, Av1ChromaFromLumaContext? chromaFromLumaContext, Span<T> pixelBuffer, int stride, Av1TransformSize transformSize, Av1Plane plane)
        where T : unmanaged, IBinaryInteger<T>
    {
        Av1BlockModeInfo modeInfo = partitionInfo.ModeInfo;
        bool isChromaFromLumaAllowedFlag = IsChromaFromLumaAllowedWithFrameHeader(partitionInfo, this.sequenceHeader.ColorConfig, this.frameHeader);
        DebugGuard.IsTrue(isChromaFromLumaAllowedFlag, "Chroma from Luma should be allowed then computing it.");

        if (chromaFromLumaContext == null)
        {
            throw new InvalidOperationException("CFL context should have been defined already.");
        }

        // U computes the shared subsampled-luma parameters first; V reuses them for the
        // same block because both chroma planes have identical sampling geometry.
        if (!chromaFromLumaContext.AreParametersComputed)
        {
            chromaFromLumaContext.ComputeParameters(transformSize);
        }

        int alphaQ3 = ChromaFromLumaIndexToAlpha(modeInfo.ChromaFromLumaAlphaIndex, modeInfo.ChromaFromLumaAlphaSign, plane);

        Av1BitDepth bitDepth = this.sequenceHeader.ColorConfig.BitDepth;
        ChromaFromLumaPredict(
            chromaFromLumaContext.Q3Buffer,
            pixelBuffer,
            stride,
            pixelBuffer,
            stride,
            alphaQ3,
            bitDepth,
            transformSize.GetWidth(),
            transformSize.GetHeight());
    }

    /// <summary>
    /// Determines whether chroma-from-luma prediction is permitted for the current block and frame state.
    /// </summary>
    /// <param name="partitionInfo">The decoded partition and mode state for the containing block.</param>
    /// <param name="colorConfig">The sequence color configuration.</param>
    /// <param name="frameHeader">The decoded frame header.</param>
    /// <returns><see langword="true"/> when the block may use chroma-from-luma prediction; otherwise, <see langword="false"/>.</returns>
    private static bool IsChromaFromLumaAllowedWithFrameHeader(Av1PartitionInfo partitionInfo, ObuColorConfig colorConfig, ObuFrameHeader frameHeader)
    {
        Av1BlockModeInfo modeInfo = partitionInfo.ModeInfo;
        Av1BlockSize blockSize = modeInfo.BlockSize;
        DebugGuard.MustBeLessThan((int)blockSize, (int)Av1BlockSize.AllSizes, nameof(blockSize));
        if (frameHeader.LosslessArray[modeInfo.SegmentId])
        {
            // In lossless, CfL is available when the partition size is equal to the
            // transform size.
            bool subX = colorConfig.SubSamplingX;
            bool subY = colorConfig.SubSamplingY;
            Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subX, subY);
            return planeBlockSize == Av1BlockSize.Block4x4;
        }

        // Outside lossless mode, AV1 limits CfL to luma partitions no larger than 32 by 32.
        return blockSize.GetWidth() <= 32 && blockSize.GetHeight() <= 32;
    }

    /// <summary>
    /// Converts the packed chroma-from-luma magnitude and joint sign into a signed Q3 scaling factor.
    /// </summary>
    /// <param name="alphaIndex">The packed U and V alpha magnitudes.</param>
    /// <param name="jointSign">The joint U and V alpha-sign symbol.</param>
    /// <param name="plane">The U or V plane whose alpha value is selected.</param>
    /// <returns>The signed Q3 alpha value for the selected chroma plane.</returns>
    public static int ChromaFromLumaIndexToAlpha(int alphaIndex, int jointSign, Av1Plane plane)
    {
        int alphaSign = (plane == Av1Plane.U) ? Av1ChromaFromLumaMath.SignU(jointSign) : Av1ChromaFromLumaMath.SignV(jointSign);
        if (alphaSign == Av1ChromaFromLumaMath.SignZero)
        {
            return 0;
        }

        int absAlphaQ3 = (plane == Av1Plane.U) ? Av1ChromaFromLumaMath.IndexU(alphaIndex) : Av1ChromaFromLumaMath.IndexV(alphaIndex);
        return (alphaSign == Av1ChromaFromLumaMath.SignPositive) ? absAlphaQ3 + 1 : -absAlphaQ3 - 1;
    }

    /// <summary>
    /// Multiplies a Q3 chroma-from-luma alpha by a Q3 luma residual and returns an integer sample adjustment.
    /// </summary>
    /// <param name="alphaQ3">The signed Q3 chroma scaling factor.</param>
    /// <param name="predictedQ3">The signed Q3 luma residual.</param>
    /// <returns>The signed integer sample adjustment.</returns>
    private static int GetScaledLumaQ0(int alphaQ3, short predictedQ3)
    {
        int scaledLumaQ6 = alphaQ3 * predictedQ3;
        return Av1Math.RoundPowerOf2Signed(scaledLumaQ6, 6);
    }

    /// <summary>
    /// Applies a chroma-from-luma residual to a chroma prediction block.
    /// </summary>
    /// <typeparam name="T">The 8-bit or high-bit-depth sample type.</typeparam>
    /// <param name="predictedBufferQ3">The Q3 luma residuals stored with the normative 32-sample CfL row stride.</param>
    /// <param name="predictedBuffer">The base chroma prediction samples.</param>
    /// <param name="predictedStride">The distance, in samples, between base prediction rows.</param>
    /// <param name="destinationBuffer">The buffer that receives the adjusted chroma samples.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="alphaQ3">The signed Q3 chroma scaling factor.</param>
    /// <param name="bitDepth">The bit depth used to clamp reconstructed samples.</param>
    /// <param name="width">The width of the prediction block in samples.</param>
    /// <param name="height">The height of the prediction block in samples.</param>
    public static void ChromaFromLumaPredict<T>(Span<short> predictedBufferQ3, Span<T> predictedBuffer, int predictedStride, Span<T> destinationBuffer, int destinationStride, int alphaQ3, Av1BitDepth bitDepth, int width, int height)
        where T : unmanaged, IBinaryInteger<T>
    {
        // TODO: Make SIMD variant of this method.
        int maxPixelValue = (1 << bitDepth.GetBitCount()) - 1;
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                int alphaQ0 = GetScaledLumaQ0(alphaQ3, predictedBufferQ3[i]);
                int predicted = int.CreateChecked(predictedBuffer[i]);
                destinationBuffer[i] = T.CreateChecked(Av1Math.Clamp(alphaQ0 + predicted, 0, maxPixelValue));
            }

            destinationBuffer = destinationBuffer[destinationStride..];
            predictedBuffer = predictedBuffer[predictedStride..];

            // The CfL scratch buffer has a fixed 32-sample row stride independent of transform width.
            predictedBufferQ3 = predictedBufferQ3[32..];
        }
    }

    /// <summary>
    /// Determines available reference samples and dispatches prediction for one transform block.
    /// </summary>
    /// <typeparam name="T">The 8-bit or high-bit-depth sample type.</typeparam>
    /// <param name="partitionInfo">The decoded partition and mode state for the containing block.</param>
    /// <param name="plane">The color plane being reconstructed.</param>
    /// <param name="transformSize">The dimensions of the transform block.</param>
    /// <param name="tileInfo">The tile boundaries used to determine neighboring-sample availability.</param>
    /// <param name="pixelBuffer">The destination samples for the transform block.</param>
    /// <param name="pixelBufferStride">The distance, in samples, between destination rows.</param>
    /// <param name="topNeighbor">The reconstructed samples along the top edge.</param>
    /// <param name="leftNeighbor">The reconstructed samples along the left edge.</param>
    /// <param name="referenceStride">The distance, in samples, between consecutive left-edge references.</param>
    /// <param name="mode">The intra prediction mode to apply.</param>
    /// <param name="blockModeInfoColumnOffset">The transform block's horizontal offset within the mode-information block.</param>
    /// <param name="blockModeInfoRowOffset">The transform block's vertical offset within the mode-information block.</param>
    /// <param name="bitDepth">The bit depth of the reconstructed samples.</param>
    private void PredictIntraBlock<T>(
        Av1PartitionInfo partitionInfo,
        Av1Plane plane,
        Av1TransformSize transformSize,
        Av1TileInfo tileInfo,
        Span<T> pixelBuffer,
        int pixelBufferStride,
        Span<T> topNeighbor,
        Span<T> leftNeighbor,
        int referenceStride,
        Av1PredictionMode mode,
        int blockModeInfoColumnOffset,
        int blockModeInfoRowOffset,
        Av1BitDepth bitDepth)
        where T : unmanaged, IBinaryInteger<T>
    {
        ObuColorConfig cc = this.sequenceHeader.ColorConfig;
        int subX = plane != Av1Plane.Y ? cc.SubSamplingX ? 1 : 0 : 0;
        int subY = plane != Av1Plane.Y ? cc.SubSamplingY ? 1 : 0 : 0;

        Av1BlockModeInfo modeInfo = partitionInfo.ModeInfo;

        int transformWidth = transformSize.GetWidth();
        int transformHeight = transformSize.GetHeight();
        int transformWidthInModeInfoUnits = transformSize.Get4x4WideCount();
        int transformHeightInModeInfoUnits = transformSize.Get4x4HighCount();

        bool usePalette = modeInfo.GetPaletteSize(plane) > 0;

        if (usePalette)
        {
            ReadOnlySpan<ushort> paletteColors = modeInfo.GetPaletteColors(plane);
            ReadOnlySpan<byte> colorIndexMap = modeInfo.GetPaletteColorIndexMap(plane);
            int paletteStride = partitionInfo.WidthInPixels[(int)plane];
            int mapOffset = ((blockModeInfoRowOffset << Av1Constants.ModeInfoSizeLog2) * paletteStride) +
                (blockModeInfoColumnOffset << Av1Constants.ModeInfoSizeLog2);

            // Every transform reconstructs its own window of the block-level palette map. Keeping the map padded to
            // the coded block dimensions lets edge transforms use the same addressing rule as interior transforms.
            for (int row = 0; row < transformHeight; row++)
            {
                Span<T> destinationRow = pixelBuffer.Slice(row * pixelBufferStride, transformWidth);
                ReadOnlySpan<byte> mapRow = colorIndexMap.Slice(mapOffset + (row * paletteStride), transformWidth);
                for (int column = 0; column < transformWidth; column++)
                {
                    destinationRow[column] = T.CreateChecked(paletteColors[mapRow[column]]);
                }
            }

            return;
        }

        Av1FilterIntraMode filterIntraMode = (plane == Av1Plane.Y && modeInfo.FilterIntraModeInfo.UseFilterIntra)
            ? modeInfo.FilterIntraModeInfo.Mode : Av1FilterIntraMode.AllFilterIntraModes;

        int angleDelta = modeInfo.AngleDelta[Math.Min(1, (int)plane)];

        Av1BlockSize blockSize = modeInfo.BlockSize;
        bool haveTop = blockModeInfoRowOffset > 0 || (subY > 0 ? partitionInfo.AvailableAboveForChroma : partitionInfo.AvailableAbove);
        bool haveLeft = blockModeInfoColumnOffset > 0 || (subX > 0 ? partitionInfo.AvailableLeftForChroma : partitionInfo.AvailableLeft);

        int modeInfoRow = -partitionInfo.ModeBlockToTopEdge >> (3 + Av1Constants.ModeInfoSizeLog2);
        int modeInfoColumn = -partitionInfo.ModeBlockToLeftEdge >> (3 + Av1Constants.ModeInfoSizeLog2);
        int xrOffset = 0;
        int ydOffset = 0;

        // These distances bound edge extension at the coded frame rather than allowing
        // a transform to read padding that happens to exist beyond the visible image.
        int xr = (partitionInfo.ModeBlockToRightEdge >> (3 + subX)) + (partitionInfo.WidthInPixels[(int)plane] - (blockModeInfoColumnOffset << Av1Constants.ModeInfoSizeLog2) - transformWidth) -
            xrOffset;

        int yd = (partitionInfo.ModeBlockToBottomEdge >> (3 + subY)) +
            (partitionInfo.HeightInPixels[(int)plane] - (blockModeInfoRowOffset << Av1Constants.ModeInfoSizeLog2) - transformHeight) - ydOffset;
        bool rightAvailable = modeInfoColumn + ((blockModeInfoColumnOffset + transformWidthInModeInfoUnits) << subX) < tileInfo.ModeInfoColumnEnd;
        bool bottomAvailable = (yd > 0) && (modeInfoRow + ((blockModeInfoRowOffset + transformHeightInModeInfoUnits) << subY) < tileInfo.ModeInfoRowEnd);

        Av1PartitionType partition = modeInfo.PartitionType;

        // Chroma prediction geometry cannot be smaller than 4 by 4 after subsampling.
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

        // Calling all other intra predictors except CFL and palette.
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
            plane,
            bitDepth.GetBitCount());
    }

    /// <summary>
    /// Adjusts sub-8-by-8 luma block geometry to the minimum chroma prediction block size.
    /// </summary>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="subX">A value indicating whether chroma is horizontally subsampled.</param>
    /// <param name="subY">A value indicating whether chroma is vertically subsampled.</param>
    /// <returns>The block size used to evaluate chroma reference availability.</returns>
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

    /// <summary>
    /// Determines whether every bottom-left reference sample required by a transform is already reconstructed.
    /// </summary>
    /// <param name="superblockSize">The sequence superblock size.</param>
    /// <param name="blockSize">The containing block size in the current plane's geometry.</param>
    /// <param name="modeInfoRow">The containing block row in 4-by-4 mode-information units.</param>
    /// <param name="modeInfoColumn">The containing block column in 4-by-4 mode-information units.</param>
    /// <param name="bottomAvailable">A value indicating whether the required rows remain inside the frame and tile.</param>
    /// <param name="haveLeft">A value indicating whether reconstructed samples exist immediately to the left.</param>
    /// <param name="partition">The partition type that determines reconstruction order.</param>
    /// <param name="transformSize">The transform size whose extended edge is required.</param>
    /// <param name="blockModeInfoRowOffset">The transform row offset within the containing block.</param>
    /// <param name="blockModeInfoColumnOffset">The transform column offset within the containing block.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <returns><see langword="true"/> when the bottom-left reference extension is available; otherwise, <see langword="false"/>.</returns>
    private static bool IntraHasBottomLeft(Av1BlockSize superblockSize, Av1BlockSize blockSize, int modeInfoRow, int modeInfoColumn, bool bottomAvailable, bool haveLeft, Av1PartitionType partition, Av1TransformSize transformSize, int blockModeInfoRowOffset, int blockModeInfoColumnOffset, int subX, int subY)
    {
        if (!bottomAvailable || !haveLeft)
        {
            return false;
        }

        // A 128-wide block is reconstructed as two 64-wide regions in raster order,
        // so the right half can consume references that already belong to the left half.
        if (blockSize.GetWidth() > 64 && blockModeInfoColumnOffset > 0)
        {
            int block64WidthInUnits = Av1BlockSize.Block64x64.Get4x4WideCount();
            int planeBlockWidthInUnits64 = block64WidthInUnits >> subX;
            int columnOffset64 = blockModeInfoColumnOffset % planeBlockWidthInUnits64;
            if (columnOffset64 == 0)
            {
                // We are at the left edge of top-right or bottom-right 64x* block.
                int block64HeightInUnits = Av1BlockSize.Block64x64.Get4x4HighCount();
                int planeBlockHeightInUnits64 = block64HeightInUnits >> subY;
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

    /// <summary>
    /// Determines whether every top-right reference sample required by a transform is already reconstructed.
    /// </summary>
    /// <param name="superblockSize">The sequence superblock size.</param>
    /// <param name="blockSize">The containing block size in the current plane's geometry.</param>
    /// <param name="modeInfoRow">The containing block row in 4-by-4 mode-information units.</param>
    /// <param name="modeInfoColumn">The containing block column in 4-by-4 mode-information units.</param>
    /// <param name="haveTop">A value indicating whether reconstructed samples exist immediately above.</param>
    /// <param name="rightAvailable">A value indicating whether the required columns remain inside the frame and tile.</param>
    /// <param name="partition">The partition type that determines reconstruction order.</param>
    /// <param name="transformSize">The transform size whose extended edge is required.</param>
    /// <param name="blockModeInfoRowOffset">The transform row offset within the containing block.</param>
    /// <param name="blockModeInfoColumnOffset">The transform column offset within the containing block.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <returns><see langword="true"/> when the top-right reference extension is available; otherwise, <see langword="false"/>.</returns>
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
        {
            // Transforms below the first row obtain their top edge from the containing block,
            // so only the reconstructed width to their right constrains availability.
            if (blockSize.GetWidth() > 64)
            {
                // Special case: For 128x128 blocks, the transform unit whose
                // top-right corner is at the center of the block does in fact have
                // pixels available at its top-right corner.
                int block64WidthInUnits = Av1BlockSize.Block64x64.Get4x4WideCount();
                int block64HeightInUnits = Av1BlockSize.Block64x64.Get4x4HighCount();
                if (blockModeInfoRowOffset == block64HeightInUnits >> subY &&
                    blockModeInfoColumnOffset + topRightUnitCount == block64WidthInUnits >> subX)
                {
                    return true;
                }

                int planeBlockWidthInUnits64 = block64WidthInUnits >> subX;
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

    /// <summary>
    /// Prepares normative reference-edge samples and runs the selected intra predictor.
    /// </summary>
    /// <typeparam name="T">The 8-bit or high-bit-depth sample type.</typeparam>
    /// <param name="partitionInfo">The decoded partition and neighboring mode state.</param>
    /// <param name="aboveNeighbor">The reconstructed top and top-right reference samples.</param>
    /// <param name="leftNeighbor">The reconstructed left and bottom-left reference samples.</param>
    /// <param name="referenceStride">The distance, in samples, between consecutive left-edge references.</param>
    /// <param name="destination">The buffer that receives the prediction block.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="mode">The intra prediction mode to apply.</param>
    /// <param name="angleDelta">The coded directional angle adjustment.</param>
    /// <param name="filterIntraMode">The selected filter intra mode, or the sentinel indicating that filter intra is disabled.</param>
    /// <param name="transformSize">The dimensions of the prediction block.</param>
    /// <param name="disableEdgeFilter">A value indicating whether intra-edge filtering and upsampling are disabled.</param>
    /// <param name="topPixelCount">The number of available top samples.</param>
    /// <param name="topRightPixelCount">The number of available top-right extension samples.</param>
    /// <param name="leftPixelCount">The number of available left samples.</param>
    /// <param name="bottomLeftPixelCount">The number of available bottom-left extension samples.</param>
    /// <param name="plane">The color plane being reconstructed.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    private void DecodeBuildIntraPredictors<T>(
        Av1PartitionInfo partitionInfo,
        Span<T> aboveNeighbor,
        Span<T> leftNeighbor,
        nuint referenceStride,
        Span<T> destination,
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
        Av1Plane plane,
        int bitDepth)
        where T : unmanaged, IBinaryInteger<T>
    {
        int baseValue = 128 << (bitDepth - 8);

        // Prefix storage is required because AV1 addresses the shared top-left sample at -1
        // and writes upsampled edge samples as far back as -2.
        Span<T> aboveData = stackalloc T[(Av1Constants.MaxTransformSize * 2) + 32];
        Span<T> leftData = stackalloc T[(Av1Constants.MaxTransformSize * 2) + 32];
        aboveData.Fill(T.CreateChecked(baseValue - 1));
        leftData.Fill(T.CreateChecked(baseValue + 1));
        Span<T> aboveRow = aboveData[16..];
        Span<T> leftColumn = leftData[16..];
        int transformWidth = transformSize.GetWidth();
        int transformHeight = transformSize.GetHeight();
        bool isDirectionalMode = mode.IsDirectional();
        Av1NeighborNeed need = mode.GetNeighborNeed();
        bool needLeft = (need & Av1NeighborNeed.Left) == Av1NeighborNeed.Left;
        bool needAbove = (need & Av1NeighborNeed.Above) == Av1NeighborNeed.Above;
        bool needAboveLeft = (need & Av1NeighborNeed.AboveLeft) == Av1NeighborNeed.AboveLeft;
        int angle = 0;
        bool useFilterIntra = filterIntraMode != Av1FilterIntraMode.AllFilterIntraModes;

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
            // Pure horizontal or vertical prediction with its sole required edge missing
            // degenerates to the first perpendicular sample or the normative midpoint offset.
            T value;
            if (needLeft)
            {
                value = topPixelCount > 0 ? aboveNeighbor[0] : T.CreateChecked(baseValue + 1);
            }
            else
            {
                value = leftPixelCount > 0 ? leftNeighbor[0] : T.CreateChecked(baseValue - 1);
            }

            for (int i = 0; i < transformHeight; ++i)
            {
                destination.Slice(i * (int)destinationStride, transformWidth).Fill(value);
            }

            return;
        }

        // Copy the available left and bottom-left samples, then extend the final sample
        // through any unavailable portion required by the selected predictor.
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

            int numLeftPixelsNeeded = transformHeight + (needBottom ? transformWidth : 0);
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
                    leftColumn.Slice(i, numLeftPixelsNeeded - i).Fill(leftColumn[i - 1]);
                }
            }
            else
            {
                if (topPixelCount > 0)
                {
                    leftColumn[..numLeftPixelsNeeded].Fill(aboveNeighbor[0]);
                }
                else
                {
                    leftColumn[..numLeftPixelsNeeded].Fill(T.CreateChecked(baseValue + 1));
                }
            }
        }

        // Prepare the top edge by the same copy-and-extend rule. Unlike the left edge,
        // these samples are contiguous in the reconstructed pixel buffer.
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

            int numTopPixelsNeeded = transformWidth + (needRight ? transformHeight : 0);
            if (topPixelCount > 0)
            {
                aboveNeighbor[..topPixelCount].CopyTo(aboveRow);
                int i = topPixelCount;
                if (topRightPixelCount > 0)
                {
                    Guard.IsTrue(topPixelCount == transformWidth, nameof(topPixelCount), string.Empty);
                    aboveNeighbor.Slice(transformWidth, topRightPixelCount).CopyTo(aboveRow[transformWidth..]);
                    i += topRightPixelCount;
                }

                if (i < numTopPixelsNeeded)
                {
                    aboveRow.Slice(i, numTopPixelsNeeded - i).Fill(aboveRow[i - 1]);
                }
            }
            else
            {
                if (leftPixelCount > 0)
                {
                    aboveRow[..numTopPixelsNeeded].Fill(leftNeighbor[0]);
                }
                else
                {
                    aboveRow[..numTopPixelsNeeded].Fill(T.CreateChecked(baseValue - 1));
                }
            }
        }

        if (needAboveLeft)
        {
            // AV1 synthesizes the shared corner from the closest available edge when only
            // one edge exists, and uses the bit-depth midpoint when neither edge exists.
            ref T aboveLeft = ref Unsafe.Subtract(ref aboveRow[0], 1);
            if (topPixelCount > 0 && leftPixelCount > 0)
            {
                aboveLeft = Unsafe.Subtract(ref aboveNeighbor[0], 1);
            }
            else if (topPixelCount > 0)
            {
                aboveLeft = aboveNeighbor[0];
            }
            else if (leftPixelCount > 0)
            {
                aboveLeft = leftNeighbor[0];
            }
            else
            {
                aboveLeft = T.CreateChecked(baseValue);
            }

            Unsafe.Subtract(ref leftColumn[0], 1) = aboveLeft;
        }

        if (useFilterIntra)
        {
            FilterIntraPredictor(destination, destinationStride, transformSize, aboveRow, leftColumn, filterIntraMode, bitDepth);
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

                    UpsampleIntraEdge(aboveRow, pixelCount, bitDepth);
                }

                upsampleLeft = UseIntraEdgeUpsample(transformHeight, transformWidth, angle - 180, filterType);
                if (needLeft && upsampleLeft)
                {
                    int pixelCount = transformHeight + (needBottom ? transformWidth : 0);

                    UpsampleIntraEdge(leftColumn, pixelCount, bitDepth);
                }
            }

            DirectionalPredictor(destination, destinationStride, transformSize, aboveRow, leftColumn, upsampleAbove, upsampleLeft, angle, bitDepth);
            return;
        }

        if (mode == Av1PredictionMode.DC)
        {
            DcPredictor(leftPixelCount > 0, topPixelCount > 0, transformSize, destination, destinationStride, aboveRow, leftColumn, bitDepth);
        }
        else
        {
            GeneralPredictor(mode, transformSize, destination, destinationStride, aboveRow, leftColumn);
        }
    }

    /// <summary>
    /// Dispatches DC prediction to the 8-bit or high-bit-depth implementation.
    /// </summary>
    /// <typeparam name="T">The byte or 16-bit sample type.</typeparam>
    /// <param name="hasLeft">A value indicating whether reconstructed left samples are available.</param>
    /// <param name="hasAbove">A value indicating whether reconstructed top samples are available.</param>
    /// <param name="transformSize">The dimensions of the prediction block.</param>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The prepared top reference samples.</param>
    /// <param name="left">The prepared left reference samples.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    private static void DcPredictor<T>(bool hasLeft, bool hasAbove, Av1TransformSize transformSize, Span<T> destination, nuint destinationStride, Span<T> above, Span<T> left, int bitDepth)
        where T : unmanaged
    {
        // DecodeCore is reachable only through byte and short overloads, so this type
        // dispatch permits shared reference preparation without boxing or allocating.
        if (typeof(T) == typeof(byte))
        {
            Av1PredictorFactory.DcPredictor(
                hasLeft,
                hasAbove,
                transformSize,
                MemoryMarshal.Cast<T, byte>(destination),
                destinationStride,
                MemoryMarshal.Cast<T, byte>(above),
                MemoryMarshal.Cast<T, byte>(left));
        }
        else
        {
            Av1PredictorFactory.DcPredictor(
                hasLeft,
                hasAbove,
                transformSize,
                MemoryMarshal.Cast<T, short>(destination),
                destinationStride,
                MemoryMarshal.Cast<T, short>(above),
                MemoryMarshal.Cast<T, short>(left),
                bitDepth);
        }
    }

    /// <summary>
    /// Dispatches nondirectional prediction to the 8-bit or high-bit-depth implementation.
    /// </summary>
    /// <typeparam name="T">The byte or 16-bit sample type.</typeparam>
    /// <param name="mode">The nondirectional prediction mode to apply.</param>
    /// <param name="transformSize">The dimensions of the prediction block.</param>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The prepared top reference samples.</param>
    /// <param name="left">The prepared left reference samples.</param>
    private static void GeneralPredictor<T>(Av1PredictionMode mode, Av1TransformSize transformSize, Span<T> destination, nuint destinationStride, Span<T> above, Span<T> left)
        where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            Av1PredictorFactory.GeneralPredictor(
                mode,
                transformSize,
                MemoryMarshal.Cast<T, byte>(destination),
                destinationStride,
                MemoryMarshal.Cast<T, byte>(above),
                MemoryMarshal.Cast<T, byte>(left));
        }
        else
        {
            Av1PredictorFactory.GeneralPredictor(
                mode,
                transformSize,
                MemoryMarshal.Cast<T, short>(destination),
                destinationStride,
                MemoryMarshal.Cast<T, short>(above),
                MemoryMarshal.Cast<T, short>(left));
        }
    }

    /// <summary>
    /// Dispatches directional prediction to the 8-bit or high-bit-depth implementation.
    /// </summary>
    /// <typeparam name="T">The byte or 16-bit sample type.</typeparam>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="transformSize">The dimensions of the prediction block.</param>
    /// <param name="above">The prepared top reference samples.</param>
    /// <param name="left">The prepared left reference samples.</param>
    /// <param name="upsampleAbove">A value indicating whether the top edge was upsampled.</param>
    /// <param name="upsampleLeft">A value indicating whether the left edge was upsampled.</param>
    /// <param name="angle">The adjusted prediction angle in degrees.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    private static void DirectionalPredictor<T>(Span<T> destination, nuint destinationStride, Av1TransformSize transformSize, Span<T> above, Span<T> left, bool upsampleAbove, bool upsampleLeft, int angle, int bitDepth)
        where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            Av1PredictorFactory.DirectionalPredictor(
                MemoryMarshal.Cast<T, byte>(destination),
                destinationStride,
                transformSize,
                MemoryMarshal.Cast<T, byte>(above),
                MemoryMarshal.Cast<T, byte>(left),
                upsampleAbove,
                upsampleLeft,
                angle);
        }
        else
        {
            Av1PredictorFactory.DirectionalPredictor(
                MemoryMarshal.Cast<T, short>(destination),
                destinationStride,
                transformSize,
                MemoryMarshal.Cast<T, short>(above),
                MemoryMarshal.Cast<T, short>(left),
                upsampleAbove,
                upsampleLeft,
                angle,
                bitDepth);
        }
    }

    /// <summary>
    /// Dispatches filter intra prediction to the 8-bit or high-bit-depth implementation.
    /// </summary>
    /// <typeparam name="T">The byte or 16-bit sample type.</typeparam>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="transformSize">The dimensions of the prediction block.</param>
    /// <param name="above">The prepared top reference samples.</param>
    /// <param name="left">The prepared left reference samples.</param>
    /// <param name="mode">The filter intra mode whose coefficient set is applied.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    private static void FilterIntraPredictor<T>(Span<T> destination, nuint destinationStride, Av1TransformSize transformSize, Span<T> above, Span<T> left, Av1FilterIntraMode mode, int bitDepth)
        where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            Av1PredictorFactory.FilterIntraPredictor(
                MemoryMarshal.Cast<T, byte>(destination),
                destinationStride,
                transformSize,
                MemoryMarshal.Cast<T, byte>(above),
                MemoryMarshal.Cast<T, byte>(left),
                mode);
        }
        else
        {
            Av1PredictorFactory.FilterIntraPredictor(
                MemoryMarshal.Cast<T, short>(destination),
                destinationStride,
                transformSize,
                MemoryMarshal.Cast<T, short>(above),
                MemoryMarshal.Cast<T, short>(left),
                mode,
                bitDepth);
        }
    }

    /// <summary>
    /// Inserts half-sample positions into a prepared intra-prediction edge.
    /// </summary>
    /// <typeparam name="T">The 8-bit or high-bit-depth sample type.</typeparam>
    /// <param name="buffer">The edge buffer, including writable prefix storage at indices -2 and -1.</param>
    /// <param name="count">The number of original edge samples to upsample.</param>
    /// <param name="bitDepth">The number of bits used to clamp interpolated samples.</param>
    private static void UpsampleIntraEdge<T>(Span<T> buffer, int count, int bitDepth)
        where T : unmanaged, IBinaryInteger<T>
    {
        // TODO: Consider creating SIMD version
        DebugGuard.MustBeLessThanOrEqualTo(count, MaxUpsampleSize, nameof(count));

        Span<T> input = stackalloc T[MaxUpsampleSize + 3];
        T beforeBuffer = Unsafe.Subtract(ref buffer[0], 1);

        // Duplicate both endpoints so the four-tap interpolation kernel can run at
        // the edge without a separate boundary branch for its outer samples.
        input[0] = beforeBuffer;
        input[1] = beforeBuffer;
        for (int i = 0; i < count; i++)
        {
            input[i + 2] = buffer[i];
        }

        input[count + 2] = buffer[count - 1];

        Unsafe.Subtract(ref buffer[0], 2) = input[0];
        ref T output = ref buffer[0];
        for (int i = 0; i < count; i++)
        {
            int s = -int.CreateChecked(input[i]) + (9 * int.CreateChecked(input[i + 1])) + (9 * int.CreateChecked(input[i + 2])) - int.CreateChecked(input[i + 3]);
            s = Av1Math.Clamp((s + 8) >> 4, 0, (1 << bitDepth) - 1);

            // The AOM edge buffer reserves prefix storage for the samples at indices -2 and -1.
            Unsafe.Add(ref output, (2 * i) - 1) = T.CreateChecked(s);
            Unsafe.Add(ref output, 2 * i) = input[i + 2];
        }
    }

    /// <summary>
    /// Determines whether AV1 intra-edge upsampling applies to a directional prediction edge.
    /// </summary>
    /// <param name="width">The edge's primary block dimension.</param>
    /// <param name="height">The edge's secondary block dimension.</param>
    /// <param name="delta">The prediction angle relative to the edge's cardinal direction.</param>
    /// <param name="filterType">A value indicating whether a neighboring smooth mode selects the alternate thresholds.</param>
    /// <returns><see langword="true"/> when the edge must be upsampled; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Corresponds to <c>svt_aom_use_intra_edge_upsample</c> in SVT-AV1.</remarks>
    private static bool UseIntraEdgeUpsample(int width, int height, int delta, bool filterType)
    {
        int d = Math.Abs(delta);
        if (d is <= 0 or >= 40)
        {
            return false;
        }

        int widthHeight = width + height;
        return filterType ? (widthHeight <= 8) : (widthHeight <= 16);
    }

    /// <summary>
    /// Applies the AV1 intra-edge smoothing kernel at the requested strength.
    /// </summary>
    /// <typeparam name="T">The 8-bit or high-bit-depth sample type.</typeparam>
    /// <param name="buffer">A reference to the first edge sample to filter.</param>
    /// <param name="count">The number of edge samples.</param>
    /// <param name="strength">The AV1 filter-strength index from zero through three.</param>
    /// <remarks>Corresponds to <c>svt_av1_filter_intra_edge_c</c> in SVT-AV1.</remarks>
    private static void FilterIntraEdge<T>(ref T buffer, int count, int strength)
        where T : unmanaged, IBinaryInteger<T>
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
        Span<T> edge = stackalloc T[129];

        // Filtering must read the original edge throughout the convolution, so retain
        // a scratch copy rather than feeding earlier filtered samples into later outputs.
        MemoryMarshal.CreateSpan(ref buffer, count).CopyTo(edge);
        for (int i = 1; i < count; i++)
        {
            int s = 0;
            for (int j = 0; j < 5; j++)
            {
                int k = i - 2 + j;
                k = (k < 0) ? 0 : k;
                k = (k > count - 1) ? count - 1 : k;
                s += int.CreateChecked(edge[k]) * kernel[filt][j];
            }

            s = (s + 8) >> 4;
            Unsafe.Add(ref buffer, i) = T.CreateChecked(s);
        }
    }

    /// <summary>
    /// Selects the AV1 intra-edge filter strength for the block dimensions and prediction angle.
    /// </summary>
    /// <param name="width">The edge's primary block dimension.</param>
    /// <param name="height">The edge's secondary block dimension.</param>
    /// <param name="delta">The prediction angle relative to the edge's cardinal direction.</param>
    /// <param name="filterType">A value indicating whether a neighboring smooth mode selects the alternate thresholds.</param>
    /// <returns>The filter strength from zero for no filtering through three for the strongest kernel.</returns>
    /// <remarks>Corresponds to <c>svt_aom_intra_edge_filter_strength</c> in SVT-AV1.</remarks>
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

    /// <summary>
    /// Smooths the shared top-left reference sample where the prepared top and left edges meet.
    /// </summary>
    /// <typeparam name="T">The 8-bit or high-bit-depth sample type.</typeparam>
    /// <param name="above">The prepared top edge with writable top-left prefix storage.</param>
    /// <param name="left">The prepared left edge with writable top-left prefix storage.</param>
    private static void FilterIntraEdgeCorner<T>(Span<T> above, Span<T> left)
        where T : unmanaged, IBinaryInteger<T>
    {
        int[] kernel = [5, 6, 5];

        ref T aboveRef = ref above[0];
        ref T leftRef = ref left[0];
        ref T abovePreviousRef = ref Unsafe.Subtract(ref aboveRef, 1);
        ref T leftPreviousRef = ref Unsafe.Subtract(ref leftRef, 1);
        int s = (int.CreateChecked(leftRef) * kernel[0]) + (int.CreateChecked(abovePreviousRef) * kernel[1]) + (int.CreateChecked(aboveRef) * kernel[2]);
        s = (s + 8) >> 4;

        // Both edge spans reserve their own prefix location for the same logical corner,
        // so keep the two scratch representations synchronized after filtering.
        abovePreviousRef = T.CreateChecked(s);
        leftPreviousRef = T.CreateChecked(s);
    }

    /// <summary>
    /// Determines the directional edge-filter threshold class from neighboring prediction modes.
    /// </summary>
    /// <param name="partitionInfo">The decoded partition and neighboring mode state.</param>
    /// <param name="plane">The color plane whose neighbors are inspected.</param>
    /// <returns><see langword="true"/> when either relevant neighbor uses a smooth mode; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Determines whether a block uses any AV1 smooth intra prediction mode on the requested plane.
    /// </summary>
    /// <param name="modeInfo">The neighboring block's decoded mode state.</param>
    /// <param name="plane">The luma or chroma plane class whose mode is inspected.</param>
    /// <returns><see langword="true"/> for smooth, smooth-horizontal, or smooth-vertical prediction; otherwise, <see langword="false"/>.</returns>
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
