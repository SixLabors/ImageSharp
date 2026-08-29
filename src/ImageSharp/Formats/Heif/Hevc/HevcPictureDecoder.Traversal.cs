// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Implements slice, coding-tree, and coding-unit traversal.
/// </content>
internal sealed partial class HevcPictureDecoder
{
    /// <summary>
    /// Decodes one ordered slice segment and returns the next tile-scan coding-tree-block address.
    /// </summary>
    /// <param name="slice">The current independent or dependent slice segment.</param>
    /// <param name="independentSlice">The independent header governing inherited slice fields.</param>
    /// <param name="independentSliceIndex">The one-based independent-slice index within the selected color plane.</param>
    /// <param name="tileLayout">The picture tile mapping.</param>
    /// <param name="startAddressInTileScan">The first coding-tree block in tile-scan order.</param>
    /// <param name="independentSliceStartAddressInTileScan">The governing independent slice's first coding-tree block in tile-scan order.</param>
    /// <returns>The tile-scan address immediately following the decoded segment.</returns>
    private int DecodeSliceSegment(
        HevcSliceSegmentHeader slice,
        HevcSliceSegmentHeader independentSlice,
        int independentSliceIndex,
        in HevcTileLayout tileLayout,
        int startAddressInTileScan,
        int independentSliceStartAddressInTileScan)
    {
        int sliceQuantizationParameter = independentSlice.QuantizationParameter!.Value;
        int colorPlaneIndex = this.sequenceParameterSet.SeparateColorPlaneFlag ? independentSlice.ColorPlaneId : 0;
        this.lastCodedQuantizationParameter = sliceQuantizationParameter;
        this.currentQuantizationParameter = sliceQuantizationParameter;
        this.currentChromaQuantizationAdjustment = 0;
        this.currentSliceChromaBlueQuantizationOffset = independentSlice.ChromaCbQuantizationParameterOffset;
        this.currentSliceChromaRedQuantizationOffset = independentSlice.ChromaCrQuantizationParameterOffset;
        this.quantizationParameterDeltaPending = this.pictureParameterSet.CodingUnitQuantizationParameterDeltaEnabled;
        this.chromaQuantizationAdjustmentPending = independentSlice.ChromaQuantizationParameterOffsetListEnabled == true;
        int substreamIndex = 0;
        HevcCabacSyntaxReader reader = new(slice.GetEntropySubstream(substreamIndex).Span, sliceQuantizationParameter);
        this.coefficientDecoder.ResetRiceAdaptation();

        int contextOffset = colorPlaneIndex * HevcCabacContexts.ContextCount;
        int riceOffset = colorPlaneIndex * 4;
        int startRasterAddress = tileLayout.GetRasterAddress(startAddressInTileScan);
        tileLayout.GetTilePosition(
            startRasterAddress,
            out int startTileIndex,
            out int startColumnInTile,
            out int startRowInTile,
            out int startTileWidth,
            out _);

        bool startsAtTileOrigin = startColumnInTile == 0 && startRowInTile == 0;
        bool canInheritSliceSegmentContexts = !startsAtTileOrigin
            && (startTileWidth >= 2 || !this.pictureParameterSet.EntropyCodingSynchronizationEnabled);

        // A dependent segment normally resumes the preceding CABAC state. Tile origins and one-CTB-wide WPP
        // rows are initialization boundaries instead, matching the availability rules used by the reference decoder.
        if (slice.DependentSliceSegment
            && canInheritSliceSegmentContexts
            && this.hasSliceSegmentContexts[colorPlaneIndex])
        {
            reader.CopyContextsFrom(this.sliceSegmentContexts.AsSpan(contextOffset, HevcCabacContexts.ContextCount));
            this.coefficientDecoder.CopyRiceAdaptationFrom(this.sliceSegmentRiceAdaptation.AsSpan(riceOffset, 4));
        }

        if (!slice.DependentSliceSegment)
        {
            // An independent slice starts a new prediction region, so an upper-right CTB from the preceding
            // independent slice cannot supply wavefront contexts to its first row.
            this.hasWavefrontContexts[colorPlaneIndex] = false;
        }

        bool startsAtWavefrontRow = this.pictureParameterSet.EntropyCodingSynchronizationEnabled
            && startColumnInTile == 0
            && startRowInTile > 0;

        if (startsAtWavefrontRow
            && startTileWidth > 1
            && this.hasWavefrontContexts[colorPlaneIndex]
            && this.wavefrontContextTileIndices[colorPlaneIndex] == startTileIndex)
        {
            // A dependent segment can begin exactly at a wavefront row boundary. Its first substream still uses
            // the upper-right state captured from the preceding row; no substream transition occurs inside this call.
            reader.CopyContextsFrom(this.wavefrontContexts.AsSpan(contextOffset, HevcCabacContexts.ContextCount));
            this.coefficientDecoder.CopyRiceAdaptationFrom(this.wavefrontRiceAdaptation.AsSpan(riceOffset, 4));
        }

        int codingTreeBlockSize = 1 << this.sequenceParameterSet.CodingTreeBlockLog2;
        int tileScanAddress = startAddressInTileScan;
        bool firstCodingTreeBlock = true;
        while (tileScanAddress < tileLayout.Width * tileLayout.Height)
        {
            int rasterAddress = tileLayout.GetRasterAddress(tileScanAddress);
            tileLayout.GetTilePosition(
                rasterAddress,
                out int tileIndex,
                out int columnInTile,
                out int rowInTile,
                out int tileWidth,
                out int tileHeight);

            bool startsTile = columnInTile == 0 && rowInTile == 0;
            bool startsWavefrontRow = this.pictureParameterSet.EntropyCodingSynchronizationEnabled && columnInTile == 0 && rowInTile > 0;
            if (!firstCodingTreeBlock && (startsTile || startsWavefrontRow))
            {
                if (!reader.ReadTerminate())
                {
                    throw new InvalidImageContentException("The HEVC entropy substream does not terminate at its tile or wavefront boundary.");
                }

                reader.ValidateTerminationAlignment();
                substreamIndex++;
                if (substreamIndex >= slice.EntropySubstreamCount)
                {
                    throw new InvalidImageContentException("The HEVC slice segment has too few entropy entry points.");
                }

                reader = new HevcCabacSyntaxReader(slice.GetEntropySubstream(substreamIndex).Span, sliceQuantizationParameter);
                this.coefficientDecoder.ResetRiceAdaptation();
                this.lastCodedQuantizationParameter = sliceQuantizationParameter;
                if (startsWavefrontRow
                    && tileWidth > 1
                    && this.hasWavefrontContexts[colorPlaneIndex]
                    && this.wavefrontContextTileIndices[colorPlaneIndex] == tileIndex)
                {
                    reader.CopyContextsFrom(this.wavefrontContexts.AsSpan(contextOffset, HevcCabacContexts.ContextCount));
                    this.coefficientDecoder.CopyRiceAdaptationFrom(this.wavefrontRiceAdaptation.AsSpan(riceOffset, 4));
                }
            }

            int ctbX = rasterAddress % tileLayout.Width;
            int ctbY = rasterAddress / tileLayout.Width;
            int x = ctbX * codingTreeBlockSize;
            int y = ctbY * codingTreeBlockSize;
            int regionId = ((independentSliceIndex - 1) * tileLayout.TileCount) + tileIndex + 1;
            HevcPlane regionPlane = this.sequenceParameterSet.SeparateColorPlaneFlag ? (HevcPlane)colorPlaneIndex : HevcPlane.Y;
            HevcLoopFilterRegion loopFilterRegion = new(
                independentSliceStartAddressInTileScan,
                tileIndex,
                independentSlice.LoopFilterAcrossSlicesEnabled == true,
                independentSlice.DeblockingFilterDisabled == true,
                independentSlice.DeblockingFilterBetaOffsetDiv2,
                independentSlice.DeblockingFilterTcOffsetDiv2);

            this.sampleAdaptiveOffsetState.SetLoopFilterRegion(rasterAddress, regionPlane, loopFilterRegion);
            this.DecodeSampleAdaptiveOffset(ref reader, independentSlice, rasterAddress, ctbX, ctbY, regionId);
            this.DecodeCodingTree(
                ref reader,
                x,
                y,
                this.sequenceParameterSet.CodingTreeBlockLog2,
                0,
                regionId,
                colorPlaneIndex);

            // HEVC places end_of_slice_segment_flag after the final coding unit of each complete CTB. Reading it
            // inside the recursive leaf traversal consumes coefficient data whenever a CTB contains multiple CUs.
            bool endOfSliceSegment = reader.ReadTerminate();

            // Wavefront synchronization copies probability and persistent Rice state after the second CTB of each
            // row. The next row starts with those contexts but a newly initialized arithmetic register.
            if (this.pictureParameterSet.EntropyCodingSynchronizationEnabled && columnInTile == 1)
            {
                reader.CopyContextsTo(this.wavefrontContexts.AsSpan(contextOffset, HevcCabacContexts.ContextCount));
                this.coefficientDecoder.CopyRiceAdaptationTo(this.wavefrontRiceAdaptation.AsSpan(riceOffset, 4));
                this.hasWavefrontContexts[colorPlaneIndex] = true;
                this.wavefrontContextTileIndices[colorPlaneIndex] = tileIndex;
            }

            tileScanAddress++;
            firstCodingTreeBlock = false;
            if (endOfSliceSegment)
            {
                reader.ValidateTerminationAlignment();
                if (substreamIndex + 1 != slice.EntropySubstreamCount)
                {
                    throw new InvalidImageContentException("The HEVC slice segment has unused entropy entry points.");
                }

                reader.CopyContextsTo(this.sliceSegmentContexts.AsSpan(contextOffset, HevcCabacContexts.ContextCount));
                this.coefficientDecoder.CopyRiceAdaptationTo(this.sliceSegmentRiceAdaptation.AsSpan(riceOffset, 4));
                this.hasSliceSegmentContexts[colorPlaneIndex] = true;
                return tileScanAddress;
            }

            bool atTileEnd = columnInTile == tileWidth - 1 && rowInTile == tileHeight - 1;
            bool atWavefrontRowEnd = this.pictureParameterSet.EntropyCodingSynchronizationEnabled && columnInTile == tileWidth - 1;
            if (atTileEnd || atWavefrontRowEnd)
            {
                // A non-final tile or wavefront row has a second terminating bin after the coding-unit end flag.
                // It is consumed when the following loop iteration opens the next bounded entropy substream.
                continue;
            }
        }

        throw new InvalidImageContentException("The HEVC slice segment reaches the picture boundary without termination.");
    }

