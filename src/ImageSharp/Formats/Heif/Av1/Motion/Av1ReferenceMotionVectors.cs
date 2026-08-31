// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Derives the weighted AV1 reference-motion-vector candidates for one inter block.
/// </summary>
internal sealed class Av1ReferenceMotionVectors
{
    /// <summary>
    /// The number of surrounding mode-information rows and columns examined by the spatial search.
    /// </summary>
    private const int ReferenceSearchDistance = 3;

    /// <summary>
    /// The weight separating immediately adjacent candidates from temporal and outer spatial candidates.
    /// </summary>
    private const int NearestCandidateWeight = 640;

    /// <summary>
    /// The maximum number of distinct candidates retained by AV1.
    /// </summary>
    private const int CandidateCapacity = 8;

    /// <summary>
    /// The width and height of the outer spatial and temporal search boundary in 4x4 mode-information units.
    /// </summary>
    private const int MaximumSearchBlockSize = 16;

    /// <summary>
    /// The packed mode-context bit containing temporal availability relative to global motion.
    /// </summary>
    private const int GlobalMotionContextBit = 1 << 3;

    /// <summary>
    /// The bit offset of the reference-motion-vector context in the packed mode context.
    /// </summary>
    private const int ReferenceMotionVectorContextOffset = 4;

    /// <summary>
    /// Stores the unique candidates in their normative weighted order.
    /// </summary>
    private InlineArray8<Av1MotionVector> candidates;

    /// <summary>
    /// Stores the secondary vector of each compound candidate.
    /// </summary>
    private InlineArray8<Av1MotionVector> compoundCandidates;

    /// <summary>
    /// Stores the accumulated spatial or temporal weight corresponding to each candidate.
    /// </summary>
    private InlineArray8<ushort> weights;

    /// <summary>
    /// Stores the nearest and near references after applying AV1 fallback and precision rules.
    /// </summary>
    private InlineArray2<Av1MotionVector> references;

    /// <summary>
    /// Stores the nearest and near secondary references for a compound block.
    /// </summary>
    private InlineArray2<Av1MotionVector> compoundReferences;

    /// <summary>
    /// Gets the number of valid entries in <see cref="Candidates"/> and <see cref="Weights"/>.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets the packed entropy context derived from adjacent, outer, and temporal candidates.
    /// </summary>
    public int ModeContext { get; private set; }

    /// <summary>
    /// Gets the derived candidates in normative nearest-region then outer-region order.
    /// </summary>
    public ReadOnlySpan<Av1MotionVector> Candidates => this.candidates[..this.Count];

    /// <summary>
    /// Gets the secondary vectors corresponding to <see cref="Candidates"/> for a compound block.
    /// </summary>
    public ReadOnlySpan<Av1MotionVector> CompoundCandidates => this.compoundCandidates[..this.Count];

    /// <summary>
    /// Gets the accumulated weight corresponding to each entry in <see cref="Candidates"/>.
    /// </summary>
    public ReadOnlySpan<ushort> Weights => this.weights[..this.Count];

    /// <summary>
    /// Gets the nearest reference, or the current block's global-motion vector when no candidate exists.
    /// </summary>
    public Av1MotionVector Nearest => this.references[0];

