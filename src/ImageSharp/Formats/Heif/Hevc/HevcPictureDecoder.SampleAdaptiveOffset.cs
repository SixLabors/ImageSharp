// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Implements sample-adaptive-offset syntax decoding and merge resolution.
/// </content>
internal sealed partial class HevcPictureDecoder
{
    /// <summary>
    /// Applies the resolved sample-adaptive offsets to every component after deblocking has completed.
    /// </summary>
    /// <param name="source">The immutable deblocked picture used to classify every sample.</param>
    /// <param name="tileLayout">The picture tile mapping used to derive coding-tree-block boundaries.</param>
    private void ApplySampleAdaptiveOffset(HevcPictureBuffer source, in HevcTileLayout tileLayout)
    {
        int codingTreeBlockSize = 1 << this.sequenceParameterSet.CodingTreeBlockLog2;
        int planeCount = this.sequenceParameterSet.ChromaFormat == 0 ? 1 : 3;
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            HevcPlane plane = (HevcPlane)planeIndex;
            HevcPlane regionPlane = this.sequenceParameterSet.SeparateColorPlaneFlag ? plane : HevcPlane.Y;
            int subsamplingX = this.Picture.GetSubsamplingX(plane);
            int subsamplingY = this.Picture.GetSubsamplingY(plane);
            int blockWidth = codingTreeBlockSize >> subsamplingX;
            int blockHeight = codingTreeBlockSize >> subsamplingY;
            int planeWidth = this.Picture.GetWidth(plane);
            int planeHeight = this.Picture.GetHeight(plane);
            int offsetScaleLog2 = plane == HevcPlane.Y
                ? this.pictureParameterSet.SampleAdaptiveOffsetScaleLumaLog2
                : this.pictureParameterSet.SampleAdaptiveOffsetScaleChromaLog2;

            for (int codingTreeBlockY = 0; codingTreeBlockY < tileLayout.Height; codingTreeBlockY++)
            {
                for (int codingTreeBlockX = 0; codingTreeBlockX < tileLayout.Width; codingTreeBlockX++)
                {
                    int rasterAddress = (codingTreeBlockY * tileLayout.Width) + codingTreeBlockX;
                    HevcSampleAdaptiveOffsetParameters parameters = this.sampleAdaptiveOffsetState.Get(rasterAddress, plane);
                    if (parameters.Type == HevcSampleAdaptiveOffsetType.Off)
                    {
                        continue;
                    }

                    HevcLoopFilterBoundaryAvailability availability = this.sampleAdaptiveOffsetState.GetLoopFilterBoundaryAvailability(
                        rasterAddress,
                        regionPlane,
                        tileLayout.Width,
                        tileLayout.Height,
                        this.pictureParameterSet.LoopFilterAcrossTilesEnabled);

                    int x = codingTreeBlockX * blockWidth;
                    int y = codingTreeBlockY * blockHeight;
                    int width = Math.Min(blockWidth, planeWidth - x);
                    int height = Math.Min(blockHeight, planeHeight - y);

                    // Every classification reads the immutable post-deblocking picture. Later CTBs can therefore never
                    // observe offsets already written by an earlier CTB, including across permitted slice and tile boundaries.
                    HevcSampleAdaptiveOffsetFilter.ApplyBlock(
                        source,
                        this.Picture,
                        plane,
                        x,
                        y,
                        width,
                        height,
                        in parameters,
                        offsetScaleLog2,
                        availability.Left,
                        availability.Right,
                        availability.Above,
                        availability.Below,
                        availability.AboveLeft,
                        availability.AboveRight,
                        availability.BelowLeft,
                        availability.BelowRight);
                }
            }
        }
    }

    /// <summary>
    /// Decodes and resolves the sample-adaptive-offset parameters for one coding-tree block.
    /// </summary>
    /// <param name="reader">The active entropy-substream reader.</param>
    /// <param name="independentSlice">The independent slice governing component enable flags.</param>
    /// <param name="rasterAddress">The coding-tree block's raster-scan address.</param>
    /// <param name="codingTreeBlockX">The horizontal coding-tree-block coordinate.</param>
    /// <param name="codingTreeBlockY">The vertical coding-tree-block coordinate.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    private void DecodeSampleAdaptiveOffset(
        ref HevcCabacSyntaxReader reader,
        HevcSliceSegmentHeader independentSlice,
        int rasterAddress,
        int codingTreeBlockX,
        int codingTreeBlockY,
        int regionId)
    {
        HevcPlane regionPlane = this.sequenceParameterSet.SeparateColorPlaneFlag ? (HevcPlane)independentSlice.ColorPlaneId : HevcPlane.Y;
        bool lumaEnabled = independentSlice.SampleAdaptiveOffsetLumaEnabled == true;
        bool chromaEnabled = independentSlice.SampleAdaptiveOffsetChromaEnabled == true;
        if (!lumaEnabled && !chromaEnabled)
        {
            this.sampleAdaptiveOffsetState.SetRegion(rasterAddress, regionPlane, regionId);
            return;
        }

        int codingTreeBlockWidth = HevcParameterSetSyntax.GetCodingTreeBlockCount(
            this.sequenceParameterSet.Width,
            this.sequenceParameterSet.CodingTreeBlockLog2);

        int leftAddress = rasterAddress - 1;
        bool leftAvailable = codingTreeBlockX > 0 && this.sampleAdaptiveOffsetState.IsInRegion(leftAddress, regionPlane, regionId);
        bool mergeLeft = leftAvailable && reader.ReadSampleAdaptiveOffsetMerge();
        int aboveAddress = rasterAddress - codingTreeBlockWidth;
        bool aboveAvailable = codingTreeBlockY > 0 && this.sampleAdaptiveOffsetState.IsInRegion(aboveAddress, regionPlane, regionId);
        bool mergeAbove = !mergeLeft && aboveAvailable && reader.ReadSampleAdaptiveOffsetMerge();
        if (mergeLeft || mergeAbove)
        {
            int sourceAddress = mergeLeft ? leftAddress : aboveAddress;
            this.CopySampleAdaptiveOffsetParameters(sourceAddress, rasterAddress, lumaEnabled, chromaEnabled, independentSlice.ColorPlaneId);
            this.sampleAdaptiveOffsetState.SetRegion(rasterAddress, regionPlane, regionId);
            return;
        }

        if (this.sequenceParameterSet.SeparateColorPlaneFlag)
        {
            HevcPlane plane = (HevcPlane)independentSlice.ColorPlaneId;
            this.sampleAdaptiveOffsetState.Set(rasterAddress, plane, ReadSampleAdaptiveOffsetParameters(ref reader, this.Picture.GetBitDepth(plane), -1));
        }
        else
        {
            if (lumaEnabled)
            {
                this.sampleAdaptiveOffsetState.Set(
                    rasterAddress,
                    HevcPlane.Y,
                    ReadSampleAdaptiveOffsetParameters(ref reader, this.sequenceParameterSet.BitDepthLuma, -1));
            }

            if (chromaEnabled)
            {
                HevcSampleAdaptiveOffsetParameters chromaBlue = ReadSampleAdaptiveOffsetParameters(
                    ref reader,
                    this.sequenceParameterSet.BitDepthChroma,
                    -1);

                this.sampleAdaptiveOffsetState.Set(rasterAddress, HevcPlane.Cb, chromaBlue);
                this.sampleAdaptiveOffsetState.Set(
                    rasterAddress,
                    HevcPlane.Cr,
                    ReadSampleAdaptiveOffsetParameters(ref reader, this.sequenceParameterSet.BitDepthChroma, (int)chromaBlue.Type));
            }
        }

        this.sampleAdaptiveOffsetState.SetRegion(rasterAddress, regionPlane, regionId);
    }

    /// <summary>
    /// Copies resolved merge-source parameters for the components enabled by the current slice.
    /// </summary>
    /// <param name="sourceAddress">The merge-source coding-tree-block address.</param>
    /// <param name="destinationAddress">The current coding-tree-block address.</param>
    /// <param name="lumaEnabled">Whether the current slice enables luma sample-adaptive offset.</param>
    /// <param name="chromaEnabled">Whether the current slice enables chroma sample-adaptive offset.</param>
    /// <param name="colorPlaneId">The selected separate-color-plane identifier.</param>
    private void CopySampleAdaptiveOffsetParameters(
        int sourceAddress,
        int destinationAddress,
        bool lumaEnabled,
        bool chromaEnabled,
        byte colorPlaneId)
    {
        if (this.sequenceParameterSet.SeparateColorPlaneFlag)
        {
            HevcPlane plane = (HevcPlane)colorPlaneId;
            this.sampleAdaptiveOffsetState.Set(destinationAddress, plane, this.sampleAdaptiveOffsetState.Get(sourceAddress, plane));
            return;
        }

        if (lumaEnabled)
        {
            this.sampleAdaptiveOffsetState.Set(destinationAddress, HevcPlane.Y, this.sampleAdaptiveOffsetState.Get(sourceAddress, HevcPlane.Y));
        }

        if (chromaEnabled)
        {
            this.sampleAdaptiveOffsetState.Set(destinationAddress, HevcPlane.Cb, this.sampleAdaptiveOffsetState.Get(sourceAddress, HevcPlane.Cb));
            this.sampleAdaptiveOffsetState.Set(destinationAddress, HevcPlane.Cr, this.sampleAdaptiveOffsetState.Get(sourceAddress, HevcPlane.Cr));
        }
    }

    /// <summary>
    /// Decodes one component's new or disabled sample-adaptive-offset mode.
    /// </summary>
    /// <param name="reader">The active entropy-substream reader.</param>
    /// <param name="bitDepth">The component sample precision.</param>
    /// <param name="inheritedType">The Cb type inherited by Cr, or negative one when the type is signaled.</param>
    /// <returns>The resolved component parameters.</returns>
    private static HevcSampleAdaptiveOffsetParameters ReadSampleAdaptiveOffsetParameters(
        ref HevcCabacSyntaxReader reader,
        int bitDepth,
        int inheritedType)
    {
        int type = inheritedType >= 0
            ? inheritedType == (int)HevcSampleAdaptiveOffsetType.Off ? 0 : inheritedType == (int)HevcSampleAdaptiveOffsetType.Band ? 1 : 2
            : reader.ReadSampleAdaptiveOffsetType();

        if (type == 0)
        {
            return default;
        }

        int maximumOffset = (1 << (Math.Min(bitDepth, 10) - 5)) - 1;
        int offset0 = reader.ReadSampleAdaptiveOffsetAbsolute(maximumOffset);
        int offset1 = reader.ReadSampleAdaptiveOffsetAbsolute(maximumOffset);
        int offset2 = reader.ReadSampleAdaptiveOffsetAbsolute(maximumOffset);
        int offset3 = reader.ReadSampleAdaptiveOffsetAbsolute(maximumOffset);
        if (type == 1)
        {
            offset0 = ApplySampleAdaptiveOffsetSign(ref reader, offset0);
            offset1 = ApplySampleAdaptiveOffsetSign(ref reader, offset1);
            offset2 = ApplySampleAdaptiveOffsetSign(ref reader, offset2);
            offset3 = ApplySampleAdaptiveOffsetSign(ref reader, offset3);
            return new HevcSampleAdaptiveOffsetParameters(
                HevcSampleAdaptiveOffsetType.Band,
                reader.ReadSampleAdaptiveOffsetBandPosition(),
                offset0,
                offset1,
                offset2,
                offset3,
                0);
        }

        HevcSampleAdaptiveOffsetType edgeType = inheritedType >= 0
            ? (HevcSampleAdaptiveOffsetType)inheritedType
            : (HevcSampleAdaptiveOffsetType)((int)HevcSampleAdaptiveOffsetType.EdgeHorizontal + reader.ReadSampleAdaptiveOffsetEdgeClass());

        return new HevcSampleAdaptiveOffsetParameters(edgeType, 0, offset0, offset1, 0, -offset2, -offset3);
    }

    /// <summary>
    /// Applies an explicitly coded sign to a nonzero band-offset magnitude.
    /// </summary>
    /// <param name="reader">The active entropy-substream reader.</param>
    /// <param name="magnitude">The decoded unsigned magnitude.</param>
    /// <returns>The signed magnitude.</returns>
    private static int ApplySampleAdaptiveOffsetSign(ref HevcCabacSyntaxReader reader, int magnitude)
        => magnitude != 0 && reader.ReadSampleAdaptiveOffsetSign() ? -magnitude : magnitude;
}