    /// <summary>
    /// Decodes one coding-tree node in depth-first Z order.
    /// </summary>
    /// <param name="reader">The active entropy-substream reader.</param>
    /// <param name="x">The coding-node left luma coordinate.</param>
    /// <param name="y">The coding-node top luma coordinate.</param>
    /// <param name="log2Size">The base-two logarithm of the coding-node side.</param>
    /// <param name="depth">The coding-tree depth below the coding-tree-block root.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    /// <param name="colorPlaneIndex">The selected separate-color plane, or zero for combined coding.</param>
    private void DecodeCodingTree(
        ref HevcCabacSyntaxReader reader,
        int x,
        int y,
        int log2Size,
        int depth,
        int regionId,
        int colorPlaneIndex)
    {
        int size = 1 << log2Size;
        bool crossesPictureBoundary = x + size > this.sequenceParameterSet.Width || y + size > this.sequenceParameterSet.Height;
        bool canSplit = log2Size > this.sequenceParameterSet.MinCodingBlockLog2;
        HevcCodingTreeState codingTreeState = this.codingTreeStates[colorPlaneIndex];
        bool split = false;
        if (canSplit)
        {
            if (crossesPictureBoundary)
            {
                split = true;
            }
            else
            {
                bool leftAvailable = this.reconstructionState.IsReconstructed((HevcPlane)colorPlaneIndex, x - 1, y, regionId);
                bool aboveAvailable = this.reconstructionState.IsReconstructed((HevcPlane)colorPlaneIndex, x, y - 1, regionId);
                int context = codingTreeState.GetSplitContext(x, y, depth, leftAvailable, aboveAvailable);
                split = reader.ReadSplit(context);
            }
        }

        bool startsQuantizationGroup = depth == this.pictureParameterSet.QuantizationParameterDeltaDepth
            || (!split && depth < this.pictureParameterSet.QuantizationParameterDeltaDepth);
        if (startsQuantizationGroup && this.pictureParameterSet.CodingUnitQuantizationParameterDeltaEnabled)
        {
            // A leaf above the configured QG depth owns one complete quantization group. Waiting for the configured
            // depth would carry the preceding group's coded-delta state into this coding unit and skip required syntax.
            this.BeginQuantizationGroup(x, y, regionId, colorPlaneIndex);
        }

        bool startsChromaQuantizationGroup = depth == this.pictureParameterSet.ChromaQuantizationParameterOffsetDepth
            || (!split && depth < this.pictureParameterSet.ChromaQuantizationParameterOffsetDepth);
        if (startsChromaQuantizationGroup && this.pictureParameterSet.ChromaQuantizationParameterOffsetsCb.Count != 0)
        {
            this.currentChromaQuantizationAdjustment = 0;
            this.chromaQuantizationAdjustmentPending = true;
        }

        if (split)
        {
            int childLog2Size = log2Size - 1;
            int childSize = 1 << childLog2Size;
            for (int child = 0; child < 4; child++)
            {
                int childX = x + ((child & 1) * childSize);
                int childY = y + ((child >> 1) * childSize);
                if (childX >= this.sequenceParameterSet.Width || childY >= this.sequenceParameterSet.Height)
                {
                    continue;
                }

                this.DecodeCodingTree(
                    ref reader,
                    childX,
                    childY,
                    childLog2Size,
                    depth + 1,
                    regionId,
                    colorPlaneIndex);
            }

            return;
        }

        this.DecodeCodingUnit(ref reader, x, y, log2Size, depth, regionId, colorPlaneIndex);
    }

