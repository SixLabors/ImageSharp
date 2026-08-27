// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the retained and projected per-8x8 motion fields associated with a decoded AV1 frame.
/// </summary>
internal partial class Av1FrameInfo
{
    /// <summary>
    /// The maximum absolute temporal distance accepted by AV1 motion-field projection.
    /// </summary>
    private const int MaximumFrameDistance = 31;

    /// <summary>
    /// The maximum number of reference frames projected into one temporal motion field.
    /// </summary>
    private const int MotionFieldProjectionCount = 3;

    /// <summary>
    /// The maximum source motion-vector magnitude retained for later temporal projection.
    /// </summary>
    private const int ReferenceMotionVectorLimit = 4095;

    /// <summary>
    /// The exclusive upper bound of an AV1 motion-vector component in one-eighth-sample units.
    /// </summary>
    private const int MotionVectorUpperBound = 16384;

    /// <summary>
    /// The reserved lower endpoint of an AV1 motion-vector component in one-eighth-sample units.
    /// </summary>
    private const int MotionVectorLowerBound = -16384;

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
    /// Stores the selected motion vector and logical reference for every retained 8x8 frame position.
    /// </summary>
    private RetainedMotionFieldEntry[] retainedMotionField = [];

    /// <summary>
    /// Stores motion vectors projected from retained frames into the current frame's 8x8 grid.
    /// </summary>
    private TemporalMotionFieldEntry[] temporalMotionField = [];

    /// <summary>
    /// Stores the order hint selected by each logical inter-reference type for later projections from this frame.
    /// </summary>
    private InlineArray8<uint> motionFieldReferenceOrderHints;

    /// <summary>
    /// Stores whether each logical inter-reference type lies after, at, or before the current frame in display order.
    /// </summary>
    private InlineArray8<sbyte> motionFieldReferenceSides;

    /// <summary>
    /// The number of retained motion-field entries in one active 8x8 row.
    /// </summary>
    private int retainedMotionFieldStride;

    /// <summary>
    /// The number of projected temporal-motion entries in one aligned 8x8 row.
    /// </summary>
    private int temporalMotionFieldStride;

    /// <summary>
    /// The active frame width in 4x4 mode-information units.
    /// </summary>
    private int activeModeInfoColumnCount;

    /// <summary>
    /// The active frame height in 4x4 mode-information units.
    /// </summary>
    private int activeModeInfoRowCount;

    /// <summary>
    /// Gets the reciprocal table used by AV1 motion-vector projection in 14-bit fixed-point precision.
    /// </summary>
    private static ReadOnlySpan<int> ProjectionDivisors =>
        [0, 16384, 8192, 5461, 4096, 3276, 2730, 2340, 2048, 1820, 1638, 1489, 1365, 1260, 1170, 1092,
         1024, 963, 910, 862, 819, 780, 744, 712, 682, 655, 630, 606, 585, 564, 546, 528];

