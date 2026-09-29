// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Encodes one range-coded AV1 tile payload.
/// </summary>
internal readonly struct Av1TileEncoder : IAv1TileWriter
{
    private readonly ReadOnlyMemory<byte> tileData;
    private readonly Av1PictureControlSet picture;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for eight-bit samples.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
    {
        this.picture = picture;
        this.tileData = Encode<byte, Av1IntraSuperblockEncoder.ByteOperator,
            Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator, Av1CdefEncoder.ByteOperator>(
            writer,
            source,
            default,
            reconstruction,
            picture,
            coefficientBuffer,
            new Av1EncoderTileWorkspace(picture.Parent.FrameHeader, superblockWorkspace),
            blockWorkspace);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for an eight-bit inter frame.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<byte> source,
        ReadOnlyMemory<Av1EncoderFrame<byte>> references,
        Av1EncoderFrame<byte> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
    {
        this.picture = picture;
        this.tileData = Encode<byte, Av1IntraSuperblockEncoder.ByteOperator,
            Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator, Av1CdefEncoder.ByteOperator>(
            writer,
            source,
            references,
            reconstruction,
            picture,
            coefficientBuffer,
            new Av1EncoderTileWorkspace(picture.Parent.FrameHeader, superblockWorkspace),
            blockWorkspace);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for an eight-bit inter frame.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<byte> source,
        ReadOnlyMemory<Av1EncoderFrame<byte>> references,
        Av1EncoderFrame<byte> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
    {
        this.picture = picture;
        this.tileData = Encode<byte, Av1IntraSuperblockEncoder.ByteOperator,
            Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator, Av1CdefEncoder.ByteOperator>(
            writer,
            source,
            references,
            reconstruction,
            picture,
            coefficientBuffer,
            tileWorkspace,
            blockWorkspace);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for high-bit-depth samples.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
    {
        this.picture = picture;
        this.tileData = Encode<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
            Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator, Av1CdefEncoder.UInt16Operator>(
            writer,
            source,
            default,
            reconstruction,
            picture,
            coefficientBuffer,
            new Av1EncoderTileWorkspace(picture.Parent.FrameHeader, superblockWorkspace),
            blockWorkspace);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for a high-bit-depth inter frame.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<ushort> source,
        ReadOnlyMemory<Av1EncoderFrame<ushort>> references,
        Av1EncoderFrame<ushort> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
    {
        this.picture = picture;
        this.tileData = Encode<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
            Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator, Av1CdefEncoder.UInt16Operator>(
            writer,
            source,
            references,
            reconstruction,
            picture,
            coefficientBuffer,
            new Av1EncoderTileWorkspace(picture.Parent.FrameHeader, superblockWorkspace),
            blockWorkspace);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for a high-bit-depth inter frame.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<ushort> source,
        ReadOnlyMemory<Av1EncoderFrame<ushort>> references,
        Av1EncoderFrame<ushort> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
    {
        this.picture = picture;
        this.tileData = Encode<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
            Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator, Av1CdefEncoder.UInt16Operator>(
            writer,
            source,
            references,
            reconstruction,
            picture,
            coefficientBuffer,
            tileWorkspace,
            blockWorkspace);
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> GetTileData(int tileNum)
    {
        int offset = this.picture.TileDataOffsets.Span[tileNum];
        int length = this.picture.TileDataLengths.Span[tileNum];
        return this.tileData.Span.Slice(offset, length);
    }

    /// <summary>
    /// Determines whether any coded block copies from the current frame.
    /// </summary>
    /// <param name="picture">The completed frame decisions.</param>
    /// <returns><see langword="true"/> when at least one block uses intra block copy.</returns>
    private static bool UsesIntraBlockCopy(Av1PictureControlSet picture)
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ReadOnlySpan<int> grid = picture.ModeInfoGrid.Span;
        ReadOnlySpan<Av1MacroBlockModeInfo> allocation = picture.ModeInfoAllocation.Span;
        for (int row = 0; row < header.ModeInfoRowCount; row++)
        {
            for (int column = 0; column < header.ModeInfoColumnCount; column++)
            {
                if (allocation[grid[(row * picture.ModeInfoStride) + column]].Block.UseIntraBlockCopy)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Records the display distance of each enabled reference, and keeps the best-ranked references out of the
    /// block-level single-reference pruning. A reference ranks by its distance plus its base quantizer index.
    /// Reference: set_rel_frame_dist() and setup_keep_ref_frame_mask().
    /// </summary>
    /// <param name="parent">The frame state that receives the distances and the mask.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="blockWorkspace">The workspace with the reference slot history.</param>
    private static void SetKeepSingleReferenceMask(
        Av1PictureParentControlSet parent,
        ObuFrameHeader frameHeader,
        Av1EncoderBlockWorkspace blockWorkspace)
    {
        Span<int> scores = stackalloc int[Av1Constants.ReferenceFrameCount - 1];
        Span<int> order = stackalloc int[Av1Constants.ReferenceFrameCount - 1];
        for (int index = 0; index < scores.Length; index++)
        {
            Av1ReferenceFrameType referenceType = (Av1ReferenceFrameType)(index + (int)Av1ReferenceFrameType.Last);
            scores[index] = int.MaxValue;
            order[index] = index;
            if ((parent.AvailableReferenceMask & (1 << (int)referenceType)) == 0)
            {
                continue;
            }

            int slot = (int)frameHeader.GetReferenceFrameIndices()[index];
            int distance = blockWorkspace.ReferenceFrameNumbers[slot] - blockWorkspace.EncodedFrameCount;
            parent.ReferenceDistances[(int)referenceType] = distance;
            scores[index] = Math.Abs(distance) + blockWorkspace.ReferenceBaseQIndices[slot];
        }

        // Ascending scores; equal scores keep the reference order.
        for (int i = 1; i < order.Length; i++)
        {
            int current = order[i];
            int j = i - 1;
            while (j >= 0 && scores[order[j]] > scores[current])
            {
                order[j + 1] = order[j];
                j--;
            }

            order[j + 1] = current;
        }

        ReadOnlySpan<int> keptCounts = [7, 5, 3, 0, 0];
        int kept = keptCounts[parent.SpeedSettings.GetPruneSingleReferenceLevel(parent.FrameUpdateType)];
        int mask = 0;
        for (int i = 0; i < kept; i++)
        {
            mask |= 1 << (order[i] + (int)Av1ReferenceFrameType.Last);
        }

        parent.KeepSingleReferenceMask = mask;

        // The compound pruning keeps the pairs of the best three references at level one, and no pair above it.
        // Reference: the keep_comp_ref_frame_mask of setup_keep_ref_frame_mask().
        ReadOnlySpan<int> keptCompoundCounts = [7, 3, 0, 0];
        int keptCompound = keptCompoundCounts[parent.SpeedSettings.CompoundReferencePruningLevel];
        int compoundMask = 0;
        for (int i = 0; i < keptCompound; i++)
        {
            compoundMask |= 1 << (order[i] + (int)Av1ReferenceFrameType.Last);
        }

        parent.KeepCompoundReferenceMask = compoundMask;
    }

    /// <summary>
    /// Determines whether any coded block predicts from two references. Reference: compound_ref_used_flag,
    /// which the encoder sets while it encodes each block.
    /// </summary>
    /// <param name="picture">The completed frame decisions.</param>
    /// <returns><see langword="true"/> when at least one block uses a compound reference.</returns>
    private static bool UsesCompoundReference(Av1PictureControlSet picture)
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ReadOnlySpan<int> grid = picture.ModeInfoGrid.Span;
        ReadOnlySpan<Av1MacroBlockModeInfo> allocation = picture.ModeInfoAllocation.Span;
        for (int row = 0; row < header.ModeInfoRowCount; row++)
        {
            for (int column = 0; column < header.ModeInfoColumnCount; column++)
            {
                if (allocation[grid[(row * picture.ModeInfoStride) + column]].Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether any coded block uses skip mode. Reference: skip_mode_used_flag.
    /// </summary>
    /// <param name="picture">The completed frame decisions.</param>
    /// <returns><see langword="true"/> when at least one block uses skip mode.</returns>
    private static bool UsesSkipMode(Av1PictureControlSet picture)
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ReadOnlySpan<int> grid = picture.ModeInfoGrid.Span;
        ReadOnlySpan<Av1MacroBlockModeInfo> allocation = picture.ModeInfoAllocation.Span;
        for (int row = 0; row < header.ModeInfoRowCount; row++)
        {
            for (int column = 0; column < header.ModeInfoColumnCount; column++)
            {
                if (allocation[grid[(row * picture.ModeInfoStride) + column]].Block.SkipMode)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Signals the one interpolation filter at frame level when every inter block uses it in both directions.
    /// Reference: fix_interp_filter(), which reads the switchable_interp counts that update_filter_type_count()
    /// gathers for every inter block while the frame filter is SWITCHABLE.
    /// </summary>
    /// <param name="picture">The completed frame decisions.</param>
    private static void FixInterpolationFilter(Av1PictureControlSet picture)
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        if (header.InterpolationFilter != Av1InterpolationFilter.Switchable)
        {
            return;
        }

        ReadOnlySpan<int> grid = picture.ModeInfoGrid.Span;
        ReadOnlySpan<Av1MacroBlockModeInfo> allocation = picture.ModeInfoAllocation.Span;
        uint used = 0;
        for (int row = 0; row < header.ModeInfoRowCount; row++)
        {
            for (int column = 0; column < header.ModeInfoColumnCount; column++)
            {
                ref readonly Av1EncoderBlockModeInfo mode = ref allocation[grid[(row * picture.ModeInfoStride) + column]].Block;
                if (mode.ReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    used |= (1U << (int)mode.HorizontalInterpolationFilter) | (1U << (int)mode.VerticalInterpolationFilter);
                }
            }
        }

        if (used != 0 && (used & (used - 1)) == 0)
        {
            header.InterpolationFilter = (Av1InterpolationFilter)BitOperations.TrailingZeroCount(used);
        }
    }

    /// <summary>
    /// Determines whether any coded block uses a transform smaller than the largest its size permits, the
    /// condition under which the reference increments <c>txb_split_count</c> while it encodes each block.
    /// </summary>
    /// <param name="picture">The completed frame decisions.</param>
    /// <returns><see langword="true"/> when at least one transform is split.</returns>
    private static bool HasTransformSplit(Av1PictureControlSet picture)
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ReadOnlySpan<int> grid = picture.ModeInfoGrid.Span;
        ReadOnlySpan<Av1MacroBlockModeInfo> allocation = picture.ModeInfoAllocation.Span;
        bool selectable = header.TransformMode == Av1TransformMode.Select;

        // An intra block has one transform size, and an inter block's transform tree has a size per 4x4 cell,
        // so visiting every cell finds each split once or more.
        for (int row = 0; row < header.ModeInfoRowCount; row++)
        {
            for (int column = 0; column < header.ModeInfoColumnCount; column++)
            {
                ref readonly Av1EncoderBlockModeInfo mode = ref allocation[grid[(row * picture.ModeInfoStride) + column]].Block;
                Av1TransformSize largest = mode.BlockSize.GetMaximumTransformSize();
                bool lossless = header.LosslessArray[mode.SegmentId];
                bool inter = mode.ReferenceFrame > Av1ReferenceFrameType.Intra || mode.UseIntraBlockCopy;
                Av1TransformSize size;
                if (inter)
                {
                    // A skipped inter block uses the largest size, or 4x4 when lossless. Otherwise the
                    // transform tree splits wherever the cell's size is below the largest.
                    if (mode.Skip || !selectable || lossless || mode.BlockSize == Av1BlockSize.Block4x4)
                    {
                        size = lossless ? Av1TransformSize.Size4x4 : largest;
                    }
                    else
                    {
                        int blockRow = row & (mode.BlockSize.Get4x4HighCount() - 1);
                        int blockColumn = column & (mode.BlockSize.Get4x4WideCount() - 1);
                        size = mode.InterTransformSizes[mode.GetInterTransformSizeIndex(blockRow, blockColumn)];
                    }
                }
                else
                {
                    size = mode.TransformSize;
                }

                if (size != largest)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Establishes the frame-level decision state that every block decision of one picture reads.
    /// </summary>
    /// <remarks>
    /// Block decisions assume this state: speed settings for the update role that the caller stores in
    /// <see cref="Av1PictureParentControlSet.FrameUpdateType"/>, inter mode thresholds, transform-type and
    /// interpolation statistics, nearest references, motion search settings, and cleared restoration choices.
    /// The tile encoder calls it once before analysis. A caller that drives
    /// <see cref="Av1IntraSuperblockEncoder.ModeDecision{TSample, TOperator}"/> directly must call it first.
    /// </remarks>
    /// <param name="picture">The picture whose parent state is prepared.</param>
    /// <param name="sourceSize">The visible source dimensions.</param>
    /// <param name="blockWorkspace">The workspace that owns the frame statistics and thresholds.</param>
    internal static void PrepareFrame(Av1PictureControlSet picture, Size sourceSize, Av1EncoderBlockWorkspace blockWorkspace)
    {
        Av1PictureParentControlSet parent = picture.Parent;
        ObuFrameHeader frameHeader = parent.FrameHeader;
        parent.SpeedSettings = new Av1EncoderSpeedSettings(
            parent.EncodingSpeed,
            picture.Sequence.SequenceHeader.IsStillPicture,
            frameHeader.IsIntra,
            parent.FrameUpdateType,
            frameHeader.QuantizationParameters.BaseQIndex,
            sourceSize,
            frameHeader.AllowScreenContentTools);

        // Distortion stops at the coded boundary, or at the frame edge when the picture pads its border.
        // Reference: set_pixels_to_frame_edge() with cpi->do_border_pad.
        ObuColorConfig colorConfig = picture.Sequence.SequenceHeader.ColorConfig;
        blockWorkspace.BorderPad = parent.BorderPad;
        blockWorkspace.LumaVisibleBoundary = parent.GetVisibleBoundary(0, 0);
        blockWorkspace.ChromaVisibleBoundary = parent.GetVisibleBoundary(
            colorConfig.SubSamplingX ? 1 : 0, colorConfig.SubSamplingY ? 1 : 0);

        if (!frameHeader.IsIntra)
        {
            // DC steps are represented at four times sample precision. Normalize high-bit-depth
            // steps before applying the nonlinear quantizer scale; retain truncation before the floor.
            Av1BitDepth bitDepth = picture.Sequence.SequenceHeader.ColorConfig.BitDepth;
            ObuQuantizationParameters quantization = frameHeader.QuantizationParameters;
            double step = Av1QuantizationLookup.GetDcQuant(quantization.QIndex[0], quantization.DeltaQDc[0], bitDepth) /
                (double)(1 << (2 + (2 * (int)bitDepth)));

            blockWorkspace.ModeThresholdQuantizerFactor = Math.Max((int)(Math.Pow(step, 1.25) * 5.12), 8);

            // Q12 spans 2.5 at quantizer zero to 1 at quantizer 255. Compute once for the frame,
            // retaining the table's nearest-integer rounding before individual candidate comparisons.
            blockWorkspace.ModeThresholdSkipMultiplier = parent.SpeedSettings.PruneSkippableInterModes
                ? 10240 - (((quantization.QIndex[0] * 6144) + 127) / 255) : 4096;
        }

        // Variance Boost signals a superblock quantizer at the resolution the frame quantizer selects, and no loop
        // filter deltas. The flag clears after analysis when no superblock moved. Reference: the delta_q_info setup of
        // encode_frame_internal().
        ObuDeltaParameters deltaQ = frameHeader.DeltaQParameters;
        int baseQIndex = frameHeader.QuantizationParameters.BaseQIndex;
        bool varianceBoost = parent.EncoderOptions.DeltaQMode == Av1DeltaQMode.VarianceBoost;
        deltaQ.IsPresent = varianceBoost && baseQIndex > 0;
        deltaQ.Resolution = varianceBoost ? Av1VarianceBoost.GetDeltaQResolution(baseQIndex) : 1;
        if (parent.EncoderOptions.DeltaQMode == Av1DeltaQMode.Objective)
        {
            deltaQ.Resolution = Av1TplDecisions.ObjectiveDeltaQResolution;
            deltaQ.IsPresent = AllowsObjectiveDeltaQ(picture, blockWorkspace) && baseQIndex > 0;
        }

        // Each coding block derives its rate multiplier from its superblock quantizer whenever the frame codes delta
        // quantizers. Reference: enable_delta_rdmult() without disable_deltaq_for_intl_arfs(), which one-pass
        // coding never enables.
        parent.CodingBlockDeltaRateMultiplier = deltaQ.IsPresent;
        frameHeader.DeltaLoopFilterParameters.IsPresent = false;
        parent.DeltaQUsed = false;

        parent.TransformTypeCounts = blockWorkspace.TransformTypeCounts;
        parent.TransformTypeCounts.Span.Clear();

        // copy_frame_prob_info() runs at a key frame, and at a golden refresh when warped motion is pruned further.
        bool restoreProbabilities = frameHeader.FrameType == ObuFrameType.KeyFrame ||
            (parent.SpeedSettings.ExtraPruneWarped && parent.RefreshesGolden);

        if (restoreProbabilities && parent.SpeedSettings.TransformTypeProbabilityPruning != 0)
        {
            Av1TransformTypeProbabilities.Defaults.CopyTo(blockWorkspace.TransformTypeProbabilities);
        }

        parent.InterpolationCounts = blockWorkspace.InterpolationCounts;
        parent.SelectedInterpolationCounts = blockWorkspace.SelectedInterpolationCounts;
        parent.InterpolationCounts.Span.Clear();
        parent.SelectedInterpolationCounts.Span.Clear();
        Array.Clear(parent.WarpedUsage);
        Array.Clear(parent.ObmcUsage);
        if (restoreProbabilities && parent.SpeedSettings.InterpolationPruningLevel == 2)
        {
            blockWorkspace.InterpolationProbabilities.Fill(512);
        }

        parent.NearestPastReference = Av1ReferenceFrameType.None;
        parent.NearestFutureReference = Av1ReferenceFrameType.None;
        Array.Clear(parent.ReferenceDistances);
        parent.KeepSingleReferenceMask = 0;
        parent.KeepCompoundReferenceMask = 0;
        parent.AllOneSidedReferences = false;
        if (!frameHeader.IsIntra)
        {
            SetKeepSingleReferenceMask(parent, frameHeader, blockWorkspace);

            // Every reference, enabled or not, must precede the frame. Reference: refs_are_one_sided().
            parent.AllOneSidedReferences = true;
            for (int index = 0; index < Av1Constants.ReferenceFrameCount - 1; index++)
            {
                int slot = (int)frameHeader.GetReferenceFrameIndices()[index];
                if (blockWorkspace.ReferenceFrameNumbers[slot] > blockWorkspace.EncodedFrameCount)
                {
                    parent.AllOneSidedReferences = false;
                }
            }
        }

        if (!frameHeader.IsIntra)
        {
            int nearestPastDistance = int.MaxValue;
            int nearestFutureDistance = int.MaxValue;
            for (Av1ReferenceFrameType referenceType = Av1ReferenceFrameType.Last; referenceType <= Av1ReferenceFrameType.Alternate; referenceType++)
            {
                if ((parent.AvailableReferenceMask & (1 << (int)referenceType)) == 0)
                {
                    continue;
                }

                int slot = (int)frameHeader.GetReferenceFrameIndices()[(int)referenceType - (int)Av1ReferenceFrameType.Last];
                int distance = blockWorkspace.ReferenceFrameNumbers[slot] - blockWorkspace.EncodedFrameCount;
                if (distance < 0 && -distance < nearestPastDistance)
                {
                    nearestPastDistance = -distance;
                    parent.NearestPastReference = referenceType;
                }
                else if (distance > 0 && distance < nearestFutureDistance)
                {
                    nearestFutureDistance = distance;
                    parent.NearestFutureReference = referenceType;
                }
            }
        }

        Av1MotionSearchSettings motionSettings = new(
            parent.EncodingSpeed,
            picture.Sequence.SequenceHeader.IsStillPicture,
            sourceSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            parent.SpeedSettings.IsBoosted,
            parent.IsScreenContent);

        parent.MotionSearchSettings = motionSettings;
        int maximumDimension = Math.Max(sourceSize.Width, sourceSize.Height);
        int stepParameter = Av1MotionSearchBase.GetInitialStepParameter(maximumDimension);
        if (frameHeader.IsIntra)
        {
            // A key frame seeds the following inter frame with the complete frame range.
            parent.MaximumMotionVectorMagnitude = maximumDimension;
        }
        else if (motionSettings.AutomaticStepSizeLevel != 0)
        {
            // Shown frames and internal alternate references use the adaptive step. Reference: the use_auto_mv_step
            // test of av1_set_mv_search_params().
            if ((frameHeader.ShowFrame || parent.FrameUpdateType == Av1FrameUpdateType.IntermediateAlternate) &&
                motionSettings.AutomaticStepSizeLevel >= 2 && parent.MaximumMotionVectorMagnitude != -1)
            {
                int range = Math.Min(maximumDimension, 2 * parent.MaximumMotionVectorMagnitude);
                stepParameter = Av1MotionSearchBase.GetInitialStepParameter(range);
            }

            // The packing pass accumulates actual NEWMV magnitudes. Trial candidates and inherited vectors
            // do not contribute; a frame with no written NEWMV leaves a zero maximum for the next frame.
            parent.MaximumMotionVectorMagnitude = 0;
        }

        // Restoration decisions belong to the completed reconstruction. A reused sequence header
        // must not expose the preceding frame's choices during block analysis.
        for (int plane = 0; plane < picture.Sequence.SequenceHeader.ColorConfig.PlaneCount; plane++)
        {
            frameHeader.LoopRestorationParameters.Items[plane].Type = ObuRestorationType.None;
        }

        frameHeader.LoopRestorationParameters.UsesLoopRestoration = false;
        frameHeader.LoopRestorationParameters.UsesChromaLoopRestoration = false;

        // Deblocking levels are also chosen from the completed reconstruction. av1_encode_frame enables the
        // reference and mode deltas from the encoder configuration, whose default is on, and keeps their
        // default values.
        ObuLoopFilterParameters loopFilter = frameHeader.LoopFilterParameters;
        loopFilter.FilterLevel.Clear();
        loopFilter.FilterLevelU = 0;
        loopFilter.FilterLevelV = 0;
        loopFilter.ReferenceDeltaModeEnabled = true;
        parent.MotionSearchStepParameter = stepParameter;
    }

    /// <summary>
    /// Returns whether a frame of the objective delta quantizer mode keeps its delta quantizer syntax: a frame other
    /// than a leaf frame whose objective superblock quantizers lower the estimated rate-distortion cost of the frame.
    /// Without temporal dependency statistics every superblock estimate is zero, so the syntax stays off. The
    /// estimate updates the regularized importance that the coding block rate multipliers divide by. Reference: the
    /// DELTA_Q_OBJECTIVE tests of encode_frame_internal(), with enable_delta_q() and allow_deltaq_mode().
    /// </summary>
    /// <param name="picture">The picture whose parent carries the temporal dependency state.</param>
    /// <param name="blockWorkspace">The workspace that holds the regularized importance.</param>
    /// <returns><see langword="true"/> when the frame codes delta quantizers.</returns>
    private static bool AllowsObjectiveDeltaQ(Av1PictureControlSet picture, Av1EncoderBlockWorkspace blockWorkspace)
    {
        // One-pass coding never takes the pyramid level test of disable_deltaq_for_intl_arfs(), which needs two-pass
        // statistics, so only leaf frames are excluded. Reference: enable_delta_q().
        Av1PictureParentControlSet parent = picture.Parent;
        if (parent.FrameUpdateType == Av1FrameUpdateType.Last || parent.TplFrame is not { } frame)
        {
            return false;
        }

        ObuSequenceHeader sequenceHeader = picture.Sequence.SequenceHeader;
        double regularizedImportance = blockWorkspace.RegularizedImportance;
        bool allowed = Av1TplDecisions.AllowDeltaQ(
            frame,
            1 << (sequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2),
            parent.FrameHeader.QuantizationParameters.BaseQIndex,
            sequenceHeader.ColorConfig.BitDepth,
            parent.TplImportance,
            ref regularizedImportance);

        blockWorkspace.RegularizedImportance = regularizedImportance;
        return allowed;
    }

    private static ReadOnlyMemory<byte> Encode<TSample, TOperator, TVerticalOperator, THorizontalOperator, TCdefOperator>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> references,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        where TVerticalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where THorizontalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where TCdefOperator : struct, Av1CdefEncoder.IEncodingOperator<TSample>
    {
        Av1PictureParentControlSet parent = picture.Parent;
        ObuFrameHeader frameHeader = parent.FrameHeader;
        PrepareFrame(picture, new Size(source.Width, source.Height), blockWorkspace);

        // Reference: the av1_set_mb_ssim_rdmult_scaling() call of encode_frame_to_data_rate().
        parent.SsimRateMultiplierFactors = null;
        if (parent.EncoderOptions.Tuning is Av1Tuning.Ssim or Av1Tuning.Iq)
        {
            Av1IntraSuperblockEncoder.SetSsimRateMultiplierScaling<TSample, TOperator>(picture, source);
        }

        _ = ProcessTiles<TSample, TOperator, Av1SymbolEncoder.SymbolUpdateOperation>(
            writer, source, references, reconstruction, picture, coefficientBuffer, tileWorkspace, blockWorkspace);

        // A frame whose superblocks all kept the frame quantizer codes no delta quantizers. Reference: the deltaq_used
        // test at the end of encode_frame_internal().
        if (frameHeader.DeltaQParameters.IsPresent && !parent.DeltaQUsed)
        {
            frameHeader.DeltaQParameters.IsPresent = false;
        }

        // A frame that allows intra block copy but never selects it stops allowing it, which also leaves the
        // in-loop filters free to run. Reference: the intrabc_used test at the end of encode_frame_internal().
        if (frameHeader.AllowIntraBlockCopy && !UsesIntraBlockCopy(picture))
        {
            frameHeader.AllowIntraBlockCopy = false;
        }

        // When no coded block split its transform, av1_encode_frame signals the largest transforms instead of
        // selecting them per block. The frame parameter update that holds this runs
        // outside realtime mode, and in realtime mode while estimated compound prediction is enabled
        // (use_comp_ref_nonrd).
        Av1EncoderSpeedSettings speedSettings = parent.SpeedSettings;
        bool frameParameterUpdate = !speedSettings.UseEstimatedInterModeDecision || speedSettings.UseEstimatedCompound;
        if (frameHeader.TransformMode == Av1TransformMode.Select &&
            frameParameterUpdate &&
            !HasTransformSplit(picture))
        {
            frameHeader.TransformMode = Av1TransformMode.Largest;
        }

        if (!frameHeader.IsIntra)
        {
            // The same branch of av1_encode_frame codes single references when no block used a compound
            // reference, and then leaves skip mode off when it is not allowed or no block used it.
            ObuSkipModeParameters skipMode = frameHeader.SkipModeParameters;
            if (frameParameterUpdate)
            {
                if (frameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect && !UsesCompoundReference(picture))
                {
                    frameHeader.ReferenceMode = ObuReferenceMode.SingleReference;
                }

                if (frameHeader.ReferenceMode == ObuReferenceMode.SingleReference)
                {
                    skipMode.Derive(picture.Sequence.SequenceHeader.OrderHintInfo, frameHeader);
                    skipMode.SkipModeFlag = false;
                }

                if (skipMode.SkipModeFlag && !UsesSkipMode(picture))
                {
                    skipMode.SkipModeFlag = false;
                }
            }

            // Reference: fix_interp_filter() in av1_finalize_encoded_frame().
            FixInterpolationFilter(picture);
        }

        // loopfilter_frame picks the levels against the source, then filters the frame.
        Av1LoopFilterEncoder.PickFilterLevel<TSample, TVerticalOperator, THorizontalOperator>(
            blockWorkspace.MemoryAllocator, picture, source, reconstruction, blockWorkspace.PreviousLoopFilterLevels);

        Av1LoopFilterEncoder.ApplyFrame<TSample, TVerticalOperator, THorizontalOperator>(picture, reconstruction);
        bool useRestoration = picture.Sequence.SequenceHeader.EnableRestoration && !frameHeader.AllLossless && !frameHeader.AllowIntraBlockCopy;
        if (useRestoration)
        {
            Av1LoopRestorationEncoder.SaveBoundaryRows(picture, reconstruction, blockWorkspace.RestorationBoundary);
        }

        Av1CdefEncoder.ApplyFrame<TSample, TCdefOperator>(blockWorkspace.MemoryAllocator, picture, source, reconstruction);
        if (useRestoration)
        {
            Av1LoopRestorationEncoder.ApplyFrame(
                blockWorkspace,
                picture,
                source,
                reconstruction,
                blockWorkspace.RestorationBoundary,
                writer,
                tileWorkspace.Tile);
        }

        // Analysis retains the selected modes, coefficients, palette tokens, and motion contexts. Packing starts
        // from the same entropy edges and probabilities while the completed frame decisions remain available.
        picture.ResetEntropyContexts();
        parent.LowMotionArea = 0;
        ReadOnlyMemory<byte> encodedTiles = ProcessTiles<TSample, TOperator, Av1SymbolEncoder.SymbolWriteOperation>(
            writer, source, references, reconstruction, picture, coefficientBuffer, tileWorkspace, blockWorkspace);

        if (!frameHeader.IsIntra)
        {
            int percentage = (100 * parent.LowMotionArea) / (frameHeader.ModeInfoRowCount * frameHeader.ModeInfoColumnCount);
            parent.AverageFrameLowMotion = parent.AverageFrameLowMotion == 0
                ? percentage
                : ((3 * parent.AverageFrameLowMotion) + percentage) / 4;

            parent.ReferenceRefreshControl?.AdjustRefresh(parent);
        }

        if (parent.SpeedSettings.TrackTransformTypeProbabilities)
        {
            Av1TransformTypeProbabilities.Update(
                blockWorkspace.TransformTypeProbabilities.Slice(
                    (int)parent.FrameUpdateType * Av1TransformTypeProbabilities.FrameLength, Av1TransformTypeProbabilities.FrameLength),
                parent.TransformTypeCounts.Span);
        }

        if (frameHeader.FrameType != ObuFrameType.KeyFrame && parent.SpeedSettings.InterpolationPruningLevel == 2 &&
            frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable)
        {
            Av1InterpolationProbabilities.Update(
                blockWorkspace.InterpolationProbabilities.Slice(
                    (int)parent.FrameUpdateType * Av1InterpolationProbabilities.FrameLength, Av1InterpolationProbabilities.FrameLength),
                parent.InterpolationCounts.Span);
        }

        for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
        {
            if ((frameHeader.RefreshFrameFlags & (1U << slot)) != 0)
            {
                blockWorkspace.ReferenceFrameNumbers[slot] = blockWorkspace.EncodedFrameCount;
                blockWorkspace.ReferenceBaseQIndices[slot] = frameHeader.QuantizationParameters.BaseQIndex;
            }
        }

        blockWorkspace.EncodedFrameCount++;
        blockWorkspace.FrameNumber = blockWorkspace.EncodedFrameCount;
        blockWorkspace.PreviousFrameRateMultiplier = parent.GetRateMultiplier(
            frameHeader.QuantizationParameters.QIndex[0] + frameHeader.QuantizationParameters.DeltaQDc[0],
            picture.Sequence.SequenceHeader.ColorConfig.BitDepth);

        return encodedTiles;
    }

    private static ReadOnlyMemory<byte> ProcessTiles<TSample, TOperator, TSymbolOperation>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> references,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        where TSymbolOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        ObuFrameHeader frameHeader = picture.Parent.FrameHeader;
        ObuSequenceHeader sequenceHeader = picture.Sequence.SequenceHeader;
        Av1TileInfo tile = tileWorkspace.Tile;
        Av1Superblock superblock = tileWorkspace.Superblock;
        Av1TileWriter.Av1EntropyCodingContext entropyContext = tileWorkspace.EntropyContext;

        int superblockModeInfoSize = sequenceHeader.SuperblockModeInfoSize;
        int superblockShift = sequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        ObuTileGroupHeader tileLayout = frameHeader.TilesInfo;
        Span<int> tileDataOffsets = picture.TileDataOffsets.Span;
        Span<int> tileDataLengths = picture.TileDataLengths.Span;
        Av1MotionSearchSettings.CostUpdateFrequency motionCostUpdate = picture.Parent.MotionSearchSettings.MotionCostUpdate;
        Av1MotionSearchSettings.CostUpdateFrequency modeCostUpdate = Av1MotionSearchSettings.CostUpdateFrequency.Superblock;
        int minimumDimension = Math.Min(source.Width, source.Height);
        if (sequenceHeader.IsStillPicture && picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level9)
        {
            modeCostUpdate = minimumDimension < 2160
                ? Av1MotionSearchSettings.CostUpdateFrequency.Off
                : Av1MotionSearchSettings.CostUpdateFrequency.SuperblockRow;
        }
        else if (!sequenceHeader.IsStillPicture && !picture.Parent.SpeedSettings.IsRealtime &&
            picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level6 && minimumDimension < 720)
        {
            // Only GOOD mode refreshes rates once per superblock row. Real-time usage keeps the default
            // refresh at every superblock. Reference: coeff_cost_upd_level and mode_cost_upd_level in
            // set_good_speed_feature_framesize_dependent(), which set_rt_speed_features does not change.
            modeCostUpdate = Av1MotionSearchSettings.CostUpdateFrequency.SuperblockRow;
        }

        if (!TSymbolOperation.WritesOutput && frameHeader.AllowIntraBlockCopy)
        {
            // Hash the visible source once before reconstruction begins so candidate discovery never depends
            // on coding order and the workspace can be reused as compact bucket links afterward.
            picture.IntraBlockCopySearch.Initialize<TSample, TOperator>(
                source.View.GetPlane(Av1Plane.Y));
        }

        int tileIndex = 0;
        int tileDataEnd = 0;
        for (int tileRow = 0; tileRow < tileLayout.TileRowCount; tileRow++)
        {
            tile.SetTileRow(tileLayout, frameHeader.ModeInfoRowCount, tileRow);
            for (int tileColumn = 0; tileColumn < tileLayout.TileColumnCount; tileColumn++)
            {
                tile.SetTileColumn(tileLayout, frameHeader.ModeInfoColumnCount, tileColumn);

                // Each pass begins every tile from the same frame probabilities. Only the packing pass
                // advances the output offset; the analysis operation does not touch range-coder state.
                writer.Reset(tileDataEnd);
                if (!TSymbolOperation.WritesOutput && !frameHeader.IsIntra)
                {
                    blockWorkspace.InterModeModels.Clear();
                }

                int motionCostRowInterval = 1;
                if (motionCostUpdate == Av1MotionSearchSettings.CostUpdateFrequency.SuperblockRowSet)
                {
                    // Target one update per 256 luma rows, then distribute those updates evenly over the
                    // tile's superblock rows. Two rounded divisions keep short final tiles evenly spaced.
                    int tileHeight = (tile.ModeInfoRowEnd - tile.ModeInfoRowStart) << Av1Constants.ModeInfoSizeLog2;
                    int updateCount = (tileHeight + 255) / 256;
                    int updateSpan = updateCount << sequenceHeader.SuperblockSizeLog2;
                    motionCostRowInterval = (tileHeight + updateSpan - 1) / updateSpan;
                }

                Point firstModeInfoPosition = new(tile.ModeInfoColumnStart, tile.ModeInfoRowStart);
                entropyContext.MacroBlockModeInfo = picture.GetMacroBlockModeInfo(firstModeInfoPosition);
                for (int modeInfoRow = tile.ModeInfoRowStart;
                    modeInfoRow < tile.ModeInfoRowEnd;
                    modeInfoRow += superblockModeInfoSize)
                {
                    if (!TSymbolOperation.WritesOutput && !frameHeader.IsIntra)
                    {
                        // Evidence is shared across this row's block searches, including partition trials.
                        // Every row starts at unity in Q5; packing does not alter search history.
                        blockWorkspace.ModeThresholdFactors.Fill(32);
                    }

                    for (int modeInfoColumn = tile.ModeInfoColumnStart;
                        modeInfoColumn < tile.ModeInfoColumnEnd;
                        modeInfoColumn += superblockModeInfoSize)
                    {
                        int superblockRow = modeInfoRow >> superblockShift;
                        int superblockColumn = modeInfoColumn >> superblockShift;
                        superblock.Index = (superblockRow * coefficientBuffer.SuperblockColumnCount) + superblockColumn;
                        superblock.TileIndex = tileIndex;

                        // Reference: the reset_mb_rd_record() and av1_zero(x->picked_ref_frames_mask) of
                        // init_encode_rd_sb().
                        blockWorkspace.MacroblockRateDistortionRecord.Reset();
                        Array.Clear(blockWorkspace.PickedReferenceFrameMasks);
                        entropyContext.SuperblockOrigin = new Point(
                            modeInfoColumn << Av1Constants.ModeInfoSizeLog2,
                            modeInfoRow << Av1Constants.ModeInfoSizeLog2);

                        if (TSymbolOperation.WritesOutput)
                        {
                            Av1TileWriter.RetainedBlockEncodingHandler blockEncoder = new(picture);
                            Av1TileWriter.WriteSuperblock<TSymbolOperation, Av1TileWriter.RetainedBlockEncodingHandler>(
                                picture,
                                entropyContext,
                                writer,
                                superblock,
                                coefficientBuffer,
                                (ushort)tileIndex,
                                ref blockEncoder);
                        }
                        else
                        {
                            bool firstColumn = modeInfoColumn == tile.ModeInfoColumnStart;
                            bool firstSuperblock = firstColumn && modeInfoRow == tile.ModeInfoRowStart;
                            bool refreshModeCosts = modeCostUpdate == Av1MotionSearchSettings.CostUpdateFrequency.Superblock ||
                                (modeCostUpdate == Av1MotionSearchSettings.CostUpdateFrequency.SuperblockRow && firstColumn);

                            if (!firstSuperblock && !frameHeader.DisableCdfUpdate && refreshModeCosts)
                            {
                                // Reset initialized this tile's first rates. Later boundaries consume only
                                // preceding selected blocks; all candidates within the boundary share rates.
                                writer.RefreshCosts();
                            }

                            if (Entropy.Av1SymbolWriter.DiagnosticSymbolTrace is not null)
                            {
                                System.Text.StringBuilder costLine = new($"COST {modeInfoColumn * 4},{modeInfoRow * 4} skip1");
                                for (int c = 0; c < 13; c++)
                                {
                                    costLine.Append(' ').Append(writer.GetTransformBlockSkipCost(true, Av1TransformSize.Size8x8, c));
                                }

                                costLine.Append(" uv32");
                                for (int c = 0; c < 13; c++)
                                {
                                    costLine.Append(' ').Append(writer.GetTransformBlockSkipCost(true, Av1TransformSize.Size32x32, c));
                                }

                                Entropy.Av1SymbolWriter.DiagnosticSymbolTrace.Add(costLine.ToString());
                                System.Text.StringBuilder txLine = new($"TXCOST {modeInfoColumn * 4},{modeInfoRow * 4}");
                                for (int t = 0; t < 4; t++)
                                {
                                    txLine.Append(' ').Append(writer.GetTransformTypeCost((Av1TransformType)t, Av1TransformSize.Size8x8, frameHeader.UseReducedTransformSet, 200, Av1FilterIntraMode.AllFilterIntraModes, Prediction.Av1PredictionMode.Directional135Degrees, false));
                                }

                                Entropy.Av1SymbolWriter.DiagnosticSymbolTrace.Add(txLine.ToString());
                            }

                            int tileSuperblockRow = (modeInfoRow - tile.ModeInfoRowStart) >> superblockShift;
                            bool refreshMotionCosts = motionCostUpdate == Av1MotionSearchSettings.CostUpdateFrequency.Superblock ||
                                (firstColumn && (tileSuperblockRow % motionCostRowInterval) == 0);

                            if (!frameHeader.IsIntra && (firstSuperblock || (!frameHeader.DisableCdfUpdate && refreshMotionCosts)))
                            {
                                // Initialize from each tile's starting CDF even when adaptation is disabled.
                                // Later updates consume only preceding selected blocks at the configured boundary;
                                // all candidate trials between boundaries share the same cost snapshot.
                                writer.FillMotionVectorCosts(blockWorkspace.GetMotionVectorCosts(frameHeader.MotionVectorPrecision));
                            }

                            if (frameHeader.AllowIntraBlockCopy &&
                                (firstSuperblock || (!sequenceHeader.IsStillPicture && !frameHeader.DisableCdfUpdate)))
                            {
                                // Still images retain their initial displacement rates. Sequence intra frames
                                // refresh at superblock boundaries while entropy coding continues to adapt.
                                writer.FillDisplacementVectorCosts(blockWorkspace.GetDisplacementVectorCosts());
                            }

                            Av1IntraSuperblockEncoder.Prepare(
                                picture,
                                superblock,
                                entropyContext.SuperblockOrigin);

                            Av1IntraSuperblockEncoder.ModeDecision<TSample, TOperator> blockEncoder = new(
                                source,
                                references,
                                reconstruction,
                                picture,
                                superblock,
                                coefficientBuffer,
                                blockWorkspace);

                            Av1TileWriter.WriteSuperblock<
                                TSymbolOperation,
                                Av1IntraSuperblockEncoder.ModeDecision<TSample, TOperator>>(
                                picture,
                                entropyContext,
                                writer,
                                superblock,
                                coefficientBuffer,
                                (ushort)tileIndex,
                                ref blockEncoder);

                            if (!frameHeader.IsIntra && picture.Parent.SpeedSettings.InterModeEstimation == 1 &&
                                tileLayout.TileColumnCount == 1 && tileLayout.TileRowCount == 1)
                            {
                                // Fit only after all partition trials for this superblock have finished.
                                // Every candidate inside the superblock uses the preceding fit.
                                foreach (ref Av1InterModeRateDistortionModel model in blockWorkspace.InterModeModels)
                                {
                                    model.Fit();
                                }
                            }
                        }
                    }
                }

                if (TSymbolOperation.WritesOutput)
                {
                    _ = writer.Exit(out int tileDataLength);
                    tileDataOffsets[tileIndex] = tileDataEnd;
                    tileDataLengths[tileIndex] = tileDataLength;
                    tileDataEnd += tileDataLength;
                }

                tileIndex++;
            }
        }

        return writer.GetOutput(tileDataEnd);
    }
}

/// <summary>
/// Retains the mutable tile, superblock, and entropy cursor graph reused by serial frame encoding.
/// </summary>
internal readonly struct Av1EncoderTileWorkspace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderTileWorkspace"/> struct.
    /// </summary>
    /// <param name="frameHeader">The fixed-geometry frame header defining tile boundaries.</param>
    /// <param name="superblockWorkspace">The retained superblock decision storage.</param>
    public Av1EncoderTileWorkspace(
        ObuFrameHeader frameHeader,
        Av1EncoderSuperblockWorkspace superblockWorkspace)
    {
        this.Tile = new Av1TileInfo(0, 0, frameHeader);
        this.Superblock = new Av1Superblock
        {
            Workspace = superblockWorkspace,
            TileInfo = this.Tile
        };

        this.EntropyContext = new Av1TileWriter.Av1EntropyCodingContext
        {
            MacroBlock = new Av1MacroBlockD { Tile = this.Tile },
            MacroBlockModeInfo = default
        };
    }

    /// <summary>
    /// Gets the mutable tile boundaries selected during raster traversal.
    /// </summary>
    public Av1TileInfo Tile { get; }

    /// <summary>
    /// Gets the mutable superblock cursor connected to the retained decision workspace.
    /// </summary>
    public Av1Superblock Superblock { get; }

    /// <summary>
    /// Gets the mutable entropy cursor shared by successive superblocks.
    /// </summary>
    public Av1TileWriter.Av1EntropyCodingContext EntropyContext { get; }
}