    /// <summary>
    /// Decodes and reconstructs one intra-coded leaf coding unit.
    /// </summary>
    /// <param name="reader">The active entropy-substream reader.</param>
    /// <param name="x">The coding-unit left luma coordinate.</param>
    /// <param name="y">The coding-unit top luma coordinate.</param>
    /// <param name="log2Size">The base-two logarithm of the coding-unit side.</param>
    /// <param name="depth">The coding-tree depth.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    /// <param name="colorPlaneIndex">The selected separate-color plane, or zero for combined coding.</param>
    private void DecodeCodingUnit(
        ref HevcCabacSyntaxReader reader,
        int x,
        int y,
        int log2Size,
        int depth,
        int regionId,
        int colorPlaneIndex)
    {
        bool transquantBypass = this.pictureParameterSet.TransquantizationBypassEnabled && reader.ReadTransquantBypass();
        bool usesNxNPartitions = reader.ReadIntraNxNPartition(log2Size == this.sequenceParameterSet.MinCodingBlockLog2);
        HevcPlane primaryPlane = this.sequenceParameterSet.SeparateColorPlaneFlag ? (HevcPlane)colorPlaneIndex : HevcPlane.Y;
        bool pcm = this.sequenceParameterSet.PcmEnabled
            && !usesNxNPartitions
            && log2Size >= this.sequenceParameterSet.MinPcmCodingBlockLog2
            && log2Size <= this.sequenceParameterSet.MaxPcmCodingBlockLog2
            && reader.ReadPcmFlag();

        if (pcm)
        {
            int size = 1 << log2Size;
            this.deblockingState.MarkBlock(primaryPlane, x, y, size, size);
            this.DecodePcmCodingUnit(ref reader, x, y, log2Size, regionId, colorPlaneIndex);
            reader.RestartAfterPcm();
        }
        else
        {
            HevcIntraPredictionState predictionState = this.intraPredictionStates[colorPlaneIndex];
            HevcPlane boundaryPlane = this.sequenceParameterSet.SeparateColorPlaneFlag ? (HevcPlane)colorPlaneIndex : HevcPlane.Y;
            bool leftAvailable = this.reconstructionState.IsReconstructed(boundaryPlane, x - 1, y, regionId);
            bool aboveAvailable = this.reconstructionState.IsReconstructed(boundaryPlane, x, y - 1, regionId);
            predictionState.DecodeLumaModes(ref reader, x, y, log2Size, usesNxNPartitions, leftAvailable, aboveAvailable);
            if (this.sequenceParameterSet.ChromaFormat != 0 && !this.sequenceParameterSet.SeparateColorPlaneFlag)
            {
                predictionState.DecodeChromaModes(ref reader, x, y, log2Size, usesNxNPartitions);
            }

            int minimumTransformLog2 = GetMinimumTransformLog2Size(this.sequenceParameterSet, log2Size, usesNxNPartitions);
            HevcTransformUnitGeometry geometry = HevcTransformUnitGeometry.CreateRoot(
                x,
                y,
                log2Size,
                this.sequenceParameterSet.ChromaFormat,
                this.sequenceParameterSet.SeparateColorPlaneFlag,
                colorPlaneIndex);

            this.DecodeTransformTree(
                ref reader,
                in geometry,
                0,
                minimumTransformLog2,
                usesNxNPartitions,
                transquantBypass,
                regionId,
                colorPlaneIndex,
                default,
                default);
        }

        this.codingTreeStates[colorPlaneIndex].SetCodingUnit(
            x,
            y,
            log2Size,
            depth,
            this.currentQuantizationParameter,
            transquantBypass,
            pcm);

        this.lastCodedQuantizationParameter = this.currentQuantizationParameter;
    }

