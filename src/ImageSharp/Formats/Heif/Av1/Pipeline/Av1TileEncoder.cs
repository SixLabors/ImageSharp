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
    /// <summary>
    /// The symbol encoder whose tile buffers hold the packed tiles.
    /// </summary>
    private readonly Av1SymbolEncoder writer;

    /// <summary>
    /// The frame coding state that holds the tile lengths.
    /// </summary>
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
        this.writer = writer;
        Encode<byte, Av1IntraSuperblockEncoder.ByteOperator,
            Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator, Av1CdefEncoder.ByteOperator>(
            writer,
            source,
            default,
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
        this.writer = writer;
        Encode<byte, Av1IntraSuperblockEncoder.ByteOperator,
            Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator, Av1CdefEncoder.ByteOperator>(
            writer,
            source,
            references,
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
    /// <param name="searchReferences">
    /// The frames that the motion search reads, indexed by prediction reference identifier: each reference, or its copy resized to the coded frame size.
    /// </param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<byte> source,
        ReadOnlyMemory<Av1EncoderFrame<byte>> references,
        ReadOnlyMemory<Av1EncoderFrame<byte>> searchReferences,
        Av1EncoderFrame<byte> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
    {
        this.picture = picture;
        this.writer = writer;
        Encode<byte, Av1IntraSuperblockEncoder.ByteOperator,
            Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator, Av1CdefEncoder.ByteOperator>(
            writer,
            source,
            references,
            searchReferences,
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
        this.writer = writer;
        Encode<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
            Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator, Av1CdefEncoder.UInt16Operator>(
            writer,
            source,
            default,
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
        this.writer = writer;
        Encode<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
            Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator, Av1CdefEncoder.UInt16Operator>(
            writer,
            source,
            references,
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
    /// <param name="searchReferences">
    /// The frames that the motion search reads, indexed by prediction reference identifier: each reference, or its copy resized to the coded frame size.
    /// </param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<ushort> source,
        ReadOnlyMemory<Av1EncoderFrame<ushort>> references,
        ReadOnlyMemory<Av1EncoderFrame<ushort>> searchReferences,
        Av1EncoderFrame<ushort> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
    {
        this.picture = picture;
        this.writer = writer;
        Encode<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
            Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator, Av1CdefEncoder.UInt16Operator>(
            writer,
            source,
            references,
            searchReferences,
            reconstruction,
            picture,
            coefficientBuffer,
            tileWorkspace,
            blockWorkspace);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for tile data that a packing pass already wrote.
    /// </summary>
    /// <param name="picture">The frame coding state that holds the tile lengths.</param>
    /// <param name="writer">The symbol encoder that holds the tile buffers of the packing pass.</param>
    private Av1TileEncoder(Av1PictureControlSet picture, Av1SymbolEncoder writer)
    {
        this.picture = picture;
        this.writer = writer;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> GetTileData(int tileNum)
        => this.writer.GetTileOutput(tileNum, this.picture.TileDataLengths.Span[tileNum]);

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
    /// Records the display distance of each enabled reference, and keeps the best-ranked references out of the block-level single-reference pruning. A
    /// reference ranks by its distance plus its base quantizer index. The best-ranked references also form the compound pruning mask.
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
        Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        Span<int> referenceFrameNumbers = blockWorkspace.ReferenceFrameNumbers;
        for (int index = 0; index < scores.Length; index++)
        {
            Av1ReferenceFrameType referenceType = (Av1ReferenceFrameType)(index + (int)Av1ReferenceFrameType.Last);
            scores[index] = int.MaxValue;
            order[index] = index;
            if ((parent.AvailableReferenceMask & (1 << (int)referenceType)) == 0)
            {
                continue;
            }

            int slot = (int)referenceFrameIndices[index];
            int distance = referenceFrameNumbers[slot] - blockWorkspace.EncodedFrameCount;
            parent.ReferenceDistances[(int)referenceType] = distance;
            scores[index] = Math.Abs(distance) + blockWorkspace.ReferenceBaseQIndices[slot];
        }

        // Sort by ascending score with an insertion sort. Equal scores keep the reference order.
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

        // Each single-reference pruning level keeps this number of the best-ranked references.
        ReadOnlySpan<int> keptCounts = [7, 5, 3, 0, 0];
        int kept = keptCounts[parent.SpeedSettings.GetPruneSingleReferenceLevel(parent.FrameUpdateType)];
        int mask = 0;
        for (int i = 0; i < kept; i++)
        {
            mask |= 1 << (order[i] + (int)Av1ReferenceFrameType.Last);
        }

        parent.KeepSingleReferenceMask = mask;

        // The compound pruning keeps every pair at level zero, the pairs of the best three references at level one, and no pair above it.
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
    /// Determines whether any coded block predicts from two references.
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
    /// Determines whether any coded block uses skip mode.
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
    /// Signals the one interpolation filter at frame level when every inter block uses it in both directions. Only a frame with a switchable filter changes.
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
    /// Determines whether any coded block uses a transform smaller than the largest size that its block size permits.
    /// </summary>
    /// <param name="picture">The completed frame decisions.</param>
    /// <returns><see langword="true"/> when at least one transform is split.</returns>
    private static bool HasTransformSplit(Av1PictureControlSet picture)
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ReadOnlySpan<int> grid = picture.ModeInfoGrid.Span;
        ReadOnlySpan<Av1MacroBlockModeInfo> allocation = picture.ModeInfoAllocation.Span;
        bool selectable = header.TransformMode == Av1TransformMode.Select;

        // An intra block has one transform size. The transform tree of an inter block has a size for each 4x4 cell. A visit to every cell finds each split at
        // least once.
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
                    // A skipped, lossless or 4x4 inter block has one size, and so does every inter block when the frame does not select sizes. That size is 4x4
                    // when lossless, and the largest size otherwise. Other blocks read the size of the cell from the transform tree.
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
    /// Block decisions read this state: speed settings for the update role that the caller stores in
    /// <see cref="Av1PictureParentControlSet.FrameUpdateType"/>, inter mode thresholds, transform-type and interpolation statistics, nearest references, motion
    /// search settings, and cleared restoration choices. The tile encoder calls it once before analysis. A caller that drives
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
            parent.EncoderOptions.IsAllIntra,
            frameHeader.IsIntra,
            parent.FrameUpdateType,
            frameHeader.QuantizationParameters.BaseQIndex,
            sourceSize,
            frameHeader.AllowScreenContentTools,
            parent.ScreenContentToolsBeforeTrial,
            parent.EncoderOptions.Sharpness,
            parent.EncoderOptions.Tuning);

        // Distortion stops at the coded boundary, or at the frame edge when the picture pads its border.
        ObuColorConfig colorConfig = picture.Sequence.SequenceHeader.ColorConfig;
        blockWorkspace.BorderPad = parent.BorderPad;
        blockWorkspace.LumaVisibleBoundary = parent.GetVisibleBoundary(0, 0);
        blockWorkspace.ChromaVisibleBoundary = parent.GetVisibleBoundary(
            colorConfig.SubSamplingX ? 1 : 0, colorConfig.SubSamplingY ? 1 : 0);

        if (!frameHeader.IsIntra)
        {
            // The DC step has four times the sample precision, plus two bits for each bit-depth step above eight bits. The division brings the step to
            // eight-bit sample units before the nonlinear scale. The scaled factor truncates to an integer before the lower limit of 8. Each segment scales its
            // thresholds by its own quantizer.
            Av1BitDepth bitDepth = picture.Sequence.SequenceHeader.ColorConfig.BitDepth;
            ObuQuantizationParameters quantization = frameHeader.QuantizationParameters;
            Span<int> quantizerFactors = blockWorkspace.ModeThresholdQuantizerFactors;
            for (int segmentId = 0; segmentId < quantizerFactors.Length; segmentId++)
            {
                int segmentQIndex = Av1QuantizationLookup.GetQIndex(
                    frameHeader.SegmentationParameters, segmentId, quantization.BaseQIndex);

                double step = Av1QuantizationLookup.GetDcQuant(segmentQIndex, quantization.DeltaQDc[0], bitDepth) /
                    (double)(1 << (2 + (2 * (int)bitDepth)));

                quantizerFactors[segmentId] = Math.Max((int)(Math.Pow(step, 1.25) * 5.12), 8);
            }
        }

        // Variance Boost signals a superblock quantizer at the resolution that the frame quantizer selects, and no loop filter deltas. The objective mode uses
        // its own fixed resolution. The flag clears after analysis when every superblock kept the frame quantizer.
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

        // Each coding block derives its rate multiplier from its superblock quantizer when the frame codes delta quantizers.
        parent.CodingBlockDeltaRateMultiplier = deltaQ.IsPresent;
        frameHeader.DeltaLoopFilterParameters.IsPresent = false;
        parent.DeltaQUsed = false;

        parent.TransformTypeCounts = blockWorkspace.TransformTypeCounts;
        parent.TransformTypeCounts.Span.Clear();

        // The frame probabilities reset to their defaults at a key frame, and at a golden refresh when warped motion is pruned further. The reset happens once
        // per frame, so a frame coded again after the screen content trial keeps what the trial updated.
        bool restoreProbabilities = !parent.RetainsFrameProbabilities &&
            (frameHeader.FrameType == ObuFrameType.KeyFrame || (parent.SpeedSettings.ExtraPruneWarped && parent.RefreshesGolden));

        parent.RetainsFrameProbabilities = false;
        parent.PalettePixelCount = 0;

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

            // The references are one-sided when no reference, enabled or not, comes after the frame.
            parent.AllOneSidedReferences = true;
            Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            Span<int> referenceFrameNumbers = blockWorkspace.ReferenceFrameNumbers;
            for (int index = 0; index < Av1Constants.ReferenceFrameCount - 1; index++)
            {
                int slot = (int)referenceFrameIndices[index];
                if (referenceFrameNumbers[slot] > blockWorkspace.EncodedFrameCount)
                {
                    parent.AllOneSidedReferences = false;
                }
            }
        }

        if (!frameHeader.IsIntra)
        {
            int nearestPastDistance = int.MaxValue;
            int nearestFutureDistance = int.MaxValue;
            Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            Span<int> referenceFrameNumbers = blockWorkspace.ReferenceFrameNumbers;
            for (Av1ReferenceFrameType referenceType = Av1ReferenceFrameType.Last; referenceType <= Av1ReferenceFrameType.Alternate; referenceType++)
            {
                if ((parent.AvailableReferenceMask & (1 << (int)referenceType)) == 0)
                {
                    continue;
                }

                int slot = (int)referenceFrameIndices[(int)referenceType - (int)Av1ReferenceFrameType.Last];
                int distance = referenceFrameNumbers[slot] - blockWorkspace.EncodedFrameCount;
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

        // The frame-size features read the screen content tools as the frame set them, before any trial, and the trial updates the quantizer-dependent features
        // before the frame does.
        bool screenContentToolsBeforeTrial = parent.ScreenContentToolsBeforeTrial ?? frameHeader.AllowScreenContentTools;
        Av1MotionSearchSettings motionSettings = new(
            parent.EncodingSpeed,
            parent.EncoderOptions.IsAllIntra,
            sourceSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            parent.ScreenContentTrialQIndex,
            parent.SpeedSettings.IsBoosted,
            parent.IsGraphicsAnimation || screenContentToolsBeforeTrial,
            parent.EncoderOptions.Tuning);

        parent.MotionSearchSettings = motionSettings;
        int maximumDimension = Math.Max(sourceSize.Width, sourceSize.Height);
        int stepParameter = Av1MotionSearchBase.GetInitialStepParameter(maximumDimension);

        // Only adaptive steps keep the vector magnitude between frames. A frame coded again keeps the step and the magnitude that its packing passes gather,
        // because the motion search setup runs once before the recode loop.
        if (parent.RecodesFrame)
        {
            stepParameter = parent.MotionSearchStepParameter;
        }
        else if (motionSettings.AutomaticStepSizeLevel != 0 && frameHeader.IsIntra)
        {
            // A key frame seeds the following inter frame with the complete frame range.
            parent.MaximumMotionVectorMagnitude = maximumDimension;
        }
        else if (motionSettings.AutomaticStepSizeLevel != 0)
        {
            // Shown frames and internal alternate references use the adaptive step.
            if ((frameHeader.ShowFrame || parent.FrameUpdateType == Av1FrameUpdateType.IntermediateAlternate) &&
                motionSettings.AutomaticStepSizeLevel >= 2 && parent.MaximumMotionVectorMagnitude != -1)
            {
                int range = Math.Min(maximumDimension, 2 * parent.MaximumMotionVectorMagnitude);
                stepParameter = Av1MotionSearchBase.GetInitialStepParameter(range);
            }

            // The packing pass accumulates the magnitudes of the written NEWMV vectors. Trial candidates and inherited vectors do not contribute. A frame with
            // no written NEWMV leaves a zero maximum for the next frame.
            parent.MaximumMotionVectorMagnitude = 0;
        }

        // Restoration decisions belong to the completed reconstruction. A reused frame header must not expose the choices of the previous frame during block
        // analysis.
        for (int plane = 0; plane < picture.Sequence.SequenceHeader.ColorConfig.PlaneCount; plane++)
        {
            frameHeader.LoopRestorationParameters.Items[plane].Type = ObuRestorationType.None;
        }

        frameHeader.LoopRestorationParameters.UsesLoopRestoration = false;
        frameHeader.LoopRestorationParameters.UsesChromaLoopRestoration = false;

        // Deblocking levels also come from the completed reconstruction. The reference and mode deltas are on, as in the default encoder configuration, and
        // keep their default values.
        ObuLoopFilterParameters loopFilter = frameHeader.LoopFilterParameters;
        loopFilter.FilterLevel.Clear();
        loopFilter.FilterLevelU = 0;
        loopFilter.FilterLevelV = 0;
        loopFilter.ReferenceDeltaModeEnabled = true;
        parent.MotionSearchStepParameter = stepParameter;

        // The frame starts its motion vector error per bit from the frame multiplier.
        blockWorkspace.ErrorPerBitRateMultiplier = parent.GetRateMultiplier(
            frameHeader.QuantizationParameters.BaseQIndex + frameHeader.QuantizationParameters.DeltaQDc[0],
            colorConfig.BitDepth);
    }

    /// <summary>
    /// Returns whether a frame of the objective delta quantizer mode keeps its delta quantizer syntax. The frame must not be a leaf frame, and its objective
    /// superblock quantizers must lower the estimated rate-distortion cost of the frame. Without temporal dependency statistics the syntax stays off. The
    /// estimate updates the regularized importance that the coding block rate multipliers divide by.
    /// </summary>
    /// <param name="picture">The picture whose parent carries the temporal dependency state.</param>
    /// <param name="blockWorkspace">The workspace that holds the regularized importance.</param>
    /// <returns><see langword="true"/> when the frame codes delta quantizers.</returns>
    private static bool AllowsObjectiveDeltaQ(Av1PictureControlSet picture, Av1EncoderBlockWorkspace blockWorkspace)
    {
        // One-pass coding has no pyramid level test, because that test needs two-pass statistics. Only leaf frames and frames without temporal dependency
        // statistics are excluded.
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

    /// <summary>
    /// Decides and reconstructs the blocks of an intra frame without filtering or packing it, as the screen content trial does. It also updates the frame
    /// probabilities that the coding keeps.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
    /// <param name="writer">The symbol encoder whose frame context the trial starts from.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    internal static void AnalyzeIntraFrame<TSample, TOperator>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        Av1PictureParentControlSet parent = picture.Parent;
        PrepareFrame(picture, new Size(source.Width, source.Height), blockWorkspace);

        // The SSIM rate multiplier factors come from the source before the trial. A sequence encoder that codes frames of different sizes measures the factors
        // itself.
        if (!parent.HasPrecomputedSsimRateMultiplierFactors)
        {
            parent.SsimRateMultiplierFactors = null;
            if (parent.EncoderOptions.Tuning == Av1Tuning.Ssim || parent.EncoderOptions.Tuning.IsImageTuning())
            {
                Av1IntraSuperblockEncoder.SetSsimRateMultiplierScaling<TSample, TOperator>(picture, source);
            }
        }

        ProcessTiles<TSample, TOperator, Av1SymbolEncoder.SymbolUpdateOperation>(
            writer, source, default, default, reconstruction, picture, coefficientBuffer, tileWorkspace, blockWorkspace);

        // The trial moves the transform type probabilities of the update type toward its selections.
        if (parent.SpeedSettings.TrackTransformTypeProbabilities)
        {
            Av1TransformTypeProbabilities.Update(
                blockWorkspace.TransformTypeProbabilities.Slice(
                    (int)parent.FrameUpdateType * Av1TransformTypeProbabilities.FrameLength, Av1TransformTypeProbabilities.FrameLength),
                parent.TransformTypeCounts.Span);
        }
    }

    /// <summary>
    /// Decides and reconstructs every block of a frame, filters the reconstruction, and packs the tiles.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
    /// <typeparam name="TVerticalOperator">The deblocking operations of vertical edges.</typeparam>
    /// <typeparam name="THorizontalOperator">The deblocking operations of horizontal edges.</typeparam>
    /// <typeparam name="TCdefOperator">The CDEF search and filter operations.</typeparam>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="searchReferences">
    /// The frames that the motion search reads, indexed by prediction reference identifier: each reference, or its copy resized to the coded frame size.
    /// </param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    private static void Encode<TSample, TOperator, TVerticalOperator, THorizontalOperator, TCdefOperator>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> references,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> searchReferences,
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
        bool switchableBeforeFix = AnalyzeFrame<TSample, TOperator>(
            writer, source, references, searchReferences, reconstruction, picture, coefficientBuffer, tileWorkspace, blockWorkspace);

        CompleteFrame<TSample, TOperator, TVerticalOperator, THorizontalOperator, TCdefOperator>(
            writer,
            source,
            references,
            searchReferences,
            reconstruction,
            picture,
            coefficientBuffer,
            tileWorkspace,
            blockWorkspace,
            switchableBeforeFix);
    }

    /// <summary>
    /// Returns a tile source for tile data that a packing pass already wrote.
    /// </summary>
    /// <param name="picture">The frame coding state that holds the tile lengths.</param>
    /// <param name="writer">The symbol encoder that holds the tile buffers of the packing pass.</param>
    /// <returns>The tile source.</returns>
    internal static Av1TileEncoder FromPackedTiles(Av1PictureControlSet picture, Av1SymbolEncoder writer)
        => new(picture, writer);

    /// <summary>
    /// Decides and reconstructs every block of a frame, then settles the frame-level syntax that the decisions allow: the delta quantizer flag, the segment map
    /// coding, intra block copy, the transform mode, the reference mode, skip mode and the interpolation filter. The reconstruction is not yet filtered.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="searchReferences">
    /// The frames that the motion search reads, indexed by prediction reference identifier: each reference, or its copy resized to the coded frame size.
    /// </param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <returns>Whether the frame filter was switchable before the filter fix narrowed it.</returns>
    internal static bool AnalyzeFrame<TSample, TOperator>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> references,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> searchReferences,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        Av1PictureParentControlSet parent = picture.Parent;
        ObuFrameHeader frameHeader = parent.FrameHeader;
        PrepareFrame(picture, new Size(source.Width, source.Height), blockWorkspace);

        // The SSIM rate multiplier factors come from the source. A sequence encoder that codes frames of different sizes measures the factors itself, before it
        // resizes the source.
        if (!parent.HasPrecomputedSsimRateMultiplierFactors)
        {
            parent.SsimRateMultiplierFactors = null;
            if (parent.EncoderOptions.Tuning == Av1Tuning.Ssim || parent.EncoderOptions.Tuning.IsImageTuning())
            {
                Av1IntraSuperblockEncoder.SetSsimRateMultiplierScaling<TSample, TOperator>(picture, source);
            }
        }

        ProcessTiles<TSample, TOperator, Av1SymbolEncoder.SymbolUpdateOperation>(
            writer, source, references, searchReferences, reconstruction, picture, coefficientBuffer, tileWorkspace, blockWorkspace);

        // A frame whose superblocks all kept the frame quantizer codes no delta quantizers.
        if (frameHeader.DeltaQParameters.IsPresent && !parent.DeltaQUsed)
        {
            frameHeader.DeltaQParameters.IsPresent = false;
        }

        // A frame that updates its segment map codes it against the map of the primary reference. The frame codes the map spatially when it has no primary
        // reference, or when the estimate says that spatial coding costs less.
        ObuSegmentationParameters segmentation = frameHeader.SegmentationParameters;
        if (segmentation.Enabled && segmentation.SegmentationUpdateMap == 1)
        {
            segmentation.SegmentationTemporalUpdate =
                frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone ||
                parent.SpatialSegmentCost < parent.TemporalSegmentCost ? 0 : 1;
        }

        // A frame that allows intra block copy but never selects it stops allowing it. The in-loop filters can then run.
        if (frameHeader.AllowIntraBlockCopy && !UsesIntraBlockCopy(picture))
        {
            frameHeader.AllowIntraBlockCopy = false;
        }

        // When no coded block split its transform, the frame signals the largest transforms instead of a selection per block. This frame parameter update runs
        // outside real-time mode, and in real-time mode when estimated compound prediction is on.
        Av1EncoderSpeedSettings speedSettings = parent.SpeedSettings;
        bool frameParameterUpdate = !speedSettings.UseEstimatedInterModeDecision || speedSettings.UseEstimatedCompound;
        if (frameHeader.TransformMode == Av1TransformMode.Select &&
            frameParameterUpdate &&
            !HasTransformSplit(picture))
        {
            frameHeader.TransformMode = Av1TransformMode.Largest;
        }

        // The filter probabilities update only when the frame filter is switchable before the filter fix narrows it. The method returns that state for the
        // update.
        bool switchableBeforeFix = frameHeader.InterpolationFilter == Av1InterpolationFilter.Switchable;
        if (!frameHeader.IsIntra)
        {
            // The same frame parameter update codes single references when no block used a compound reference. A single-reference frame does not allow skip
            // mode, so the flag turns off. Skip mode also turns off when no block used it.
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

            FixInterpolationFilter(picture);
        }

        return switchableBeforeFix;
    }

    /// <summary>
    /// Writes the symbols of every tile of an analyzed frame. Each tile starts from the entropy context of the frame. The pass counts the selected transform
    /// types, warped and OBMC motion, and interpolation filters again from zero.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="searchReferences">
    /// The frames that the motion search reads, indexed by prediction reference identifier: each reference, or its copy resized to the coded frame size.
    /// </param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    internal static void PackFrame<TSample, TOperator>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> references,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> searchReferences,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        // Analysis retains the selected modes, coefficients, palette tokens, and motion contexts. Packing starts from the same entropy edges and probabilities
        // while the completed frame decisions remain available. A frame packed before to measure its size counts its selections again.
        Av1PictureParentControlSet parent = picture.Parent;
        picture.ResetEntropyContexts();
        parent.LowMotionArea = 0;
        parent.TransformTypeCounts.Span.Clear();
        parent.SelectedInterpolationCounts.Span.Clear();
        Array.Clear(parent.WarpedUsage);
        Array.Clear(parent.ObmcUsage);
        ProcessTiles<TSample, TOperator, Av1SymbolEncoder.SymbolWriteOperation>(
            writer, source, references, searchReferences, reconstruction, picture, coefficientBuffer, tileWorkspace, blockWorkspace);
    }

    /// <summary>
    /// Moves the transform type and interpolation filter probabilities of the update type of the frame halfway toward the selections of the frame. The filter
    /// probabilities change only in a frame that is not a key frame and had a switchable filter before the filter fix.
    /// </summary>
    /// <param name="picture">The frame coding state with the selection counts.</param>
    /// <param name="blockWorkspace">The workspace that holds the probabilities.</param>
    /// <param name="switchableBeforeFix">Whether the frame filter was switchable before the filter fix.</param>
    internal static void UpdateFrameProbabilities(Av1PictureControlSet picture, Av1EncoderBlockWorkspace blockWorkspace, bool switchableBeforeFix)
    {
        Av1PictureParentControlSet parent = picture.Parent;
        ObuFrameHeader frameHeader = parent.FrameHeader;
        if (parent.SpeedSettings.TrackTransformTypeProbabilities)
        {
            Av1TransformTypeProbabilities.Update(
                blockWorkspace.TransformTypeProbabilities.Slice(
                    (int)parent.FrameUpdateType * Av1TransformTypeProbabilities.FrameLength, Av1TransformTypeProbabilities.FrameLength),
                parent.TransformTypeCounts.Span);
        }

        if (frameHeader.FrameType != ObuFrameType.KeyFrame && parent.SpeedSettings.InterpolationPruningLevel == 2 &&
            switchableBeforeFix)
        {
            Av1InterpolationProbabilities.Update(
                blockWorkspace.InterpolationProbabilities.Slice(
                    (int)parent.FrameUpdateType * Av1InterpolationProbabilities.FrameLength, Av1InterpolationProbabilities.FrameLength),
                parent.InterpolationCounts.Span);
        }
    }

    /// <summary>
    /// Filters the reconstruction of an analyzed frame, packs its tiles and records the frame in the workspace history: the low motion share, the frame
    /// probabilities, and the frame number and quantizer of each refreshed slot.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
    /// <typeparam name="TVerticalOperator">The deblocking operations of vertical edges.</typeparam>
    /// <typeparam name="THorizontalOperator">The deblocking operations of horizontal edges.</typeparam>
    /// <typeparam name="TCdefOperator">The CDEF search and filter operations.</typeparam>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="searchReferences">
    /// The frames that the motion search reads, indexed by prediction reference identifier: each reference, or its copy resized to the coded frame size.
    /// </param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="switchableBeforeFix">Whether the frame filter was switchable before the filter fix.</param>
    internal static void CompleteFrame<TSample, TOperator, TVerticalOperator, THorizontalOperator, TCdefOperator>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> references,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> searchReferences,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        bool switchableBeforeFix)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        where TVerticalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where THorizontalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where TCdefOperator : struct, Av1CdefEncoder.IEncodingOperator<TSample>
    {
        Av1PictureParentControlSet parent = picture.Parent;
        ObuFrameHeader frameHeader = parent.FrameHeader;

        // The deblocking filter picks its levels against the source, then filters the frame.
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

        PackFrame<TSample, TOperator>(
            writer, source, references, searchReferences, reconstruction, picture, coefficientBuffer, tileWorkspace, blockWorkspace);

        if (!frameHeader.IsIntra)
        {
            // The running low motion percentage keeps three quarters of its history. A zero average takes the percentage of the frame.
            int percentage = (100 * parent.LowMotionArea) / (frameHeader.ModeInfoRowCount * frameHeader.ModeInfoColumnCount);
            parent.AverageFrameLowMotion = parent.AverageFrameLowMotion == 0
                ? percentage
                : ((3 * parent.AverageFrameLowMotion) + percentage) / 4;

            parent.ReferenceRefreshControl?.AdjustRefresh(parent);
        }

        UpdateFrameProbabilities(picture, blockWorkspace, switchableBeforeFix);
        Span<int> referenceFrameNumbers = blockWorkspace.ReferenceFrameNumbers;
        for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
        {
            if ((frameHeader.RefreshFrameFlags & (1U << slot)) != 0)
            {
                referenceFrameNumbers[slot] = blockWorkspace.EncodedFrameCount;
                blockWorkspace.ReferenceBaseQIndices[slot] = frameHeader.QuantizationParameters.BaseQIndex;
            }
        }

        blockWorkspace.EncodedFrameCount++;
        blockWorkspace.FrameNumber = blockWorkspace.EncodedFrameCount;
        blockWorkspace.PreviousFrameRateMultiplier = parent.GetRateMultiplier(
            frameHeader.QuantizationParameters.BaseQIndex + frameHeader.QuantizationParameters.DeltaQDc[0],
            picture.Sequence.SequenceHeader.ColorConfig.BitDepth);
    }

    /// <summary>
    /// Runs the superblocks of every tile in coding order. The pass either decides them and updates the probabilities, or writes their symbols.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
    /// <typeparam name="TSymbolOperation">Whether the pass writes symbols or only updates the probabilities.</typeparam>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="searchReferences">
    /// The frames that the motion search reads, indexed by prediction reference identifier: each reference, or its copy resized to the coded frame size.
    /// </param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    private static void ProcessTiles<TSample, TOperator, TSymbolOperation>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> references,
        ReadOnlyMemory<Av1EncoderFrame<TSample>> searchReferences,
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
        Span<int> tileDataLengths = picture.TileDataLengths.Span;
        Av1MotionSearchSettings.CostUpdateFrequency motionCostUpdate = picture.Parent.MotionSearchSettings.MotionCostUpdate;
        Av1MotionSearchSettings.CostUpdateFrequency modeCostUpdate = Av1MotionSearchSettings.CostUpdateFrequency.Superblock;
        CancellationToken cancellationToken = picture.Parent.EncoderOptions.CancellationToken;
        int minimumDimension = Math.Min(source.Width, source.Height);
        bool allIntra = picture.Parent.EncoderOptions.IsAllIntra;
        if (allIntra && picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level9)
        {
            // All-intra coding at speed 9 refreshes mode rates once per superblock row when the short side is 2160 samples or more, and never for smaller
            // frames.
            modeCostUpdate = minimumDimension < 2160
                ? Av1MotionSearchSettings.CostUpdateFrequency.Off
                : Av1MotionSearchSettings.CostUpdateFrequency.SuperblockRow;
        }
        else if (!allIntra && !picture.Parent.SpeedSettings.IsRealtime &&
            picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level6 && minimumDimension < 720)
        {
            // At speed 6 and higher, good-quality frames with a short side under 720 samples refresh mode rates once per superblock row. Real-time usage keeps
            // the default refresh at every superblock.
            modeCostUpdate = Av1MotionSearchSettings.CostUpdateFrequency.SuperblockRow;
        }

        // Only good-quality usage refreshes the displacement vector rates at each superblock. All-intra and real-time usage keep the rates of the tile start.
        bool refreshDisplacementCosts = !allIntra && !picture.Parent.SpeedSettings.IsRealtime;

        if (!TSymbolOperation.WritesOutput && frameHeader.AllowIntraBlockCopy)
        {
            // The visible source is hashed once before reconstruction starts. Candidate discovery then does not depend on coding order, and the workspace can
            // serve as compact bucket links afterward.
            picture.IntraBlockCopySearch.Initialize<TSample, TOperator>(
                source.View.GetPlane(Av1Plane.Y));
        }

        int tileIndex = 0;
        int tileCount = tileLayout.TileColumnCount * tileLayout.TileRowCount;
        int largestTileLength = 0;
        int largestTileIndex = 0;

        // The inter search state exists only for inter frames.
        Span<Av1InterModeRateDistortionModel> interModeModels = frameHeader.IsIntra ? default : blockWorkspace.InterModeModels;
        Span<int> modeThresholdFactors = frameHeader.IsIntra ? default : blockWorkspace.ModeThresholdFactors;

        // The rate tables and the transform buffers of every block search of the frame, read once here and passed down.
        Av1CoefficientTables tables = writer.GetCoefficientTables();
        Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace = blockWorkspace.GetModeDecisionWorkspace<TSample>();
        Span<int> transformCoefficients = blockWorkspace.TransformCoefficients;
        Span<int> dequantizedCoefficients = blockWorkspace.DequantizedCoefficients;
        Span<int> searchDequantizedCoefficients = blockWorkspace.SearchDequantizedCoefficients;
        Span<int> transformWorkspace = blockWorkspace.TransformWorkspace;
        ReadOnlySpan<int> transformTypeProbabilities = blockWorkspace.TransformTypeProbabilities;
        Span<short> blockResidual = blockWorkspace.Residual;
        Span<int> searchCoefficients = blockWorkspace.SearchCoefficients;
        Span<int> searchReconstructions = blockWorkspace.SearchReconstructions;
        Span<int> estimationRowCoefficients = blockWorkspace.EstimationRowCoefficients;

        // The inter prediction buffers of the block searches. An intra frame prices no motion vector, and a worker that encodes only intra frames keeps no
        // motion vector rates.
        Av1EncoderInterPredictionWorkspace<TSample> interWorkspace = blockWorkspace.GetInterPredictionWorkspace<TSample>();
        Span<TSample> motionSearchPrediction = blockWorkspace.GetMotionSearchPrediction<TSample>();
        Av1MotionVectorCosts motionVectorCosts = frameHeader.IsIntra ? default : blockWorkspace.GetMotionVectorCosts(frameHeader.MotionVectorPrecision);
        blockWorkspace.GetInterIntraStorage<TSample>(
            out Span<TSample> transformPrediction, out Span<TSample> interIntraAbove, out Span<TSample> interIntraLeft);

        blockWorkspace.GetCompoundPredictionIntermediates(out Span<ushort> firstIntermediate, out Span<ushort> secondIntermediate);
        Span<byte> compoundMask = blockWorkspace.GetCompoundPredictionMask();

        // The search buffers that each block reads from the workspace are sliced from this storage.
        Span<int> workspaceStorage = blockWorkspace.Storage;

        // The mode decision reads the coded views of the source and the reconstruction, so their samples are read once here.
        Span<TSample> sourceLuma = source.CodedView.GetPlane(Av1Plane.Y).Samples;
        Span<TSample> sourceBlue = source.CodedView.GetPlane(Av1Plane.U).Samples;
        Span<TSample> sourceRed = source.CodedView.GetPlane(Av1Plane.V).Samples;
        Span<TSample> reconstructionLuma = reconstruction.CodedView.GetPlane(Av1Plane.Y).Samples;
        Span<TSample> reconstructionBlue = reconstruction.CodedView.GetPlane(Av1Plane.U).Samples;
        Span<TSample> reconstructionRed = reconstruction.CodedView.GetPlane(Av1Plane.V).Samples;

        // The mode information and the retained block decisions of the picture serve every block of the frame pass, so they are read once here.
        Span<int> modeInfoGrid = picture.ModeInfoGrid.Span;
        Span<Av1MacroBlockModeInfo> modeInfoAllocation = picture.ModeInfoAllocation.Span;
        Span<Av1EncoderDisplacementVector> displacementVectors = picture.DisplacementVectors.Span;
        Span<Av1EncoderReferenceContext> referenceContexts = picture.ReferenceContexts.Span;
        Span<Av1EncoderBlockStruct> blockEncodings = picture.BlockEncodings.Span;
        Span<Av1EncoderPaletteInfo> blockPalettes = picture.BlockPalettes.Span;
        Span<byte> paletteTokens = picture.PaletteTokens.Span;
        Span<int> cdefPreset = picture.CdefPreset.Span;
        Span<int> previousQIndex = picture.Parent.PreviousQIndex.Span;

        // The symbol statistics and the segment maps of the frame are also updated by every block, so they are read once here.
        Span<int> transformTypeCounts = picture.Parent.TransformTypeCounts.Span;
        Span<int> interpolationCounts = picture.Parent.InterpolationCounts.Span;
        Span<int> selectedInterpolationCounts = picture.Parent.SelectedInterpolationCounts.Span;
        Span<byte> encoderSegmentMap = picture.Parent.EncoderSegmentMap.Span;
        Span<byte> segmentationNeighborMap = picture.SegmentationNeighborMap.Span;
        Span<byte> searchSegmentMap = picture.Parent.SearchSegmentMap.Span;
        ReadOnlySpan<byte> previousSegmentMap = picture.Parent.PreviousSegmentMap.Span;

        for (int tileRow = 0; tileRow < tileLayout.TileRowCount; tileRow++)
        {
            tile.SetTileRow(tileLayout, frameHeader.ModeInfoRowCount, tileRow);
            for (int tileColumn = 0; tileColumn < tileLayout.TileColumnCount; tileColumn++)
            {
                tile.SetTileColumn(tileLayout, frameHeader.ModeInfoColumnCount, tileColumn);

                // Each pass starts every tile from the same frame probabilities. Only the packing pass writes into the buffer of the tile. The analysis
                // operation does not change the state of the range coder.
                writer.Reset(tileIndex);

                // The range coder of the tile writes into the tile buffer, which is read once here. A write that grows the buffer replaces this span.
                Span<byte> output = writer.GetTileBuffer();

                // The neighbor context edges of the tile serve every block of the tile, so they are read once here. Only a frame with screen content tools has
                // palette contexts.
                Av1NeighborEdges<Av1PartitionContext> partitionEdges = picture.PartitionContexts[tileIndex].GetEdges();
                Av1NeighborEdges<byte> transformEdges = picture.TransformFunctionContexts[tileIndex].GetEdges();
                Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges = frameHeader.AllowScreenContentTools
                    ? picture.PaletteContexts[tileIndex].GetEdges()
                    : default;

                Av1NeighborEdges<byte> lumaCoefficientEdges = picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
                Av1NeighborEdges<byte> blueCoefficientEdges = picture.CbDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
                Av1NeighborEdges<byte> redCoefficientEdges = picture.CrDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
                if (TSymbolOperation.WritesOutput)
                {
                    picture.Parent.MotionVectorStatistics?.BeginTile(writer.FrameMotionVectorContext);
                }

                if (!TSymbolOperation.WritesOutput && !frameHeader.IsIntra)
                {
                    interModeModels.Clear();
                }

                int motionCostRowInterval = 1;
                if (motionCostUpdate == Av1MotionSearchSettings.CostUpdateFrequency.SuperblockRowSet)
                {
                    // The tile takes one update per 256 luma rows, spread evenly over its superblock rows. Two divisions that round up keep short final tiles
                    // evenly spaced.
                    int tileHeight = (tile.ModeInfoRowEnd - tile.ModeInfoRowStart) << Av1Constants.ModeInfoSizeLog2;
                    int updateCount = (tileHeight + 255) / 256;
                    int updateSpan = updateCount << sequenceHeader.SuperblockSizeLog2;
                    motionCostRowInterval = (tileHeight + updateSpan - 1) / updateSpan;
                }

                Point firstModeInfoPosition = new(tile.ModeInfoColumnStart, tile.ModeInfoRowStart);
                entropyContext.MacroBlockModeInfo = picture.GetMacroBlockModeInfo(modeInfoAllocation, firstModeInfoPosition);
                for (int modeInfoRow = tile.ModeInfoRowStart;
                    modeInfoRow < tile.ModeInfoRowEnd;
                    modeInfoRow += superblockModeInfoSize)
                {
                    // A canceled encode stops between superblock rows, so a large frame does not run to its end.
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!TSymbolOperation.WritesOutput && !frameHeader.IsIntra)
                    {
                        // The block searches of this row share the mode threshold factors, partition trials included. Every row starts at 1.0 in Q5 format
                        // (32). Packing does not change the search history.
                        modeThresholdFactors.Fill(32);
                    }

                    for (int modeInfoColumn = tile.ModeInfoColumnStart;
                        modeInfoColumn < tile.ModeInfoColumnEnd;
                        modeInfoColumn += superblockModeInfoSize)
                    {
                        int superblockRow = modeInfoRow >> superblockShift;
                        int superblockColumn = modeInfoColumn >> superblockShift;
                        superblock.Index = (superblockRow * coefficientBuffer.SuperblockColumnCount) + superblockColumn;
                        superblock.TileIndex = tileIndex;

                        // The coefficients and transform block states of the superblock serve every block in it, so they are read once here.
                        Span<int> superblockCoefficients = coefficientBuffer.GetSuperblockSpan(superblock.Index);

                        // Each superblock starts with an empty rate-distortion record and no picked reference frames.
                        blockWorkspace.MacroblockRateDistortionRecord.Reset();
                        Array.Clear(blockWorkspace.PickedReferenceFrameMasks);
                        entropyContext.SuperblockOrigin = new Point(
                            modeInfoColumn << Av1Constants.ModeInfoSizeLog2,
                            modeInfoRow << Av1Constants.ModeInfoSizeLog2);

                        if (TSymbolOperation.WritesOutput)
                        {
                            Av1TileWriter.RetainedBlockEncodingHandler<TSample> blockEncoder = new(picture);
                            Av1TileWriter.WriteSuperblock<TSymbolOperation, Av1TileWriter.RetainedBlockEncodingHandler<TSample>, TSample>(
                                ref output,
                                picture,
                                entropyContext,
                                writer,
                                in tables,
                                in modeWorkspace,
                                transformCoefficients,
                                dequantizedCoefficients,
                                searchDequantizedCoefficients,
                                transformWorkspace,
                                transformTypeProbabilities,
                                blockResidual,
                                searchCoefficients,
                                searchReconstructions,
                                estimationRowCoefficients,
                                in interWorkspace,
                                motionSearchPrediction,
                                in motionVectorCosts,
                                transformPrediction,
                                interIntraAbove,
                                interIntraLeft,
                                firstIntermediate,
                                secondIntermediate,
                                compoundMask,
                                in partitionEdges,
                                in transformEdges,
                                in paletteEdges,
                                in lumaCoefficientEdges,
                                in blueCoefficientEdges,
                                in redCoefficientEdges,
                                modeInfoGrid,
                                modeInfoAllocation,
                                displacementVectors,
                                referenceContexts,
                                blockEncodings,
                                blockPalettes,
                                paletteTokens,
                                cdefPreset,
                                previousQIndex,
                                transformTypeCounts,
                                interpolationCounts,
                                selectedInterpolationCounts,
                                encoderSegmentMap,
                                segmentationNeighborMap,
                                searchSegmentMap,
                                previousSegmentMap,
                                superblockCoefficients,
                                workspaceStorage,
                                sourceLuma,
                                sourceBlue,
                                sourceRed,
                                reconstructionLuma,
                                reconstructionBlue,
                                reconstructionRed,
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
                                // The tile reset gave the first rates of this tile. A later refresh uses only the selected blocks before it. All candidates
                                // between two refreshes share the same rates.
                                writer.RefreshCosts();
                            }

                            int tileSuperblockRow = (modeInfoRow - tile.ModeInfoRowStart) >> superblockShift;
                            bool refreshMotionCosts = motionCostUpdate == Av1MotionSearchSettings.CostUpdateFrequency.Superblock ||
                                (firstColumn && (tileSuperblockRow % motionCostRowInterval) == 0);

                            if (!frameHeader.IsIntra && (firstSuperblock || (!frameHeader.DisableCdfUpdate && refreshMotionCosts)))
                            {
                                // The first superblock of each tile takes its rates from the starting CDF of the tile, also when adaptation is off. A later
                                // refresh at the configured interval uses only the selected blocks before it. All candidate trials between two refreshes share
                                // the same rates.
                                writer.FillMotionVectorCosts(motionVectorCosts);
                            }

                            if (frameHeader.AllowIntraBlockCopy &&
                                (firstSuperblock || (refreshDisplacementCosts && !frameHeader.DisableCdfUpdate)))
                            {
                                // All-intra and real-time usage keep the initial displacement rates. Good-quality usage refreshes them at each superblock while
                                // entropy coding continues to adapt.
                                writer.FillDisplacementVectorCosts(blockWorkspace.GetDisplacementVectorCosts(workspaceStorage));
                            }

                            Av1IntraSuperblockEncoder.Prepare(picture, modeInfoAllocation, superblock, entropyContext.SuperblockOrigin);

                            Av1IntraSuperblockEncoder.ModeDecision<TSample, TOperator> blockEncoder = new(
                                source,
                                references,
                                searchReferences,
                                reconstruction,
                                picture,
                                superblock,
                                coefficientBuffer,
                                blockWorkspace);

                            blockEncoder.PrepareGradientCache(in modeWorkspace, sourceLuma, sourceBlue, entropyContext.SuperblockOrigin);

                            Av1TileWriter.WriteSuperblock<TSymbolOperation, Av1IntraSuperblockEncoder.ModeDecision<TSample, TOperator>, TSample>(
                                ref output,
                                picture,
                                entropyContext,
                                writer,
                                in tables,
                                in modeWorkspace,
                                transformCoefficients,
                                dequantizedCoefficients,
                                searchDequantizedCoefficients,
                                transformWorkspace,
                                transformTypeProbabilities,
                                blockResidual,
                                searchCoefficients,
                                searchReconstructions,
                                estimationRowCoefficients,
                                in interWorkspace,
                                motionSearchPrediction,
                                in motionVectorCosts,
                                transformPrediction,
                                interIntraAbove,
                                interIntraLeft,
                                firstIntermediate,
                                secondIntermediate,
                                compoundMask,
                                in partitionEdges,
                                in transformEdges,
                                in paletteEdges,
                                in lumaCoefficientEdges,
                                in blueCoefficientEdges,
                                in redCoefficientEdges,
                                modeInfoGrid,
                                modeInfoAllocation,
                                displacementVectors,
                                referenceContexts,
                                blockEncodings,
                                blockPalettes,
                                paletteTokens,
                                cdefPreset,
                                previousQIndex,
                                transformTypeCounts,
                                interpolationCounts,
                                selectedInterpolationCounts,
                                encoderSegmentMap,
                                segmentationNeighborMap,
                                searchSegmentMap,
                                previousSegmentMap,
                                superblockCoefficients,
                                workspaceStorage,
                                sourceLuma,
                                sourceBlue,
                                sourceRed,
                                reconstructionLuma,
                                reconstructionBlue,
                                reconstructionRed,
                                superblock,
                                coefficientBuffer,
                                (ushort)tileIndex,
                                ref blockEncoder);

                            if (!frameHeader.IsIntra && picture.Parent.SpeedSettings.InterModeEstimation == 1 &&
                                tileLayout.TileColumnCount == 1 && tileLayout.TileRowCount == 1)
                            {
                                // The models fit only after all partition trials of this superblock finish. Every candidate inside the superblock uses the
                                // previous fit.
                                foreach (ref Av1InterModeRateDistortionModel model in interModeModels)
                                {
                                    model.Fit();
                                }
                            }
                        }
                    }
                }

                if (TSymbolOperation.WritesOutput)
                {
                    // The bytes of the tile stay in its tile buffer, where the frame writer reads them.
                    int tileDataLength = writer.ExitTile();
                    tileDataLengths[tileIndex] = tileDataLength;

                    // The largest tile, the first of equal sizes, updates the frame context.
                    if (tileCount > 1 && tileDataLength > largestTileLength)
                    {
                        largestTileLength = tileDataLength;
                        largestTileIndex = tileIndex;
                        writer.RetainContextUpdateTile();
                    }
                }

                tileIndex++;
            }
        }

        if (TSymbolOperation.WritesOutput && tileCount > 1)
        {
            // The frame signals the largest tile as the context update tile. The tile size field uses the fewest bytes that hold the size of the largest tile.
            tileLayout.ContextUpdateTileId = (uint)largestTileIndex;
            tileLayout.TileSizeBytes = (uint)largestTileLength >> 24 != 0 ? 4
                : (uint)largestTileLength >> 16 != 0 ? 3
                : (uint)largestTileLength >> 8 != 0 ? 2
                : 1;
        }
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