    /// <summary>
    /// Derives all single- or compound-reference motion-vector candidates for the current block.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry and decoded spatial neighbors.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="frameInfo">The frame-wide spatial map and projected temporal motion field.</param>
    /// <param name="sequenceHeader">The sequence-level superblock and order-hint configuration.</param>
    /// <param name="frameHeader">The frame-level global-motion and motion-vector precision configuration.</param>
    /// <param name="referenceFrame">The primary canonical inter reference selected for the current block.</param>
    /// <param name="secondaryReferenceFrame">The secondary compound reference, or <see cref="Av1ReferenceFrameType.None"/>.</param>
    public void Build(
        ref Av1PartitionInfo partitionInfo,
        Av1TileInfo tileInfo,
        Av1FrameInfo frameInfo,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame = Av1ReferenceFrameType.None)
    {
        Av1BlockSize blockSize = partitionInfo.ModeInfo.BlockSize;
        int width = blockSize.Get4x4WideCount();
        int height = blockSize.Get4x4HighCount();
        int row = partitionInfo.RowIndex;
        int column = partitionInfo.ColumnIndex;
        int rowAdjustment = height < 2 && (row & 1) != 0 ? 1 : 0;
        int columnAdjustment = width < 2 && (column & 1) != 0 ? 1 : 0;
        int maximumRowOffset = 0;
        int maximumColumnOffset = 0;

        this.Count = 0;
        this.ModeContext = 0;

        if (partitionInfo.AvailableAbove)
        {
            maximumRowOffset = height < 2 ? -4 + rowAdjustment : -(ReferenceSearchDistance << 1) + rowAdjustment;
            maximumRowOffset = Math.Clamp(maximumRowOffset, tileInfo.ModeInfoRowStart - row, tileInfo.ModeInfoRowEnd - row - 1);
        }

        if (partitionInfo.AvailableLeft)
        {
            maximumColumnOffset = width < 2 ? -4 + columnAdjustment : -(ReferenceSearchDistance << 1) + columnAdjustment;
            maximumColumnOffset = Math.Clamp(maximumColumnOffset, tileInfo.ModeInfoColumnStart - column, tileInfo.ModeInfoColumnEnd - column - 1);
        }

        Av1GlobalMotionParameters globalMotion = frameHeader.GetGlobalMotionParameters()[(int)referenceFrame - 1];
        Av1MotionVector globalMotionVector = globalMotion.GetMotionVector(
            frameHeader.AllowHighPrecisionMotionVector,
            blockSize,
            new Point(column, row),
            frameHeader.ForceIntegerMotionVector);

        Av1GlobalMotionParameters secondaryGlobalMotion = default;
        Av1MotionVector secondaryGlobalMotionVector = default;
        if (secondaryReferenceFrame > Av1ReferenceFrameType.Intra)
        {
            secondaryGlobalMotion = frameHeader.GetGlobalMotionParameters()[(int)secondaryReferenceFrame - 1];
            secondaryGlobalMotionVector = secondaryGlobalMotion.GetMotionVector(
                frameHeader.AllowHighPrecisionMotionVector,
                blockSize,
                new Point(column, row),
                frameHeader.ForceIntegerMotionVector);
        }

        int processedRows = 0;
        int processedColumns = 0;
        int rowMatchCount = 0;
        int columnMatchCount = 0;
        int newMotionVectorCount = 0;

        // Immediate above and left scans form a distinct high-priority region. Their direction-level match counts,
        // rather than their number of unique vectors, drive the packed inter-mode entropy context.
        if (Math.Abs(maximumRowOffset) >= 1)
        {
            this.ScanRow(
                ref partitionInfo,
                referenceFrame,
                secondaryReferenceFrame,
                in globalMotion,
                in secondaryGlobalMotion,
                globalMotionVector,
                secondaryGlobalMotionVector,
                -1,
                maximumRowOffset,
                ref rowMatchCount,
                ref newMotionVectorCount,
                ref processedRows);
        }

        if (Math.Abs(maximumColumnOffset) >= 1)
        {
            this.ScanColumn(
                ref partitionInfo,
                referenceFrame,
                secondaryReferenceFrame,
                in globalMotion,
                in secondaryGlobalMotion,
                globalMotionVector,
                secondaryGlobalMotionVector,
                -1,
                maximumColumnOffset,
                ref columnMatchCount,
                ref newMotionVectorCount,
                ref processedColumns);
        }

        if (partitionInfo.HasTopRight(sequenceHeader.SuperblockModeInfoSize))
        {
            this.AddSpatialBlock(
                ref partitionInfo,
                tileInfo,
                referenceFrame,
                secondaryReferenceFrame,
                in globalMotion,
                in secondaryGlobalMotion,
                globalMotionVector,
                secondaryGlobalMotionVector,
                -1,
                width,
                ref rowMatchCount,
                ref newMotionVectorCount);
        }

        int nearestMatch = (rowMatchCount > 0 ? 1 : 0) + (columnMatchCount > 0 ? 1 : 0);
        int nearestCandidateCount = this.Count;
        for (int index = 0; index < nearestCandidateCount; index++)
        {
            this.weights[index] += NearestCandidateWeight;
        }

        if (frameHeader.UseReferenceFrameMotionVectors)
        {
            this.AddTemporalCandidates(
                ref partitionInfo,
                tileInfo,
                frameInfo,
                sequenceHeader.OrderHintInfo,
                frameHeader,
                referenceFrame,
                secondaryReferenceFrame,
                globalMotionVector,
                secondaryGlobalMotionVector);
        }

        int ignoredNewMotionVectorCount = 0;

        // The top-left block begins the lower-priority outer region. Candidate deduplication still spans both
        // regions, while the two independent stable sorts below preserve the normative nearest-before-outer order.
        this.AddSpatialBlock(
            ref partitionInfo,
            tileInfo,
            referenceFrame,
            secondaryReferenceFrame,
            in globalMotion,
            in secondaryGlobalMotion,
            globalMotionVector,
            secondaryGlobalMotionVector,
            -1,
            -1,
            ref rowMatchCount,
            ref ignoredNewMotionVectorCount);

        for (int index = 2; index <= ReferenceSearchDistance; index++)
        {
            int rowOffset = -(index << 1) + 1 + rowAdjustment;
            int columnOffset = -(index << 1) + 1 + columnAdjustment;
            if (Math.Abs(rowOffset) <= Math.Abs(maximumRowOffset) && Math.Abs(rowOffset) > processedRows)
            {
                this.ScanRow(
                    ref partitionInfo,
                    referenceFrame,
                    secondaryReferenceFrame,
                    in globalMotion,
                    in secondaryGlobalMotion,
                    globalMotionVector,
                    secondaryGlobalMotionVector,
                    rowOffset,
                    maximumRowOffset,
                    ref rowMatchCount,
                    ref ignoredNewMotionVectorCount,
                    ref processedRows);
            }

            if (Math.Abs(columnOffset) <= Math.Abs(maximumColumnOffset) && Math.Abs(columnOffset) > processedColumns)
            {
                this.ScanColumn(
                    ref partitionInfo,
                    referenceFrame,
                    secondaryReferenceFrame,
                    in globalMotion,
                    in secondaryGlobalMotion,
                    globalMotionVector,
                    secondaryGlobalMotionVector,
                    columnOffset,
                    maximumColumnOffset,
                    ref columnMatchCount,
                    ref ignoredNewMotionVectorCount,
                    ref processedColumns);
            }
        }

        int referenceMatchCount = (rowMatchCount > 0 ? 1 : 0) + (columnMatchCount > 0 ? 1 : 0);
        this.ModeContext |= nearestMatch switch
        {
            0 => (referenceMatchCount >= 1 ? 1 : 0) |
                (referenceMatchCount == 1 ? 1 << ReferenceMotionVectorContextOffset :
                    referenceMatchCount >= 2 ? 2 << ReferenceMotionVectorContextOffset : 0),
            1 => (newMotionVectorCount > 0 ? 2 : 3) |
                (referenceMatchCount == 1 ? 3 << ReferenceMotionVectorContextOffset :
                    referenceMatchCount >= 2 ? 4 << ReferenceMotionVectorContextOffset : 0),
            _ => (newMotionVectorCount >= 1 ? 4 : 5) | (5 << ReferenceMotionVectorContextOffset),
        };

        this.SortByWeight(0, nearestCandidateCount);
        this.SortByWeight(nearestCandidateCount, this.Count);

        int frameWidth = frameHeader.ModeInfoColumnCount;
        int frameHeight = frameHeader.ModeInfoRowCount;
        int modeInfoWidth = Math.Min(Math.Min(MaximumSearchBlockSize, width), frameWidth - column);
        int modeInfoHeight = Math.Min(Math.Min(MaximumSearchBlockSize, height), frameHeight - row);
        int extensionLength = Math.Min(modeInfoWidth, modeInfoHeight);

        if (secondaryReferenceFrame > Av1ReferenceFrameType.Intra)
        {
            if (this.Count < 2)
            {
                this.ExtendCompoundStack(
                    ref partitionInfo,
                    frameInfo,
                    referenceFrame,
                    secondaryReferenceFrame,
                    globalMotionVector,
                    secondaryGlobalMotionVector,
                    Math.Abs(maximumRowOffset) >= 1,
                    Math.Abs(maximumColumnOffset) >= 1,
                    extensionLength);
            }
        }
        else
        {
            // This fallback supplies only the nearest and near pair when direct and projected scans leave gaps.
            // Differing reference sign biases are reversed before either candidate enters that pair.
            for (int index = 0; Math.Abs(maximumRowOffset) >= 1 && index < extensionLength && this.Count < 2;)
            {
                Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column + index, row - 1));
                this.AddExtensionCandidate(candidate, frameInfo, referenceFrame);
                index += candidate.BlockSize.Get4x4WideCount();
            }