    /// <summary>
    /// Begins one luma quantization group using available spatial predictors.
    /// </summary>
    /// <param name="x">The quantization-group left luma coordinate.</param>
    /// <param name="y">The quantization-group top luma coordinate.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    /// <param name="colorPlaneIndex">The selected separate-color plane, or zero for combined coding.</param>
    private void BeginQuantizationGroup(int x, int y, int regionId, int colorPlaneIndex)
    {
        HevcPlane plane = this.sequenceParameterSet.SeparateColorPlaneFlag ? (HevcPlane)colorPlaneIndex : HevcPlane.Y;
        int codingTreeBlockMask = (1 << this.sequenceParameterSet.CodingTreeBlockLog2) - 1;

        // QP prediction neighbours are confined to the current CTB. This differs from intra sample availability,
        // which may legitimately use reconstructed samples across the same left or upper CTB boundary.
        bool leftAvailable = (x & codingTreeBlockMask) != 0 && this.reconstructionState.IsReconstructed(plane, x - 1, y, regionId);
        bool aboveAvailable = (y & codingTreeBlockMask) != 0 && this.reconstructionState.IsReconstructed(plane, x, y - 1, regionId);
        int fallback = this.lastCodedQuantizationParameter;
        HevcCodingTreeState codingTreeState = this.codingTreeStates[colorPlaneIndex];
        int left = leftAvailable ? codingTreeState.GetQuantizationParameter(x - 1, y) : fallback;
        int above = aboveAvailable ? codingTreeState.GetQuantizationParameter(x, y - 1) : fallback;
        this.currentQuantizationParameter = (left + above + 1) >> 1;
        this.quantizationParameterDeltaPending = true;
    }

    /// <summary>
    /// Derives the smallest luma transform permitted within one intra coding unit.
    /// </summary>
    /// <param name="sequenceParameterSet">The transform hierarchy limits.</param>
    /// <param name="codingUnitLog2Size">The base-two logarithm of the coding-unit side.</param>
    /// <param name="usesNxNPartitions">Whether the coding unit has four luma prediction partitions.</param>
    /// <returns>The minimum luma transform side as a base-two logarithm.</returns>
    private static int GetMinimumTransformLog2Size(
        HevcSequenceParameterSet sequenceParameterSet,
        int codingUnitLog2Size,
        bool usesNxNPartitions)
    {
        int hierarchyReduction = sequenceParameterSet.MaxTransformHierarchyDepthIntra - 1 + (usesNxNPartitions ? 1 : 0);
        int minimum = codingUnitLog2Size < sequenceParameterSet.MinTransformBlockLog2 + hierarchyReduction
            ? sequenceParameterSet.MinTransformBlockLog2
            : codingUnitLog2Size - hierarchyReduction;

        return Math.Min(minimum, sequenceParameterSet.MaxTransformBlockLog2);
    }
}