    /// <summary>
    /// Allocates and derives the motion fields required by one decoded frame.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining motion-field enablement and order-hint precision.</param>
    /// <param name="frameHeader">The current frame header and its seven resolved inter-reference roles.</param>
    /// <param name="referenceFrames">The retained reconstructed frames selected by the current reference map.</param>
    public void InitializeMotionField(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameStore referenceFrames)
    {
        if (!sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors)
        {
            return;
        }

        this.activeModeInfoColumnCount = frameHeader.ModeInfoColumnCount;
        this.activeModeInfoRowCount = frameHeader.ModeInfoRowCount;
        this.retainedMotionFieldStride = (this.activeModeInfoColumnCount + 1) >> MotionFieldModeInfoShift;

        if (frameHeader.IsIntra)
        {
            // Intra frames retain an empty source field. They can occupy reference slots, but libaom rejects them as
            // projection sources before consulting their reference-order-hint metadata.
            return;
        }

        InlineArray8<Av1ReferenceFrame?> selectedReferences = default;
        ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        ObuOrderHintInfo orderHintInfo = sequenceHeader.OrderHintInfo;

        // Capture the seven logical-role order hints before this frame refreshes any physical map slots. Libaom keeps
        // the same snapshot on RefCntBuffer so a later frame can project this frame's stored motion vectors.
        for (int referenceIndex = 0; referenceIndex < Av1Constants.ReferencesPerFrame; referenceIndex++)
        {
            Av1ReferenceFrameType referenceFrameType = (Av1ReferenceFrameType)(referenceIndex + 1);
            Av1ReferenceFrame referenceFrame = referenceFrames.Resolve((int)referenceFrameIndices[referenceIndex])!;
            uint referenceOrderHint = referenceFrame.FrameHeader.OrderHint;
            selectedReferences[(int)referenceFrameType] = referenceFrame;
            this.motionFieldReferenceOrderHints[(int)referenceFrameType] = referenceOrderHint;

            int relativeDistance = orderHintInfo.GetRelativeDistance(referenceOrderHint, frameHeader.OrderHint);
            this.motionFieldReferenceSides[(int)referenceFrameType] = relativeDistance > 0
                ? (sbyte)1
                : referenceOrderHint == frameHeader.OrderHint ? (sbyte)-1 : (sbyte)0;
        }

        // FrameInfo is transferred directly into each retained frame owner, so allocate only the active 8x8 source
        // grid whose completed block vectors can be projected by a later frame.
        int retainedRowCount = (this.activeModeInfoRowCount + 1) >> MotionFieldModeInfoShift;
        this.retainedMotionField = new RetainedMotionFieldEntry[this.retainedMotionFieldStride * retainedRowCount];

        if (!frameHeader.UseReferenceFrameMotionVectors)
        {
            return;
        }

        // libaom aligns the projected field stride to the largest superblock even for a 64x64 sequence. This keeps
        // later temporal-candidate addressing independent of the current sequence's selected superblock size.
        int alignedModeInfoColumnCount = Av1Math.AlignPowerOf2(
            this.activeModeInfoColumnCount,
            MaximumSuperblockModeInfoSizeLog2);

        this.temporalMotionFieldStride = alignedModeInfoColumnCount >> MotionFieldModeInfoShift;
        int temporalRowCount = (this.activeModeInfoRowCount + MaximumSuperblockModeInfoSize) >> MotionFieldModeInfoShift;
        this.temporalMotionField = new TemporalMotionFieldEntry[this.temporalMotionFieldStride * temporalRowCount];

        // AV1 examines LAST, BWDREF, ALTREF2, ALTREF, and LAST2 in this normative order and admits at most three
        // projection sources. LAST always consumes the first budget position, forward references consume one only
        // when eligible projection succeeds, and LAST2 fills the final unused position in the reverse direction.
        int remainingProjectionCount = MotionFieldProjectionCount;
        Av1ReferenceFrame lastFrame = selectedReferences[(int)Av1ReferenceFrameType.Last]!;
        Av1ReferenceFrame goldenFrame = selectedReferences[(int)Av1ReferenceFrameType.Golden]!;
        uint alternateOfLastOrderHint = lastFrame.FrameInfo.motionFieldReferenceOrderHints[(int)Av1ReferenceFrameType.Alternate];

        // A LAST frame whose ALTREF order matches GOLDEN is an overlay. Projecting it would duplicate the overlay's
        // temporal source, but libaom still consumes one position from the three-source projection budget.
        if (alternateOfLastOrderHint != goldenFrame.FrameHeader.OrderHint)
        {
            _ = this.ProjectMotionField(sequenceHeader, frameHeader, lastFrame, reverseDirection: true);
        }

        remainingProjectionCount--;
        Av1ReferenceFrame backwardFrame = selectedReferences[(int)Av1ReferenceFrameType.Backward]!;

        if (orderHintInfo.GetRelativeDistance(backwardFrame.FrameHeader.OrderHint, frameHeader.OrderHint) > 0 &&
            this.ProjectMotionField(sequenceHeader, frameHeader, backwardFrame, reverseDirection: false))
        {
            remainingProjectionCount--;
        }

        Av1ReferenceFrame alternate2Frame = selectedReferences[(int)Av1ReferenceFrameType.Alternate2]!;

        if (orderHintInfo.GetRelativeDistance(alternate2Frame.FrameHeader.OrderHint, frameHeader.OrderHint) > 0 &&
            this.ProjectMotionField(sequenceHeader, frameHeader, alternate2Frame, reverseDirection: false))
        {
            remainingProjectionCount--;
        }

        Av1ReferenceFrame alternateFrame = selectedReferences[(int)Av1ReferenceFrameType.Alternate]!;

        if (remainingProjectionCount > 0 &&
            orderHintInfo.GetRelativeDistance(alternateFrame.FrameHeader.OrderHint, frameHeader.OrderHint) > 0 &&
            this.ProjectMotionField(sequenceHeader, frameHeader, alternateFrame, reverseDirection: false))
        {
            remainingProjectionCount--;
        }

        if (remainingProjectionCount > 0)
        {
            Av1ReferenceFrame last2Frame = selectedReferences[(int)Av1ReferenceFrameType.Last2]!;
            _ = this.ProjectMotionField(sequenceHeader, frameHeader, last2Frame, reverseDirection: true);
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
        int index = ((modeInfoRow >> MotionFieldModeInfoShift) * this.temporalMotionFieldStride) +
            (modeInfoColumn >> MotionFieldModeInfoShift);

        TemporalMotionFieldEntry entry = this.temporalMotionField[index];
        motionVector = entry.MotionVector;
        referenceFrameOffset = entry.ReferenceFrameOffset;
        return referenceFrameOffset > 0;
    }

    /// <summary>
    /// Writes the retained per-8x8 motion-field entries covered by one completed mode-information block.
    /// </summary>
    /// <param name="modeInfo">The completed block mode information.</param>
    /// <param name="modeInfoPosition">The block origin in frame-relative 4x4 units.</param>
    private void UpdateRetainedMotionField(Av1BlockModeInfo modeInfo, Point modeInfoPosition)
    {
        if (this.retainedMotionField.Length == 0)
        {
            return;
        }

        Av1ReferenceFrameType selectedReference = Av1ReferenceFrameType.None;
        Av1MotionVector selectedMotionVector = default;
        Span<Av1ReferenceFrameType> referenceFrames = modeInfo.ReferenceFrames;
        Span<Av1MotionVector> motionVectors = modeInfo.MotionVectors;

        // Compound blocks may offer two vectors. libaom retains the last eligible forward-or-past reference after
        // excluding same-order, future, and out-of-range vectors, so preserve that overwrite order exactly.
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

        // One decoded block supplies the same retained candidate to every covered 8x8 cell. Array.Fill preserves the
        // native contiguous-row write and lets later sub-8x8 blocks overwrite the shared cell in traversal order.
        for (int row = 0; row < fieldHeight; row++)
        {
            int rowOffset = ((firstFieldRow + row) * this.retainedMotionFieldStride) + firstFieldColumn;
            Array.Fill(this.retainedMotionField, entry, rowOffset, fieldWidth);
        }
    }

    /// <summary>
    /// Projects one retained frame's motion field into the current frame's temporal candidate grid.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining the modulo order-hint domain.</param>
    /// <param name="frameHeader">The current frame header.</param>
    /// <param name="startFrame">The retained frame whose stored motion vectors are projected.</param>
    /// <param name="reverseDirection">
    /// A value indicating whether the start-to-current distance and spatial displacement are reversed for a past frame.
    /// </param>
    /// <returns><see langword="true"/> when the retained frame is an eligible projection source.</returns>
    private bool ProjectMotionField(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrame startFrame,
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

        Av1FrameInfo startFrameInfo = startFrame.FrameInfo;
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

        for (int blockRow = 0; blockRow < sourceRowCount; blockRow++)
        {
            int sourceRowOffset = blockRow * startFrameInfo.retainedMotionFieldStride;
            for (int blockColumn = 0; blockColumn < sourceColumnCount; blockColumn++)
            {
                RetainedMotionFieldEntry source = startFrameInfo.retainedMotionField[sourceRowOffset + blockColumn];
                if (source.ReferenceFrame <= Av1ReferenceFrameType.Intra)
                {
                    continue;
                }

                int referenceFrameOffset = orderHintInfo.GetRelativeDistance(
                    startFrameHeader.OrderHint,
                    startFrameInfo.motionFieldReferenceOrderHints[(int)source.ReferenceFrame]);

                bool positionIsValid = Math.Abs(referenceFrameOffset) <= MaximumFrameDistance &&
                    referenceFrameOffset > 0 &&
                    Math.Abs(startToCurrentFrameOffset) <= MaximumFrameDistance;

                if (!positionIsValid)
                {
                    continue;
                }

                Av1MotionVector projected = ProjectMotionVector(
                    source.MotionVector,
                    startToCurrentFrameOffset,
                    referenceFrameOffset);

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
                int destinationOffset = (projectedRow * this.temporalMotionFieldStride) + projectedColumn;
                this.temporalMotionField[destinationOffset] = new(source.MotionVector, referenceFrameOffset);
            }
        }

        return true;
    }

    /// <summary>
    /// Scales a retained motion vector by a signed ratio of temporal distances.
    /// </summary>
    /// <param name="motionVector">The retained vector in one-eighth-sample units.</param>
    /// <param name="numerator">The signed start-to-current temporal distance.</param>
    /// <param name="denominator">The positive start-to-reference temporal distance.</param>
    /// <returns>The projected and AV1-range-clamped vector.</returns>
    private static Av1MotionVector ProjectMotionVector(Av1MotionVector motionVector, int numerator, int denominator)
    {
        denominator = Math.Min(denominator, MaximumFrameDistance);
        numerator = Av1Math.Clip3(-MaximumFrameDistance, MaximumFrameDistance, numerator);

        // ProjectionDivisors represents 1 / denominator in Q14. Symmetric power-of-two rounding matches libaom for
        // negative vectors, and the final clamp excludes the two reserved extreme motion-vector values.
        int row = Av1Math.RoundPowerOf2Signed(motionVector.Row * numerator * ProjectionDivisors[denominator], 14);
        int column = Av1Math.RoundPowerOf2Signed(motionVector.Column * numerator * ProjectionDivisors[denominator], 14);
        row = Av1Math.Clip3(MotionVectorLowerBound + 1, MotionVectorUpperBound - 1, row);
        column = Av1Math.Clip3(MotionVectorLowerBound + 1, MotionVectorUpperBound - 1, column);
        return new(row, column);
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
        // them. C# integer division truncates toward zero, matching libaom's explicit signed-shift construction.
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
    /// Stores one motion vector and logical reference retained for projection by a later frame.
    /// </summary>
    private readonly struct RetainedMotionFieldEntry
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
}
