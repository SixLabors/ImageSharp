// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the retained and projected per-8x8 motion fields associated with a decoded AV1 frame.
/// </summary>
internal partial class Av1FrameInfo
{
    /// <summary>
    /// The maximum number of reference frames projected into one temporal motion field.
    /// </summary>
    private const int MotionFieldProjectionCount = 3;

    /// <summary>
    /// The maximum source motion-vector magnitude retained for later temporal projection.
    /// </summary>
    private const int ReferenceMotionVectorLimit = 4095;

    /// <summary>
    /// The width or height of the largest AV1 superblock in 4x4 mode-information units.
    /// </summary>
    private const int MaximumSuperblockModeInfoSize = 1 << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2);

    /// <summary>
    /// The base-two logarithm of <see cref="MaximumSuperblockModeInfoSize"/>.
    /// </summary>
    private const int MaximumSuperblockModeInfoSizeLog2 = Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2;

    /// <summary>
    /// The base-two reduction from 4x4 mode-information coordinates to the 8x8 motion-field grid.
    /// </summary>
    private const int MotionFieldModeInfoShift = 1;

    /// <summary>
    /// The base-two reduction from one-eighth-sample motion vectors to offsets on the 8x8 motion-field grid.
    /// </summary>
    private const int MotionVectorToFieldOffsetShift = 4 + Av1Constants.ModeInfoSizeLog2;

    /// <summary>
    /// The maximum horizontal projection displacement, measured in 8x8 motion-field blocks.
    /// </summary>
    private const int MaximumHorizontalFieldOffset = 8;

    /// <summary>
    /// Owns the selected motion vector and logical reference for every retained 8x8 frame position.
    /// </summary>
    private MotionFieldStorage<RetainedMotionFieldEntry>? retainedMotionField;

    /// <summary>
    /// Owns motion vectors projected from retained frames into the current frame's 8x8 grid.
    /// </summary>
    private MotionFieldStorage<TemporalMotionFieldEntry>? temporalMotionField;

    /// <summary>
    /// The compact reference state detached from reconstruction storage after this frame refreshes the reference map.
    /// </summary>
    private ReferenceState? referenceState;

    /// <summary>
    /// The number of tile-reader and decoder-result owners retaining this reconstruction state.
    /// </summary>
    private int ownerCount = 1;

    /// <summary>
    /// Indicates whether this frame state still owns the initial lease created with the instance.
    /// </summary>
    private bool ownsInitialLease = true;

    /// <summary>
    /// Stores the order hint selected by each logical inter-reference type for later projections from this frame.
    /// </summary>
    private InlineArray8<uint> motionFieldReferenceOrderHints;

    /// <summary>
    /// Stores whether each logical inter-reference type lies after, at, or before the current frame in display order.
    /// </summary>
    private InlineArray8<sbyte> motionFieldReferenceSides;

    /// <summary>
    /// The order hint of the current frame represented by this mode-information owner.
    /// </summary>
    private uint motionFieldOrderHint;

    /// <summary>
    /// The active frame width in 4x4 mode-information units.
    /// </summary>
    private int activeModeInfoColumnCount;

    /// <summary>
    /// The active frame height in 4x4 mode-information units.
    /// </summary>
    private int activeModeInfoRowCount;

    /// <summary>
    /// Allocates and derives the motion fields required by one decoded frame.
    /// </summary>
    /// <param name="configuration">The decoder configuration providing motion-field storage.</param>
    /// <param name="sequenceHeader">The sequence header defining motion-field enablement and order-hint precision.</param>
    /// <param name="frameHeader">The current frame header and its seven resolved inter-reference roles.</param>
    /// <param name="referenceFrames">The retained reconstructed frames selected by the current reference map.</param>
    public void InitializeMotionField(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameStore referenceFrames)
    {
        if (!sequenceHeader.OrderHintInfo.EnableOrderHint)
        {
            return;
        }

        this.motionFieldOrderHint = frameHeader.OrderHint;
        this.activeModeInfoColumnCount = frameHeader.ModeInfoColumnCount;
        this.activeModeInfoRowCount = frameHeader.ModeInfoRowCount;
        int retainedMotionFieldStride = (this.activeModeInfoColumnCount + 1) >> MotionFieldModeInfoShift;

        if (frameHeader.IsIntra)
        {
            // Intra frames can occupy reference slots, but contain no inter motion to project. Source selection
            // rejects them before consulting their motion fields or reference-order hints.
            return;
        }

        ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        SelectedReferenceFrames selectedReferences = new(referenceFrames, referenceFrameIndices);
        ObuOrderHintInfo orderHintInfo = sequenceHeader.OrderHintInfo;

        // Capture the seven logical-role order hints before refreshing physical map slots. Later projection needs
        // each vector's original source-to-reference distance, even after those slots hold different frames.
        for (int referenceIndex = 0; referenceIndex < Av1Constants.ReferencesPerFrame; referenceIndex++)
        {
            Av1ReferenceFrameType referenceFrameType = (Av1ReferenceFrameType)(referenceIndex + 1);
            Av1ReferenceFrame referenceFrame = selectedReferences[referenceFrameType];
            uint referenceOrderHint = referenceFrame.FrameHeader.OrderHint;
            this.motionFieldReferenceOrderHints[(int)referenceFrameType] = referenceOrderHint;

            int relativeDistance = orderHintInfo.GetRelativeDistance(referenceOrderHint, frameHeader.OrderHint);
            this.motionFieldReferenceSides[(int)referenceFrameType] = relativeDistance > 0
                ? (sbyte)1
                : referenceOrderHint == frameHeader.OrderHint ? (sbyte)-1 : (sbyte)0;
        }

        if (!sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors)
        {
            // Spatial reference extension still needs the sign classification above when temporal motion fields are
            // disabled. Retained and projected 8x8 storage belongs exclusively to the temporal-motion-vector tool.
            return;
        }

        // The retained field transfers to compact reference state after reconstruction. Keeping it allocator-backed
        // avoids placing a frame-sized array on the managed heap. Clean storage is required because an all-zero entry
        // denotes the normative empty field.
        int retainedRowCount = (this.activeModeInfoRowCount + 1) >> MotionFieldModeInfoShift;
        IMemoryOwner<RetainedMotionFieldEntry> retainedMotionFieldOwner =
            configuration.MemoryAllocator.Allocate<RetainedMotionFieldEntry>(
                retainedMotionFieldStride * retainedRowCount,
                AllocationOptions.Clean);

        this.retainedMotionField = new(retainedMotionFieldOwner, retainedMotionFieldStride);

        if (!frameHeader.UseReferenceFrameMotionVectors)
        {
            return;
        }

        // Align projected rows to 128 samples so temporal-candidate addressing uses the same grid for both
        // superblock sizes. The extra rows below the active frame accommodate the projection window.
        int alignedModeInfoColumnCount = Av1Math.AlignPowerOf2(
            this.activeModeInfoColumnCount,
            MaximumSuperblockModeInfoSizeLog2);

        int temporalMotionFieldStride = alignedModeInfoColumnCount >> MotionFieldModeInfoShift;
        int temporalRowCount = (this.activeModeInfoRowCount + MaximumSuperblockModeInfoSize) >> MotionFieldModeInfoShift;
        IMemoryOwner<TemporalMotionFieldEntry> temporalMotionFieldOwner =
            configuration.MemoryAllocator.Allocate<TemporalMotionFieldEntry>(
                temporalMotionFieldStride * temporalRowCount,
                AllocationOptions.Clean);

        MotionFieldStorage<TemporalMotionFieldEntry> temporalMotionField =
            new(temporalMotionFieldOwner, temporalMotionFieldStride);

        this.temporalMotionField = temporalMotionField;

        // AV1 examines LAST, BWDREF, ALTREF2, ALTREF, and LAST2 in this normative order and admits at most three
        // projection sources. LAST always consumes the first budget position, forward references consume one only
        // when eligible projection succeeds, and LAST2 fills the final unused position in the reverse direction.
        int remainingProjectionCount = MotionFieldProjectionCount;
        Av1ReferenceFrame lastFrame = selectedReferences[Av1ReferenceFrameType.Last];
        Av1ReferenceFrame goldenFrame = selectedReferences[Av1ReferenceFrameType.Golden];
        uint alternateOfLastOrderHint =
            lastFrame.ReferenceState.MotionFieldReferenceOrderHints[(int)Av1ReferenceFrameType.Alternate];

        // A LAST frame whose ALTREF order matches GOLDEN is an overlay. Projecting it would duplicate the overlay's
        // temporal source. LAST consumes its position in the three-source projection budget even when omitted.
        if (alternateOfLastOrderHint != goldenFrame.FrameHeader.OrderHint)
        {
            _ = this.ProjectMotionField(sequenceHeader, frameHeader, lastFrame, temporalMotionField, reverseDirection: true);
        }

        remainingProjectionCount--;
        Av1ReferenceFrame backwardFrame = selectedReferences[Av1ReferenceFrameType.Backward];

        if (orderHintInfo.GetRelativeDistance(backwardFrame.FrameHeader.OrderHint, frameHeader.OrderHint) > 0 &&
            this.ProjectMotionField(sequenceHeader, frameHeader, backwardFrame, temporalMotionField, reverseDirection: false))
        {
            remainingProjectionCount--;
        }

        Av1ReferenceFrame alternate2Frame = selectedReferences[Av1ReferenceFrameType.Alternate2];

        if (orderHintInfo.GetRelativeDistance(alternate2Frame.FrameHeader.OrderHint, frameHeader.OrderHint) > 0 &&
            this.ProjectMotionField(sequenceHeader, frameHeader, alternate2Frame, temporalMotionField, reverseDirection: false))
        {
            remainingProjectionCount--;
        }

        Av1ReferenceFrame alternateFrame = selectedReferences[Av1ReferenceFrameType.Alternate];

        if (remainingProjectionCount > 0 &&
            orderHintInfo.GetRelativeDistance(alternateFrame.FrameHeader.OrderHint, frameHeader.OrderHint) > 0 &&
            this.ProjectMotionField(sequenceHeader, frameHeader, alternateFrame, temporalMotionField, reverseDirection: false))
        {
            remainingProjectionCount--;
        }

        if (remainingProjectionCount > 0)
        {
            Av1ReferenceFrame last2Frame = selectedReferences[Av1ReferenceFrameType.Last2];
            _ = this.ProjectMotionField(sequenceHeader, frameHeader, last2Frame, temporalMotionField, reverseDirection: true);
        }
    }

    /// <summary>
    /// Gets the temporal motion vector projected over a 4x4 mode-information position.
    /// </summary>
    /// <param name="modeInfoRow">The zero-based 4x4 row.</param>
    /// <param name="modeInfoColumn">The zero-based 4x4 column.</param>
    /// <param name="motionVector">Receives the retained source vector in one-eighth-sample units.</param>
    /// <param name="referenceFrameOffset">Receives the positive temporal distance from the source to its reference.</param>
    /// <returns><see langword="true"/> when a projected vector covers the requested position.</returns>
    public bool TryGetTemporalMotionVector(
        int modeInfoRow,
        int modeInfoColumn,
        out Av1MotionVector motionVector,
        out int referenceFrameOffset)
    {
        MotionFieldStorage<TemporalMotionFieldEntry>? temporalMotionFieldState = this.temporalMotionField;
        if (temporalMotionFieldState is null)
        {
            motionVector = default;
            referenceFrameOffset = 0;
            return false;
        }

        MotionFieldStorage<TemporalMotionFieldEntry> temporalMotionField = temporalMotionFieldState.Value;
        int index = ((modeInfoRow >> MotionFieldModeInfoShift) * temporalMotionField.Stride) +
            (modeInfoColumn >> MotionFieldModeInfoShift);

        TemporalMotionFieldEntry entry = temporalMotionField.Owner.Memory.Span[index];
        motionVector = entry.MotionVector;
        referenceFrameOffset = entry.ReferenceFrameOffset;
        return referenceFrameOffset > 0;
    }

    /// <summary>
    /// Gets a value indicating whether a canonical inter reference has positive AV1 sign bias.
    /// </summary>
    /// <param name="referenceFrame">The canonical inter-reference role.</param>
    /// <returns><see langword="true"/> for a future reference; otherwise, <see langword="false"/>.</returns>
    public bool IsReferenceSignBiased(Av1ReferenceFrameType referenceFrame) => this.motionFieldReferenceSides[(int)referenceFrame] > 0;

    /// <summary>
    /// Gets a temporal motion-field entry projected to a selected canonical inter reference.
    /// </summary>
    /// <param name="modeInfoRow">The zero-based 4x4 row.</param>
    /// <param name="modeInfoColumn">The zero-based 4x4 column.</param>
    /// <param name="referenceFrame">The canonical inter-reference role targeted by the candidate.</param>
    /// <param name="orderHintInfo">The sequence modulo order-hint configuration.</param>
    /// <param name="allowHighPrecision">A value indicating whether one-eighth-sample precision may be retained.</param>
    /// <param name="forceInteger">A value indicating whether integer-sample precision is required.</param>
    /// <param name="motionVector">Receives the projected and precision-reduced candidate.</param>
    /// <returns><see langword="true"/> when a temporal entry covers the requested position.</returns>
    public bool TryGetProjectedTemporalMotionVector(
        int modeInfoRow,
        int modeInfoColumn,
        Av1ReferenceFrameType referenceFrame,
        ObuOrderHintInfo orderHintInfo,
        bool allowHighPrecision,
        bool forceInteger,
        out Av1MotionVector motionVector)
    {
        if (!this.TryGetTemporalMotionVector(
            modeInfoRow,
            modeInfoColumn,
            out Av1MotionVector sourceMotionVector,
            out int sourceReferenceOffset))
        {
            motionVector = default;
            return false;
        }

        int targetReferenceOffset = orderHintInfo.GetRelativeDistance(
            this.motionFieldOrderHint,
            this.motionFieldReferenceOrderHints[(int)referenceFrame]);

        // The projected field retains the source frame's original vector and its source-to-reference distance.
        // Candidate construction therefore applies the second normative ratio for the current frame's selected role.
        motionVector = sourceMotionVector
            .ProjectTemporal(targetReferenceOffset, sourceReferenceOffset)
            .LowerPrecision(allowHighPrecision, forceInteger);

        return true;
    }

    /// <summary>
    /// Writes the retained per-8x8 motion-field entries covered by one completed mode-information block.
    /// </summary>
    /// <param name="modeInfo">The completed block mode information.</param>
    /// <param name="modeInfoPosition">The block origin in frame-relative 4x4 units.</param>
    private void UpdateRetainedMotionField(Av1BlockModeInfo modeInfo, Point modeInfoPosition)
    {
        MotionFieldStorage<RetainedMotionFieldEntry>? retainedMotionFieldState = this.retainedMotionField;
        if (retainedMotionFieldState is null)
        {
            return;
        }

        MotionFieldStorage<RetainedMotionFieldEntry> retainedMotionField = retainedMotionFieldState.Value;
        Span<RetainedMotionFieldEntry> retainedEntries = retainedMotionField.Owner.Memory.Span;

        Av1ReferenceFrameType selectedReference = Av1ReferenceFrameType.None;
        Av1MotionVector selectedMotionVector = default;
        Span<Av1ReferenceFrameType> referenceFrames = modeInfo.ReferenceFrames;
        Span<Av1MotionVector> motionVectors = modeInfo.MotionVectors;

        // A compound block can supply two vectors to one retained cell. Visit both in coded order so the last
        // eligible past-reference vector wins; same-order, future, and out-of-range vectors cannot replace it.
        for (int referenceIndex = 0; referenceIndex < 2; referenceIndex++)
        {
            Av1ReferenceFrameType referenceFrame = referenceFrames[referenceIndex];
            Av1MotionVector motionVector = motionVectors[referenceIndex];

            if (referenceFrame > Av1ReferenceFrameType.Intra &&
                this.motionFieldReferenceSides[(int)referenceFrame] == 0 &&
                Math.Abs(motionVector.Row) <= ReferenceMotionVectorLimit &&
                Math.Abs(motionVector.Column) <= ReferenceMotionVectorLimit)
            {
                selectedReference = referenceFrame;
                selectedMotionVector = motionVector;
            }
        }

        int blockModeInfoWidth = Math.Min(
            modeInfo.BlockSize.Get4x4WideCount(),
            this.activeModeInfoColumnCount - modeInfoPosition.X);

        int blockModeInfoHeight = Math.Min(
            modeInfo.BlockSize.Get4x4HighCount(),
            this.activeModeInfoRowCount - modeInfoPosition.Y);

        int fieldWidth = (blockModeInfoWidth + 1) >> MotionFieldModeInfoShift;
        int fieldHeight = (blockModeInfoHeight + 1) >> MotionFieldModeInfoShift;
        int firstFieldRow = modeInfoPosition.Y >> MotionFieldModeInfoShift;
        int firstFieldColumn = modeInfoPosition.X >> MotionFieldModeInfoShift;
        RetainedMotionFieldEntry entry = new(selectedMotionVector, selectedReference);

        // One decoded block supplies the same retained candidate to every covered 8x8 cell. Filling each contiguous
        // row lets the runtime select its optimized span implementation while later sub-8x8 blocks retain the
        // normative ability to overwrite the shared cell in traversal order.
        for (int row = 0; row < fieldHeight; row++)
        {
            int rowOffset = ((firstFieldRow + row) * retainedMotionField.Stride) + firstFieldColumn;
            retainedEntries.Slice(rowOffset, fieldWidth).Fill(entry);
        }
    }

    /// <summary>
    /// Projects one retained frame's motion field into the current frame's temporal candidate grid.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining the modulo order-hint domain.</param>
    /// <param name="frameHeader">The current frame header.</param>
    /// <param name="startFrame">The retained frame whose stored motion vectors are projected.</param>
    /// <param name="temporalMotionField">The complete destination field storage for the current frame.</param>
    /// <param name="reverseDirection">
    /// A value indicating whether the start-to-current distance and spatial displacement are reversed for a past frame.
    /// </param>
    /// <returns><see langword="true"/> when the retained frame is an eligible projection source.</returns>
    private bool ProjectMotionField(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrame startFrame,
        MotionFieldStorage<TemporalMotionFieldEntry> temporalMotionField,
        bool reverseDirection)
    {
        ObuFrameHeader startFrameHeader = startFrame.FrameHeader;
        if (startFrameHeader.IsIntra ||
            startFrameHeader.ModeInfoRowCount != this.activeModeInfoRowCount ||
            startFrameHeader.ModeInfoColumnCount != this.activeModeInfoColumnCount)
        {
            // AV1 does not rescale temporal fields. Intra sources contain no inter motion, and a differently sized
            // source has no one-to-one 8x8 grid on which the normative projection can operate.
            return false;
        }

        ReferenceState startFrameState = startFrame.ReferenceState;
        ObuOrderHintInfo orderHintInfo = sequenceHeader.OrderHintInfo;
        int startToCurrentFrameOffset = orderHintInfo.GetRelativeDistance(
            startFrameHeader.OrderHint,
            frameHeader.OrderHint);

        if (reverseDirection)
        {
            startToCurrentFrameOffset = -startToCurrentFrameOffset;
        }

        int sourceRowCount = (this.activeModeInfoRowCount + 1) >> MotionFieldModeInfoShift;
        int sourceColumnCount = (this.activeModeInfoColumnCount + 1) >> MotionFieldModeInfoShift;
        int destinationRowCount = this.activeModeInfoRowCount >> MotionFieldModeInfoShift;
        int destinationColumnCount = this.activeModeInfoColumnCount >> MotionFieldModeInfoShift;
        MotionFieldStorage<RetainedMotionFieldEntry>? retainedMotionFieldState = startFrameState.RetainedMotionField;
        if (retainedMotionFieldState is null)
        {
            return false;
        }

        MotionFieldStorage<RetainedMotionFieldEntry> retainedMotionField = retainedMotionFieldState.Value;

        ReadOnlySpan<RetainedMotionFieldEntry> sourceEntries = retainedMotionField.Owner.Memory.Span;
        Span<TemporalMotionFieldEntry> destinationEntries = temporalMotionField.Owner.Memory.Span;

        for (int blockRow = 0; blockRow < sourceRowCount; blockRow++)
        {
            int sourceRowOffset = blockRow * retainedMotionField.Stride;
            for (int blockColumn = 0; blockColumn < sourceColumnCount; blockColumn++)
            {
                RetainedMotionFieldEntry source = sourceEntries[sourceRowOffset + blockColumn];
                if (source.ReferenceFrame <= Av1ReferenceFrameType.Intra)
                {
                    continue;
                }

                int referenceFrameOffset = orderHintInfo.GetRelativeDistance(
                    startFrameHeader.OrderHint,
                    startFrameState.MotionFieldReferenceOrderHints[(int)source.ReferenceFrame]);

                bool positionIsValid = Math.Abs(referenceFrameOffset) <= Av1MotionVector.MaximumTemporalDistance &&
                    referenceFrameOffset > 0 &&
                    Math.Abs(startToCurrentFrameOffset) <= Av1MotionVector.MaximumTemporalDistance;

                if (!positionIsValid)
                {
                    continue;
                }

                Av1MotionVector projected = source.MotionVector.ProjectTemporal(startToCurrentFrameOffset, referenceFrameOffset);

                if (!TryGetProjectedBlockPosition(
                    blockRow,
                    blockColumn,
                    projected,
                    reverseDirection,
                    destinationRowCount,
                    destinationColumnCount,
                    out int projectedRow,
                    out int projectedColumn))
                {
                    continue;
                }

                // The projected vector selects the destination cell, but AV1 stores the original forward vector and
                // its source-to-reference distance there. Candidate scaling later uses both values for its own target.
                int destinationOffset = (projectedRow * temporalMotionField.Stride) + projectedColumn;
                destinationEntries[destinationOffset] = new(source.MotionVector, referenceFrameOffset);
            }
        }

        return true;
    }

    /// <summary>
    /// Detaches the compact state required while this decoded frame occupies the reference map.
    /// </summary>
    public ReferenceState PrepareReferenceState()
    {
        ReferenceState? state = this.referenceState;
        if (state is null)
        {
            // Future frames need the segment map, 8x8 motion field, and original reference-order hints.
            // Transfer their owners without copying; reconstruction scratch remains local to this frame.
            state = new ReferenceState(
                this.segmentIds,
                this.segmentIdColumnCount,
                this.segmentIdRowCount,
                this.retainedMotionField,
                this.motionFieldReferenceOrderHints);

            this.segmentIds = null;
            this.retainedMotionField = null;
            this.referenceState = state;
        }

        return state;
    }

    /// <summary>
    /// Acquires the prepared compact state for one reference-frame owner.
    /// </summary>
    /// <returns>The retained reference state with one ownership lease for the caller.</returns>
    public ReferenceState AcquireReferenceState()
    {
        ReferenceState state = this.PrepareReferenceState();

        state.AddOwner();
        return state;
    }

    /// <summary>
    /// Adds one owner for this frame state.
    /// </summary>
    public void AddOwner()
    {
        // One decoder session serializes tile parsing, reference-map updates, and output transfer. A direct count is
        // therefore sufficient and avoids both atomic operations and a separately allocated shared-owner object.
        this.ownerCount++;
    }

    /// <summary>
    /// Releases the initial owner created with this frame state.
    /// </summary>
    public void Dispose()
    {
        if (this.ownsInitialLease)
        {
            // Av1TileReader can complete through both the OBU lifecycle and decoder cleanup. Keeping the initial lease
            // idempotent lets either path dispose safely without affecting reference-frame or result-state owners.
            this.ownsInitialLease = false;
            this.ReleaseOwner();
        }
    }

    /// <summary>
    /// Releases one owner and returns allocator-backed frame storage after the final owner is released.
    /// </summary>
    public void ReleaseOwner()
    {
        this.ownerCount--;
        if (this.ownerCount == 0)
        {
            // Reconstruction-only syntax expires with the tile reader and optional decoder-result owner. A detached
            // reference state has its own lease and can outlive this full frame state.
            this.referenceState?.ReleaseOwner();
            this.referenceState = null;
            this.retainedMotionField?.Dispose();
            this.retainedMotionField = null;
            this.temporalMotionField?.Dispose();
            this.temporalMotionField = null;

            for (int plane = 0; plane < Av1Constants.MaxPlanes; plane++)
            {
                this.loopRestorationUnits[plane]?.Dispose();
            }

            this.segmentIds?.Dispose();
            this.coefficientScratch.Dispose();
            this.deltaLoopFilter.Dispose();
            this.cdefStrength.Dispose();
            this.quantizerIndices.Dispose();
            this.transformInfoScratch.Dispose();
            this.modeInfoMap.Dispose();
            this.modeInfoCounts.Dispose();
            this.modeInfos.Dispose();
        }
    }

    /// <summary>
    /// Maps a projected motion vector to its bounded destination on the current 8x8 field.
    /// </summary>
    /// <param name="blockRow">The source 8x8 row.</param>
    /// <param name="blockColumn">The source 8x8 column.</param>
    /// <param name="motionVector">The temporally projected vector in one-eighth-sample units.</param>
    /// <param name="reverseDirection">Whether the vector moves backwards from the source position.</param>
    /// <param name="rowCount">The number of complete 8x8 rows in the current frame.</param>
    /// <param name="columnCount">The number of complete 8x8 columns in the current frame.</param>
    /// <param name="projectedRow">Receives the projected 8x8 row.</param>
    /// <param name="projectedColumn">Receives the projected 8x8 column.</param>
    /// <returns><see langword="true"/> when the destination lies in the permitted projection window.</returns>
    private static bool TryGetProjectedBlockPosition(
        int blockRow,
        int blockColumn,
        Av1MotionVector motionVector,
        bool reverseDirection,
        int rowCount,
        int columnCount,
        out int projectedRow,
        out int projectedColumn)
    {
        int baseBlockRow = (blockRow >> 3) << 3;
        int baseBlockColumn = (blockColumn >> 3) << 3;

        // One field cell spans 8 samples, while vectors use one-eighth-sample units; dividing by 64 converts between
        // them. Division must truncate toward zero: an arithmetic right shift would move negative sub-cell
        // displacements into the preceding cell.
        int rowOffset = motionVector.Row / (1 << MotionVectorToFieldOffsetShift);
        int columnOffset = motionVector.Column / (1 << MotionVectorToFieldOffsetShift);
        projectedRow = reverseDirection ? blockRow - rowOffset : blockRow + rowOffset;
        projectedColumn = reverseDirection ? blockColumn - columnOffset : blockColumn + columnOffset;

        if (projectedRow < 0 || projectedRow >= rowCount || projectedColumn < 0 || projectedColumn >= columnCount)
        {
            return false;
        }

        // AV1 keeps a projection in the same 64x64 row band and permits one additional 64-sample horizontal band on
        // either side. This bounds temporal-candidate lookup while accommodating common lateral motion.
        return projectedRow >= baseBlockRow &&
            projectedRow < baseBlockRow + 8 &&
            projectedColumn >= baseBlockColumn - MaximumHorizontalFieldOffset &&
            projectedColumn < baseBlockColumn + 8 + MaximumHorizontalFieldOffset;
    }

    /// <summary>
    /// Couples one allocator-owned motion-field buffer with the stride required to address it.
    /// </summary>
    internal readonly struct MotionFieldStorage<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MotionFieldStorage{T}"/> struct.
        /// </summary>
        public MotionFieldStorage(IMemoryOwner<T> owner, int stride)
        {
            this.Owner = owner;
            this.Stride = stride;
        }

        /// <summary>
        /// Gets the allocator-owned field entries.
        /// </summary>
        public IMemoryOwner<T> Owner { get; }

        /// <summary>
        /// Gets the number of entries in one field row.
        /// </summary>
        public int Stride { get; }

        /// <summary>
        /// Returns the field entries to their allocator.
        /// </summary>
        public void Dispose() => this.Owner.Dispose();
    }

    /// <summary>
    /// Carries the complete seven-role reference mapping captured before the current frame refreshes map slots.
    /// </summary>
    private readonly struct SelectedReferenceFrames
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SelectedReferenceFrames"/> struct.
        /// </summary>
        public SelectedReferenceFrames(Av1ReferenceFrameStore referenceFrames, ReadOnlySpan<uint> referenceFrameIndices)
        {
            this.Last = referenceFrames.ResolveRequired((int)referenceFrameIndices[0]);
            this.Last2 = referenceFrames.ResolveRequired((int)referenceFrameIndices[1]);
            this.Last3 = referenceFrames.ResolveRequired((int)referenceFrameIndices[2]);
            this.Golden = referenceFrames.ResolveRequired((int)referenceFrameIndices[3]);
            this.Backward = referenceFrames.ResolveRequired((int)referenceFrameIndices[4]);
            this.Alternate2 = referenceFrames.ResolveRequired((int)referenceFrameIndices[5]);
            this.Alternate = referenceFrames.ResolveRequired((int)referenceFrameIndices[6]);
        }

        /// <summary>
        /// Gets the retained LAST frame.
        /// </summary>
        public Av1ReferenceFrame Last { get; }

        /// <summary>
        /// Gets the retained LAST2 frame.
        /// </summary>
        public Av1ReferenceFrame Last2 { get; }

        /// <summary>
        /// Gets the retained LAST3 frame.
        /// </summary>
        public Av1ReferenceFrame Last3 { get; }

        /// <summary>
        /// Gets the retained GOLDEN frame.
        /// </summary>
        public Av1ReferenceFrame Golden { get; }

        /// <summary>
        /// Gets the retained BWDREF frame.
        /// </summary>
        public Av1ReferenceFrame Backward { get; }

        /// <summary>
        /// Gets the retained ALTREF2 frame.
        /// </summary>
        public Av1ReferenceFrame Alternate2 { get; }

        /// <summary>
        /// Gets the retained ALTREF frame.
        /// </summary>
        public Av1ReferenceFrame Alternate { get; }

        /// <summary>
        /// Gets the retained frame for one canonical inter-reference role.
        /// </summary>
        public Av1ReferenceFrame this[Av1ReferenceFrameType referenceFrame] => referenceFrame switch
        {
            Av1ReferenceFrameType.Last => this.Last,
            Av1ReferenceFrameType.Last2 => this.Last2,
            Av1ReferenceFrameType.Last3 => this.Last3,
            Av1ReferenceFrameType.Golden => this.Golden,
            Av1ReferenceFrameType.Backward => this.Backward,
            Av1ReferenceFrameType.Alternate2 => this.Alternate2,
            Av1ReferenceFrameType.Alternate => this.Alternate,
            _ => throw new InvalidOperationException("Motion fields only use canonical inter-reference roles.")
        };
    }

    /// <summary>
    /// Stores one motion vector and logical reference retained for projection by a later frame.
    /// </summary>
    internal readonly struct RetainedMotionFieldEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RetainedMotionFieldEntry"/> struct.
        /// </summary>
        /// <param name="motionVector">The retained motion vector in one-eighth-sample units.</param>
        /// <param name="referenceFrame">The logical reference targeted by the vector.</param>
        public RetainedMotionFieldEntry(Av1MotionVector motionVector, Av1ReferenceFrameType referenceFrame)
        {
            this.MotionVector = motionVector;
            this.ReferenceFrame = referenceFrame;
        }

        /// <summary>
        /// Gets the retained motion vector in one-eighth-sample units.
        /// </summary>
        public Av1MotionVector MotionVector { get; }

        /// <summary>
        /// Gets the logical reference targeted by <see cref="MotionVector"/>.
        /// </summary>
        public Av1ReferenceFrameType ReferenceFrame { get; }
    }

    /// <summary>
    /// Stores one temporal candidate projected over the current frame's 8x8 grid.
    /// </summary>
    private readonly struct TemporalMotionFieldEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalMotionFieldEntry"/> struct.
        /// </summary>
        /// <param name="motionVector">The retained source vector in one-eighth-sample units.</param>
        /// <param name="referenceFrameOffset">The positive temporal distance from the source to its reference.</param>
        public TemporalMotionFieldEntry(Av1MotionVector motionVector, int referenceFrameOffset)
        {
            this.MotionVector = motionVector;
            this.ReferenceFrameOffset = referenceFrameOffset;
        }

        /// <summary>
        /// Gets the retained source vector in one-eighth-sample units.
        /// </summary>
        public Av1MotionVector MotionVector { get; }

        /// <summary>
        /// Gets the positive temporal distance from the source frame to its reference.
        /// </summary>
        public int ReferenceFrameOffset { get; }
    }

    /// <summary>
    /// Retains the segmentation and motion information needed when subsequent frames refer to this frame.
    /// </summary>
    internal sealed class ReferenceState
    {
        /// <summary>
        /// The number of frame-info and reference-frame owners retaining this state.
        /// </summary>
        private int ownerCount = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceState"/> class by taking ownership of retained buffers.
        /// </summary>
        public ReferenceState(
            Buffer2D<byte>? segmentIds,
            int segmentIdColumnCount,
            int segmentIdRowCount,
            MotionFieldStorage<RetainedMotionFieldEntry>? retainedMotionField,
            InlineArray8<uint> motionFieldReferenceOrderHints)
        {
            this.SegmentIds = segmentIds;
            this.SegmentIdColumnCount = segmentIdColumnCount;
            this.SegmentIdRowCount = segmentIdRowCount;
            this.RetainedMotionField = retainedMotionField;
            this.MotionFieldReferenceOrderHints = motionFieldReferenceOrderHints;
        }

        /// <summary>
        /// Gets the retained 4x4 segmentation map, or <see langword="null"/> when segmentation is disabled.
        /// </summary>
        public Buffer2D<byte>? SegmentIds { get; private set; }

        /// <summary>
        /// Gets the number of active 4x4 columns in <see cref="SegmentIds"/>.
        /// </summary>
        public int SegmentIdColumnCount { get; }

        /// <summary>
        /// Gets the number of active 4x4 rows in <see cref="SegmentIds"/>.
        /// </summary>
        public int SegmentIdRowCount { get; }

        /// <summary>
        /// Gets the retained per-8x8 motion field, or <see langword="null"/> when the temporal tool is disabled.
        /// </summary>
        public MotionFieldStorage<RetainedMotionFieldEntry>? RetainedMotionField { get; private set; }

        /// <summary>
        /// Gets the order hints selected by this frame's seven logical inter-reference roles.
        /// </summary>
        public InlineArray8<uint> MotionFieldReferenceOrderHints { get; }

        /// <summary>
        /// Adds one owner for this retained state.
        /// </summary>
        public void AddOwner() => this.ownerCount++;

        /// <summary>
        /// Releases one owner and returns retained buffers after the final lease.
        /// </summary>
        public void ReleaseOwner()
        {
            this.ownerCount--;
            if (this.ownerCount == 0)
            {
                this.RetainedMotionField?.Dispose();
                this.RetainedMotionField = null;
                this.SegmentIds?.Dispose();
                this.SegmentIds = null;
            }
        }
    }
}