            for (int index = 0; Math.Abs(maximumColumnOffset) >= 1 && index < extensionLength && this.Count < 2;)
            {
                Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column - 1, row + index));
                this.AddExtensionCandidate(candidate, frameInfo, referenceFrame);
                index += candidate.BlockSize.Get4x4HighCount();
            }
        }

        for (int index = 0; index < this.Count; index++)
        {
            this.candidates[index] = this.candidates[index].ClampReference(
                blockSize.GetWidth(),
                blockSize.GetHeight(),
                partitionInfo.ModeBlockToLeftEdge,
                partitionInfo.ModeBlockToRightEdge,
                partitionInfo.ModeBlockToTopEdge,
                partitionInfo.ModeBlockToBottomEdge);

            if (secondaryReferenceFrame > Av1ReferenceFrameType.Intra)
            {
                this.compoundCandidates[index] = this.compoundCandidates[index].ClampReference(
                    blockSize.GetWidth(),
                    blockSize.GetHeight(),
                    partitionInfo.ModeBlockToLeftEdge,
                    partitionInfo.ModeBlockToRightEdge,
                    partitionInfo.ModeBlockToTopEdge,
                    partitionInfo.ModeBlockToBottomEdge);
            }
        }

        // The two-element reference list is separate from the full DRL stack. Missing entries use global motion,
        // and both entries undergo the same precision reduction as the reference decoder's av1_find_best_ref_mvs output.
        this.references[0] = (this.Count > 0 ? this.candidates[0] : globalMotionVector).LowerPrecision(
            frameHeader.AllowHighPrecisionMotionVector,
            frameHeader.ForceIntegerMotionVector);

        this.references[1] = (this.Count > 1 ? this.candidates[1] : globalMotionVector).LowerPrecision(
            frameHeader.AllowHighPrecisionMotionVector,
            frameHeader.ForceIntegerMotionVector);

        if (secondaryReferenceFrame > Av1ReferenceFrameType.Intra)
        {
            this.compoundReferences[0] = this.compoundCandidates[0].LowerPrecision(
                frameHeader.AllowHighPrecisionMotionVector,
                frameHeader.ForceIntegerMotionVector);

            this.compoundReferences[1] = this.compoundCandidates[1].LowerPrecision(
                frameHeader.AllowHighPrecisionMotionVector,
                frameHeader.ForceIntegerMotionVector);
        }
    }

    /// <summary>
    /// Gets the near reference selected by a decoded dynamic-reference-list index.
    /// </summary>
    /// <param name="referenceMotionVectorIndex">The decoded zero-based dynamic-reference-list index.</param>
    /// <returns>The selected near motion vector.</returns>
    public Av1MotionVector GetNearReference(int referenceMotionVectorIndex)
        => referenceMotionVectorIndex == 0 ? this.references[1] : this.candidates[referenceMotionVectorIndex + 1];

    /// <summary>
    /// Gets the differential reference used to decode a new motion vector.
    /// </summary>
    /// <param name="referenceMotionVectorIndex">The decoded zero-based dynamic-reference-list index.</param>
    /// <returns>The selected stack candidate, or the nearest fallback when the stack contains one or no entries.</returns>
    public Av1MotionVector GetNewReference(int referenceMotionVectorIndex)
        => this.Count > 1 ? this.candidates[referenceMotionVectorIndex] : this.references[0];

    /// <summary>
    /// Gets the nearest reference for one member of a compound pair.
    /// </summary>
    /// <param name="referenceIndex">Zero for the primary reference or one for the secondary reference.</param>
    /// <returns>The precision-reduced nearest vector.</returns>
    public Av1MotionVector GetCompoundNearestReference(int referenceIndex)
        => referenceIndex == 0 ? this.references[0] : this.compoundReferences[0];

    /// <summary>
    /// Gets the near reference for one member of a compound pair.
    /// </summary>
    /// <param name="referenceMotionVectorIndex">The decoded zero-based dynamic-reference-list index.</param>
    /// <param name="referenceIndex">Zero for the primary reference or one for the secondary reference.</param>
    /// <returns>The precision-reduced near vector.</returns>
    public Av1MotionVector GetCompoundNearReference(int referenceMotionVectorIndex, int referenceIndex)
    {
        if (referenceMotionVectorIndex == 0)
        {
            return referenceIndex == 0 ? this.references[1] : this.compoundReferences[1];
        }

        int candidateIndex = referenceMotionVectorIndex + 1;
        return referenceIndex == 0 ? this.candidates[candidateIndex] : this.compoundCandidates[candidateIndex];
    }

    /// <summary>
    /// Gets the differential reference for one member of a compound pair.
    /// </summary>
    /// <param name="referenceMotionVectorIndex">The selected stack index.</param>
    /// <param name="referenceIndex">Zero for the primary reference or one for the secondary reference.</param>
    /// <returns>The selected raw stack vector.</returns>
    public Av1MotionVector GetCompoundNewReference(int referenceMotionVectorIndex, int referenceIndex)
        => referenceIndex == 0
            ? this.candidates[referenceMotionVectorIndex]
            : this.compoundCandidates[referenceMotionVectorIndex];

    /// <summary>
    /// Scans one spatial row using AV1's block-size-dependent steps and weights.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry and frame-wide mode map.</param>
    /// <param name="referenceFrame">The canonical inter reference selected for the current block.</param>
    /// <param name="secondaryReferenceFrame">The secondary compound reference, or <see cref="Av1ReferenceFrameType.None"/>.</param>
    /// <param name="globalMotion">The selected reference's global-motion model.</param>
    /// <param name="secondaryGlobalMotion">The secondary reference's global-motion model.</param>
    /// <param name="globalMotionVector">The selected reference's global-motion vector at the current block.</param>
    /// <param name="secondaryGlobalMotionVector">The secondary reference's global-motion vector at the current block.</param>
    /// <param name="rowOffset">The signed row offset from the current block in 4x4 units.</param>
    /// <param name="maximumRowOffset">The farthest permitted row offset inside the tile.</param>
    /// <param name="referenceMatchCount">Accumulates matching reference labels found in this scan direction.</param>
    /// <param name="newMotionVectorCount">Accumulates matching neighbors whose inter mode contains a new vector.</param>
    /// <param name="processedRows">Receives the spatial depth covered by block-height weighting.</param>
    private void ScanRow(
        ref Av1PartitionInfo partitionInfo,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame,
        in Av1GlobalMotionParameters globalMotion,
        in Av1GlobalMotionParameters secondaryGlobalMotion,
        Av1MotionVector globalMotionVector,
        Av1MotionVector secondaryGlobalMotionVector,
        int rowOffset,
        int maximumRowOffset,
        ref int referenceMatchCount,
        ref int newMotionVectorCount,
        ref int processedRows)
    {
        int width = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
        int end = Math.Min(partitionInfo.GetMaxBlockWide(partitionInfo.ModeInfo.BlockSize, false), MaximumSearchBlockSize);
        int columnOffset = 0;
        if (Math.Abs(rowOffset) > 1)
        {
            columnOffset = 1;
            if ((partitionInfo.ColumnIndex & 1) != 0 && width < 2)
            {
                columnOffset--;
            }
        }

        // The scan advances by at least one 16x16 mode-info region only when the current block reaches 64 pixels
        // on this axis. Smaller blocks must visit narrow neighbors individually so none of their candidates vanish.
        bool useFourUnitStep = width >= MaximumSearchBlockSize;
        for (int index = 0; index < end;)
        {
            Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(
                new Point(partitionInfo.ColumnIndex + columnOffset + index, partitionInfo.RowIndex + rowOffset));

            int candidateWidth = candidate.BlockSize.Get4x4WideCount();
            int length = Math.Min(width, candidateWidth);
            if (useFourUnitStep)
            {
                length = Math.Max(4, length);
            }
            else if (Math.Abs(rowOffset) > 1)
            {
                length = Math.Max(2, length);
            }

            int weight = 2;
            if (width >= 2 && width <= candidateWidth)
            {
                int increment = Math.Min(-maximumRowOffset + rowOffset + 1, candidate.BlockSize.Get4x4HighCount());
                weight = Math.Max(weight, increment);
                processedRows = increment - rowOffset - 1;
            }

            this.AddCandidate(
                candidate,
                referenceFrame,
                secondaryReferenceFrame,
                in globalMotion,
                in secondaryGlobalMotion,
                globalMotionVector,
                secondaryGlobalMotionVector,
                length * weight,
                ref referenceMatchCount,
                ref newMotionVectorCount);

            index += length;
        }
    }

    /// <summary>
    /// Scans one spatial column using AV1's block-size-dependent steps and weights.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry and frame-wide mode map.</param>
    /// <param name="referenceFrame">The canonical inter reference selected for the current block.</param>
    /// <param name="secondaryReferenceFrame">The secondary compound reference, or <see cref="Av1ReferenceFrameType.None"/>.</param>
    /// <param name="globalMotion">The selected reference's global-motion model.</param>
    /// <param name="secondaryGlobalMotion">The secondary reference's global-motion model.</param>
    /// <param name="globalMotionVector">The selected reference's global-motion vector at the current block.</param>
    /// <param name="secondaryGlobalMotionVector">The secondary reference's global-motion vector at the current block.</param>
    /// <param name="columnOffset">The signed column offset from the current block in 4x4 units.</param>
    /// <param name="maximumColumnOffset">The farthest permitted column offset inside the tile.</param>
    /// <param name="referenceMatchCount">Accumulates matching reference labels found in this scan direction.</param>
    /// <param name="newMotionVectorCount">Accumulates matching neighbors whose inter mode contains a new vector.</param>
    /// <param name="processedColumns">Receives the spatial depth covered by block-width weighting.</param>
    private void ScanColumn(
        ref Av1PartitionInfo partitionInfo,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame,
        in Av1GlobalMotionParameters globalMotion,
        in Av1GlobalMotionParameters secondaryGlobalMotion,
        Av1MotionVector globalMotionVector,
        Av1MotionVector secondaryGlobalMotionVector,
        int columnOffset,
        int maximumColumnOffset,
        ref int referenceMatchCount,
        ref int newMotionVectorCount,
        ref int processedColumns)
    {
        int height = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
        int end = Math.Min(partitionInfo.GetMaxBlockHigh(partitionInfo.ModeInfo.BlockSize, false), MaximumSearchBlockSize);
        int rowOffset = 0;
        if (Math.Abs(columnOffset) > 1)
        {
            rowOffset = 1;
            if ((partitionInfo.RowIndex & 1) != 0 && height < 2)
            {
                rowOffset--;
            }
        }

        // The scan advances by at least one 16x16 mode-info region only when the current block reaches 64 pixels
        // on this axis. Smaller blocks must visit narrow neighbors individually so none of their candidates vanish.
        bool useFourUnitStep = height >= MaximumSearchBlockSize;
        for (int index = 0; index < end;)
        {
            Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(
                new Point(partitionInfo.ColumnIndex + columnOffset, partitionInfo.RowIndex + rowOffset + index));

            int candidateHeight = candidate.BlockSize.Get4x4HighCount();
            int length = Math.Min(height, candidateHeight);
            if (useFourUnitStep)
            {
                length = Math.Max(4, length);
            }
            else if (Math.Abs(columnOffset) > 1)
            {
                length = Math.Max(2, length);
            }

            int weight = 2;
            if (height >= 2 && height <= candidateHeight)
            {
                int increment = Math.Min(-maximumColumnOffset + columnOffset + 1, candidate.BlockSize.Get4x4WideCount());
                weight = Math.Max(weight, increment);
                processedColumns = increment - columnOffset - 1;
            }

            this.AddCandidate(
                candidate,
                referenceFrame,
                secondaryReferenceFrame,
                in globalMotion,
                in secondaryGlobalMotion,
                globalMotionVector,
                secondaryGlobalMotionVector,
                length * weight,
                ref referenceMatchCount,
                ref newMotionVectorCount);

            index += length;
        }
    }

    /// <summary>
    /// Adds the candidate at one tile-relative spatial search position.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry and frame-wide mode map.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="referenceFrame">The canonical inter reference selected for the current block.</param>
    /// <param name="secondaryReferenceFrame">The secondary compound reference, or <see cref="Av1ReferenceFrameType.None"/>.</param>
    /// <param name="globalMotion">The selected reference's global-motion model.</param>
    /// <param name="secondaryGlobalMotion">The secondary reference's global-motion model.</param>
    /// <param name="globalMotionVector">The selected reference's global-motion vector at the current block.</param>
    /// <param name="secondaryGlobalMotionVector">The secondary reference's global-motion vector at the current block.</param>
    /// <param name="rowOffset">The signed row offset from the current block in 4x4 units.</param>
    /// <param name="columnOffset">The signed column offset from the current block in 4x4 units.</param>
    /// <param name="referenceMatchCount">Accumulates matching reference labels at the search position.</param>
    /// <param name="newMotionVectorCount">Accumulates matching neighbors whose inter mode contains a new vector.</param>
    private void AddSpatialBlock(
        ref Av1PartitionInfo partitionInfo,
        Av1TileInfo tileInfo,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame,
        in Av1GlobalMotionParameters globalMotion,
        in Av1GlobalMotionParameters secondaryGlobalMotion,
        Av1MotionVector globalMotionVector,
        Av1MotionVector secondaryGlobalMotionVector,
        int rowOffset,
        int columnOffset,
        ref int referenceMatchCount,
        ref int newMotionVectorCount)
    {
        int row = partitionInfo.RowIndex + rowOffset;
        int column = partitionInfo.ColumnIndex + columnOffset;
        if (row < tileInfo.ModeInfoRowStart || row >= tileInfo.ModeInfoRowEnd ||
            column < tileInfo.ModeInfoColumnStart || column >= tileInfo.ModeInfoColumnEnd)
        {
            return;
        }

        Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column, row));
        this.AddCandidate(
            candidate,
            referenceFrame,
            secondaryReferenceFrame,
            in globalMotion,
            in secondaryGlobalMotion,
            globalMotionVector,
            secondaryGlobalMotionVector,
            4,
            ref referenceMatchCount,
            ref newMotionVectorCount);
    }

    /// <summary>
    /// Accumulates matching references from one decoded inter block.
    /// </summary>
    /// <param name="candidate">The decoded neighboring block.</param>
    /// <param name="referenceFrame">The canonical inter reference selected for the current block.</param>
    /// <param name="secondaryReferenceFrame">The secondary compound reference, or <see cref="Av1ReferenceFrameType.None"/>.</param>
    /// <param name="globalMotion">The selected reference's global-motion model.</param>
    /// <param name="secondaryGlobalMotion">The secondary reference's global-motion model.</param>
    /// <param name="globalMotionVector">The selected reference's global-motion vector at the current block.</param>
    /// <param name="secondaryGlobalMotionVector">The secondary reference's global-motion vector at the current block.</param>
    /// <param name="weight">The spatial weight contributed by each matching reference.</param>
    /// <param name="referenceMatchCount">Accumulates matching reference labels in the active scan direction.</param>
    /// <param name="newMotionVectorCount">Accumulates matching neighbors whose inter mode contains a new vector.</param>
    private void AddCandidate(
        Av1BlockModeInfo candidate,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame,
        in Av1GlobalMotionParameters globalMotion,
        in Av1GlobalMotionParameters secondaryGlobalMotion,
        Av1MotionVector globalMotionVector,
        Av1MotionVector secondaryGlobalMotionVector,
        int weight,
        ref int referenceMatchCount,
        ref int newMotionVectorCount)
    {
        Span<Av1ReferenceFrameType> candidateReferences = candidate.ReferenceFrames;
        if (candidateReferences[0] <= Av1ReferenceFrameType.Intra)
        {
            return;
        }

        Span<Av1MotionVector> candidateMotionVectors = candidate.MotionVectors;
        if (secondaryReferenceFrame > Av1ReferenceFrameType.Intra)
        {
            if (candidateReferences[0] != referenceFrame || candidateReferences[1] != secondaryReferenceFrame)
            {
                return;
            }

            bool usePrimaryGlobalMotion =
                candidate.YMode == Av1PredictionMode.GlobalGlobalMotionVector &&
                globalMotion.Type > Av1GlobalMotionType.Translation &&
                Math.Min(candidate.BlockSize.GetWidth(), candidate.BlockSize.GetHeight()) >= 8;

            bool useSecondaryGlobalMotion =
                candidate.YMode == Av1PredictionMode.GlobalGlobalMotionVector &&
                secondaryGlobalMotion.Type > Av1GlobalMotionType.Translation &&
                Math.Min(candidate.BlockSize.GetWidth(), candidate.BlockSize.GetHeight()) >= 8;

            Av1MotionVector primaryMotionVector = usePrimaryGlobalMotion
                ? globalMotionVector
                : candidateMotionVectors[0];

            Av1MotionVector secondaryMotionVector = useSecondaryGlobalMotion
                ? secondaryGlobalMotionVector
                : candidateMotionVectors[1];

            this.AddUnique(primaryMotionVector, secondaryMotionVector, weight);
            if (UsesNewMotionVector(candidate.YMode))
            {
                newMotionVectorCount++;
            }

            referenceMatchCount++;
            return;
        }

        for (int referenceIndex = 0; referenceIndex < 2; referenceIndex++)
        {
            if (candidateReferences[referenceIndex] != referenceFrame)
            {
                continue;
            }

            // A non-translational global block has no independent translational candidate at the neighbor. AV1
            // therefore evaluates the selected reference's global model at the current block and contributes that
            // vector, but only for blocks large enough to use affine global prediction.
            bool useGlobalMotion =
                (candidate.YMode is Av1PredictionMode.GlobalMotionVector or Av1PredictionMode.GlobalGlobalMotionVector) &&
                globalMotion.Type > Av1GlobalMotionType.Translation &&
                Math.Min(candidate.BlockSize.GetWidth(), candidate.BlockSize.GetHeight()) >= 8;

            Av1MotionVector motionVector = useGlobalMotion
                ? globalMotionVector
                : candidateMotionVectors[referenceIndex];

            this.AddUnique(motionVector, weight);

            // Every matching reference in a neighbor carrying a NEW component contributes to the adjacent NEWMV
            // context even when its vector deduplicates against an earlier stack entry.
            if (UsesNewMotionVector(candidate.YMode))
            {
                newMotionVectorCount++;
            }

            referenceMatchCount++;
        }
    }

    /// <summary>
    /// Adds projected temporal candidates over the current block and its permitted extension positions.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="frameInfo">The projected temporal motion field.</param>
    /// <param name="orderHintInfo">The sequence modulo order-hint configuration.</param>
    /// <param name="frameHeader">The frame-level motion-vector precision configuration.</param>
    /// <param name="referenceFrame">The canonical inter reference selected for the current block.</param>
    /// <param name="secondaryReferenceFrame">The secondary compound reference, or <see cref="Av1ReferenceFrameType.None"/>.</param>
    /// <param name="globalMotionVector">The selected reference's global-motion vector at the current block.</param>
    /// <param name="secondaryGlobalMotionVector">The secondary reference's global-motion vector at the current block.</param>
    private void AddTemporalCandidates(
        ref Av1PartitionInfo partitionInfo,
        Av1TileInfo tileInfo,
        Av1FrameInfo frameInfo,
        ObuOrderHintInfo orderHintInfo,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame,
        Av1MotionVector globalMotionVector,
        Av1MotionVector secondaryGlobalMotionVector)
    {
        int width = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
        int height = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
        int verticalOffset = Math.Max(2, height);
        int horizontalOffset = Math.Max(2, width);
        int blockRowEnd = Math.Min(height, MaximumSearchBlockSize);
        int blockColumnEnd = Math.Min(width, MaximumSearchBlockSize);
        int rowStep = height >= MaximumSearchBlockSize ? 4 : 2;
        int columnStep = width >= MaximumSearchBlockSize ? 4 : 2;
        bool firstSampleAvailable = false;

        for (int blockRow = 0; blockRow < blockRowEnd; blockRow += rowStep)
        {
            for (int blockColumn = 0; blockColumn < blockColumnEnd; blockColumn += columnStep)
            {
                bool available = this.AddTemporalCandidate(
                    ref partitionInfo,
                    tileInfo,
                    frameInfo,
                    orderHintInfo,
                    frameHeader,
                    referenceFrame,
                    secondaryReferenceFrame,
                    globalMotionVector,
                    secondaryGlobalMotionVector,
                    blockRow,
                    blockColumn);

                if (blockRow == 0 && blockColumn == 0)
                {
                    firstSampleAvailable = available;
                }
            }
        }

        if (!firstSampleAvailable)
        {
            this.ModeContext |= GlobalMotionContextBit;
        }

        bool allowExtension = height >= 2 && height < MaximumSearchBlockSize && width >= 2 && width < MaximumSearchBlockSize;
        if (!allowExtension)
        {
            return;
        }

        // These three positions extend the temporal search below-left, below-right, and above-right. The 64x64
        // boundary test is normative even when the sequence uses 128x128 superblocks.
        this.AddTemporalExtension(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            orderHintInfo,
            frameHeader,
            referenceFrame,
            secondaryReferenceFrame,
            globalMotionVector,
            secondaryGlobalMotionVector,
            verticalOffset,
            -2);

        this.AddTemporalExtension(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            orderHintInfo,
            frameHeader,
            referenceFrame,
            secondaryReferenceFrame,
            globalMotionVector,
            secondaryGlobalMotionVector,
            verticalOffset,
            horizontalOffset);

        this.AddTemporalExtension(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            orderHintInfo,
            frameHeader,
            referenceFrame,
            secondaryReferenceFrame,
            globalMotionVector,
            secondaryGlobalMotionVector,
            verticalOffset - 2,
            horizontalOffset);
    }

    /// <summary>
    /// Adds one optional temporal extension candidate after applying the normative 64x64 boundary rule.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="frameInfo">The projected temporal motion field.</param>
    /// <param name="orderHintInfo">The sequence modulo order-hint configuration.</param>
    /// <param name="frameHeader">The frame-level motion-vector precision configuration.</param>
    /// <param name="referenceFrame">The canonical inter reference selected for the current block.</param>
    /// <param name="secondaryReferenceFrame">The secondary compound reference, or <see cref="Av1ReferenceFrameType.None"/>.</param>
    /// <param name="globalMotionVector">The selected reference's global-motion vector at the current block.</param>
    /// <param name="secondaryGlobalMotionVector">The secondary reference's global-motion vector at the current block.</param>
    /// <param name="blockRow">The temporal sample row relative to the current block in 4x4 units.</param>
    /// <param name="blockColumn">The temporal sample column relative to the current block in 4x4 units.</param>
    private void AddTemporalExtension(
        ref Av1PartitionInfo partitionInfo,
        Av1TileInfo tileInfo,
        Av1FrameInfo frameInfo,
        ObuOrderHintInfo orderHintInfo,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame,
        Av1MotionVector globalMotionVector,
        Av1MotionVector secondaryGlobalMotionVector,
        int blockRow,
        int blockColumn)
    {
        int rowWithinBlock64 = partitionInfo.RowIndex & (MaximumSearchBlockSize - 1);
        int columnWithinBlock64 = partitionInfo.ColumnIndex & (MaximumSearchBlockSize - 1);
        if (rowWithinBlock64 + blockRow < 0 || rowWithinBlock64 + blockRow >= MaximumSearchBlockSize ||
            columnWithinBlock64 + blockColumn < 0 || columnWithinBlock64 + blockColumn >= MaximumSearchBlockSize)
        {
            return;
        }

        _ = this.AddTemporalCandidate(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            orderHintInfo,
            frameHeader,
            referenceFrame,
            secondaryReferenceFrame,
            globalMotionVector,
            secondaryGlobalMotionVector,
            blockRow,
            blockColumn);
    }

    /// <summary>
    /// Projects and accumulates one temporal motion-field sample.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="frameInfo">The projected temporal motion field.</param>
    /// <param name="orderHintInfo">The sequence modulo order-hint configuration.</param>
    /// <param name="frameHeader">The frame-level motion-vector precision configuration.</param>
    /// <param name="referenceFrame">The canonical inter reference selected for the current block.</param>
    /// <param name="secondaryReferenceFrame">The secondary compound reference, or <see cref="Av1ReferenceFrameType.None"/>.</param>
    /// <param name="globalMotionVector">The selected reference's global-motion vector at the current block.</param>
    /// <param name="secondaryGlobalMotionVector">The secondary reference's global-motion vector at the current block.</param>
    /// <param name="blockRow">The temporal sample row relative to the current block in 4x4 units.</param>
    /// <param name="blockColumn">The temporal sample column relative to the current block in 4x4 units.</param>
    /// <returns><see langword="true"/> when the projected motion field covers the requested position.</returns>
    private bool AddTemporalCandidate(
        ref Av1PartitionInfo partitionInfo,
        Av1TileInfo tileInfo,
        Av1FrameInfo frameInfo,
        ObuOrderHintInfo orderHintInfo,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame,
        Av1MotionVector globalMotionVector,
        Av1MotionVector secondaryGlobalMotionVector,
        int blockRow,
        int blockColumn)
    {
        int rowOffset = (partitionInfo.RowIndex & 1) != 0 ? blockRow : blockRow + 1;
        int columnOffset = (partitionInfo.ColumnIndex & 1) != 0 ? blockColumn : blockColumn + 1;
        int row = partitionInfo.RowIndex + rowOffset;
        int column = partitionInfo.ColumnIndex + columnOffset;
        if (row < tileInfo.ModeInfoRowStart || row >= tileInfo.ModeInfoRowEnd ||
            column < tileInfo.ModeInfoColumnStart || column >= tileInfo.ModeInfoColumnEnd)
        {
            return false;
        }

        if (!frameInfo.TryGetProjectedTemporalMotionVector(
            row,
            column,
            referenceFrame,
            orderHintInfo,
            frameHeader.AllowHighPrecisionMotionVector,
            frameHeader.ForceIntegerMotionVector,
            out Av1MotionVector motionVector))
        {
            return false;
        }

        Av1MotionVector secondaryMotionVector = default;
        if (secondaryReferenceFrame > Av1ReferenceFrameType.Intra &&
            !frameInfo.TryGetProjectedTemporalMotionVector(
                row,
                column,
                secondaryReferenceFrame,
                orderHintInfo,
                frameHeader.AllowHighPrecisionMotionVector,
                frameHeader.ForceIntegerMotionVector,
                out secondaryMotionVector))
        {
            return false;
        }

        if (blockRow == 0 && blockColumn == 0 &&
            (Math.Abs(motionVector.Row - globalMotionVector.Row) >= 16 ||
             Math.Abs(motionVector.Column - globalMotionVector.Column) >= 16 ||
             (secondaryReferenceFrame > Av1ReferenceFrameType.Intra &&
              (Math.Abs(secondaryMotionVector.Row - secondaryGlobalMotionVector.Row) >= 16 ||
               Math.Abs(secondaryMotionVector.Column - secondaryGlobalMotionVector.Column) >= 16))))
        {
            // The packed global-motion context records whether the first temporal sample is absent or differs from
            // global motion by at least two full samples in either one-eighth-sample component.
            this.ModeContext |= GlobalMotionContextBit;
        }

        if (secondaryReferenceFrame > Av1ReferenceFrameType.Intra)
        {
            this.AddUnique(motionVector, secondaryMotionVector, 2);
        }
        else
        {
            this.AddUnique(motionVector, 2);
        }

        return true;
    }

    /// <summary>
    /// Extends a short stack with inter vectors from a neighboring block, correcting their temporal direction.
    /// </summary>
    /// <param name="candidate">The decoded neighboring block.</param>
    /// <param name="frameInfo">The reference-side classification for the current frame.</param>
    /// <param name="referenceFrame">The canonical inter reference selected for the current block.</param>
    private void AddExtensionCandidate(
        Av1BlockModeInfo candidate,
        Av1FrameInfo frameInfo,
        Av1ReferenceFrameType referenceFrame)
    {
        Span<Av1ReferenceFrameType> candidateReferences = candidate.ReferenceFrames;
        Span<Av1MotionVector> candidateMotionVectors = candidate.MotionVectors;
        bool targetSignBias = frameInfo.IsReferenceSignBiased(referenceFrame);

        for (int referenceIndex = 0; referenceIndex < 2; referenceIndex++)
        {
            Av1ReferenceFrameType candidateReference = candidateReferences[referenceIndex];
            if (candidateReference <= Av1ReferenceFrameType.Intra)
            {
                continue;
            }

            Av1MotionVector motionVector = candidateMotionVectors[referenceIndex];
            if (frameInfo.IsReferenceSignBiased(candidateReference) != targetSignBias)
            {
                motionVector = new Av1MotionVector(-motionVector.Row, -motionVector.Column);
            }

            int candidateIndex;
            for (candidateIndex = 0; candidateIndex < this.Count; candidateIndex++)
            {
                if (this.candidates[candidateIndex] == motionVector)
                {
                    break;
                }
            }

            if (candidateIndex == this.Count && this.Count < CandidateCapacity)
            {
                // AV1's outer spatial extension only initializes a new stack entry. Unlike the weighted nearest and
                // temporal scans, finding an existing vector here must not change its previously accumulated rank.
                this.candidates[this.Count] = motionVector;
                this.weights[this.Count] = 2;
                this.Count++;
            }
        }
    }

    /// <summary>
    /// Extends a short compound stack from the immediate above and left blocks.
    /// </summary>
    private void ExtendCompoundStack(
        ref Av1PartitionInfo partitionInfo,
        Av1FrameInfo frameInfo,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame,
        Av1MotionVector globalMotionVector,
        Av1MotionVector secondaryGlobalMotionVector,
        bool hasAbove,
        bool hasLeft,
        int extensionLength)
    {
        InlineArray2<Av1MotionVector> primaryExact = default;
        InlineArray2<Av1MotionVector> secondaryExact = default;
        InlineArray2<Av1MotionVector> primaryDifferent = default;
        InlineArray2<Av1MotionVector> secondaryDifferent = default;
        int primaryExactCount = 0;
        int secondaryExactCount = 0;
        int primaryDifferentCount = 0;
        int secondaryDifferentCount = 0;
        int row = partitionInfo.RowIndex;
        int column = partitionInfo.ColumnIndex;

        for (int index = 0; hasAbove && index < extensionLength;)
        {
            Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column + index, row - 1));
            CollectCompoundExtensionCandidate(
                candidate,
                frameInfo,
                referenceFrame,
                secondaryReferenceFrame,
                ref primaryExact,
                ref primaryExactCount,
                ref secondaryExact,
                ref secondaryExactCount,
                ref primaryDifferent,
                ref primaryDifferentCount,
                ref secondaryDifferent,
                ref secondaryDifferentCount);

            index += candidate.BlockSize.Get4x4WideCount();
        }

        for (int index = 0; hasLeft && index < extensionLength;)
        {
            Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column - 1, row + index));
            CollectCompoundExtensionCandidate(
                candidate,
                frameInfo,
                referenceFrame,
                secondaryReferenceFrame,
                ref primaryExact,
                ref primaryExactCount,
                ref secondaryExact,
                ref secondaryExactCount,
                ref primaryDifferent,
                ref primaryDifferentCount,
                ref secondaryDifferent,
                ref secondaryDifferentCount);

            index += candidate.BlockSize.Get4x4HighCount();
        }

        InlineArray2<Av1MotionVector> primaryList = BuildCompoundExtensionList(
            in primaryExact,
            primaryExactCount,
            in primaryDifferent,
            primaryDifferentCount,
            globalMotionVector);

        InlineArray2<Av1MotionVector> secondaryList = BuildCompoundExtensionList(
            in secondaryExact,
            secondaryExactCount,
            in secondaryDifferent,
            secondaryDifferentCount,
            secondaryGlobalMotionVector);

        if (this.Count == 1)
        {
            int listIndex = primaryList[0] == this.candidates[0] && secondaryList[0] == this.compoundCandidates[0] ? 1 : 0;
            this.candidates[1] = primaryList[listIndex];
            this.compoundCandidates[1] = secondaryList[listIndex];
            this.weights[1] = 2;
            this.Count = 2;
            return;
        }

        // The fallback list is positional rather than a weighted candidate scan. Preserve both entries even when
        // they are equal so the derived DRL indices retain the same meaning.
        for (int index = 0; index < 2; index++)
        {
            this.candidates[index] = primaryList[index];
            this.compoundCandidates[index] = secondaryList[index];
            this.weights[index] = 2;
        }

        this.Count = 2;
    }

    /// <summary>
    /// Collects exact-reference and temporal-direction-corrected fallback vectors from one neighboring block.
    /// </summary>
    private static void CollectCompoundExtensionCandidate(
        Av1BlockModeInfo candidate,
        Av1FrameInfo frameInfo,
        Av1ReferenceFrameType referenceFrame,
        Av1ReferenceFrameType secondaryReferenceFrame,
        ref InlineArray2<Av1MotionVector> primaryExact,
        ref int primaryExactCount,
        ref InlineArray2<Av1MotionVector> secondaryExact,
        ref int secondaryExactCount,
        ref InlineArray2<Av1MotionVector> primaryDifferent,
        ref int primaryDifferentCount,
        ref InlineArray2<Av1MotionVector> secondaryDifferent,
        ref int secondaryDifferentCount)
    {
        Span<Av1ReferenceFrameType> candidateReferences = candidate.ReferenceFrames;
        Span<Av1MotionVector> candidateMotionVectors = candidate.MotionVectors;

        for (int candidateIndex = 0; candidateIndex < 2; candidateIndex++)
        {
            Av1ReferenceFrameType candidateReference = candidateReferences[candidateIndex];
            Av1MotionVector candidateMotionVector = candidateMotionVectors[candidateIndex];

            for (int targetIndex = 0; targetIndex < 2; targetIndex++)
            {
                Av1ReferenceFrameType targetReference = targetIndex == 0 ? referenceFrame : secondaryReferenceFrame;
                if (candidateReference == targetReference)
                {
                    ref int exactCount = ref (targetIndex == 0 ? ref primaryExactCount : ref secondaryExactCount);
                    if (exactCount < 2)
                    {
                        if (targetIndex == 0)
                        {
                            primaryExact[exactCount] = candidateMotionVector;
                        }
                        else
                        {
                            secondaryExact[exactCount] = candidateMotionVector;
                        }

                        exactCount++;
                        continue;
                    }
                }

                if (candidateReference <= Av1ReferenceFrameType.Intra)
                {
                    continue;
                }

                ref int differentCount = ref (targetIndex == 0 ? ref primaryDifferentCount : ref secondaryDifferentCount);
                if (differentCount >= 2)
                {
                    continue;
                }

                Av1MotionVector differentMotionVector = candidateMotionVector;
                if (frameInfo.IsReferenceSignBiased(candidateReference) != frameInfo.IsReferenceSignBiased(targetReference))
                {
                    differentMotionVector = new Av1MotionVector(-differentMotionVector.Row, -differentMotionVector.Column);
                }

                if (targetIndex == 0)
                {
                    primaryDifferent[differentCount] = differentMotionVector;
                }
                else
                {
                    secondaryDifferent[differentCount] = differentMotionVector;
                }

                differentCount++;
            }
        }
    }

    /// <summary>
    /// Builds the two positional fallback entries for one member of a compound reference pair.
    /// </summary>
    private static InlineArray2<Av1MotionVector> BuildCompoundExtensionList(
        in InlineArray2<Av1MotionVector> exact,
        int exactCount,
        in InlineArray2<Av1MotionVector> different,
        int differentCount,
        Av1MotionVector globalMotionVector)
    {
        InlineArray2<Av1MotionVector> result = default;
        int resultCount = 0;

        for (int index = 0; index < exactCount && resultCount < 2; index++)
        {
            result[resultCount++] = exact[index];
        }

        for (int index = 0; index < differentCount && resultCount < 2; index++)
        {
            result[resultCount++] = different[index];
        }

        while (resultCount < 2)
        {
            result[resultCount++] = globalMotionVector;
        }

        return result;
    }

    /// <summary>
    /// Adds a unique candidate or accumulates the weight of an existing candidate.
    /// </summary>
    /// <param name="motionVector">The candidate vector in one-eighth-sample units.</param>
    /// <param name="weight">The spatial or temporal weight contributed by this occurrence.</param>
    private void AddUnique(Av1MotionVector motionVector, int weight)
    {
        for (int index = 0; index < this.Count; index++)
        {
            if (this.candidates[index] == motionVector)
            {
                this.weights[index] += (ushort)weight;
                return;
            }
        }

        if (this.Count < CandidateCapacity)
        {
            this.candidates[this.Count] = motionVector;
            this.weights[this.Count] = (ushort)weight;
            this.Count++;
        }
    }

    /// <summary>
    /// Adds a unique compound candidate or accumulates the weight of an existing vector pair.
    /// </summary>
    private void AddUnique(Av1MotionVector motionVector, Av1MotionVector compoundMotionVector, int weight)
    {
        for (int index = 0; index < this.Count; index++)
        {
            if (this.candidates[index] == motionVector && this.compoundCandidates[index] == compoundMotionVector)
            {
                this.weights[index] += (ushort)weight;
                return;
            }
        }

        if (this.Count < CandidateCapacity)
        {
            this.candidates[this.Count] = motionVector;
            this.compoundCandidates[this.Count] = compoundMotionVector;
            this.weights[this.Count] = (ushort)weight;
            this.Count++;
        }
    }

    /// <summary>
    /// Sorts one candidate region by descending accumulated weight while retaining scan order for equal weights.
    /// </summary>
    /// <param name="start">The inclusive first candidate index in the region.</param>
    /// <param name="end">The exclusive end candidate index in the region.</param>
    private void SortByWeight(int start, int end)
    {
        int length = end;
        while (length > start)
        {
            int lastSwap = start;
            for (int index = start + 1; index < length; index++)
            {
                if (this.weights[index - 1] < this.weights[index])
                {
                    Av1MotionVector candidate = this.candidates[index - 1];
                    this.candidates[index - 1] = this.candidates[index];
                    this.candidates[index] = candidate;

                    Av1MotionVector compoundCandidate = this.compoundCandidates[index - 1];
                    this.compoundCandidates[index - 1] = this.compoundCandidates[index];
                    this.compoundCandidates[index] = compoundCandidate;

                    ushort weight = this.weights[index - 1];
                    this.weights[index - 1] = this.weights[index];
                    this.weights[index] = weight;
                    lastSwap = index;
                }
            }

            length = lastSwap;
        }
    }

    /// <summary>
    /// Determines whether an inter mode decodes at least one new motion-vector component.
    /// </summary>
    private static bool UsesNewMotionVector(Av1PredictionMode mode)
        => mode is Av1PredictionMode.NewMotionVector or
            Av1PredictionMode.NewNewMotionVector or
            Av1PredictionMode.NearestNewMotionVector or
            Av1PredictionMode.NewNearestMotionVector or
            Av1PredictionMode.NearNewMotionVector or
            Av1PredictionMode.NewNearMotionVector;

    /// <summary>
    /// Provides fixed storage for AV1's eight reference-motion-vector candidates.
    /// </summary>
    /// <typeparam name="T">The motion-vector or weight type stored in the inline buffer.</typeparam>
    [InlineArray(CandidateCapacity)]
    private struct InlineArray8<T>
    {
        /// <summary>
        /// The first element in the compiler-expanded inline buffer.
        /// </summary>
        private T element;
    }

    /// <summary>
    /// Provides fixed storage for the nearest and near motion-vector references.
    /// </summary>
    /// <typeparam name="T">The motion-vector type stored in the inline buffer.</typeparam>
    [InlineArray(2)]
    private struct InlineArray2<T>
    {
        /// <summary>
        /// The first element in the compiler-expanded inline buffer.
        /// </summary>
        private T element;
    }
}
