// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Holds the temporal motion vectors that the current frame of a sequence projects from its references, and saves
/// the motion vectors of each coded frame for later frames. Reference: cm->tpl_mvs and cm->ref_frame_side.
/// </summary>
internal sealed class Av1EncoderMotionField : IDisposable
{
    /// <summary>
    /// The largest retained vector component. Reference: REFMVS_LIMIT.
    /// </summary>
    private const int ReferenceMotionVectorLimit = (1 << 12) - 1;

    /// <summary>
    /// The number of projections a frame may use. Reference: MFMV_STACK_SIZE.
    /// </summary>
    private const int ProjectionStackSize = 3;

    /// <summary>
    /// The base-two logarithm of the largest superblock width in 4x4 mode-information units.
    /// </summary>
    private const int MaximumSuperblockModeInfoSizeLog2 = Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2;

    private readonly int modeInfoColumnCount;
    private readonly int modeInfoRowCount;
    private readonly Av1FrameInfo.MotionFieldStorage<Av1FrameInfo.TemporalMotionFieldEntry> temporalMotionField;
    private readonly uint[] referenceOrderHints = new uint[Av1Constants.ReferenceFrameCount];
    private readonly sbyte[] referenceSides = new sbyte[Av1Constants.ReferenceFrameCount];
    private uint orderHint;
    private bool useTemporalMotionField;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderMotionField"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the field allocator.</param>
    /// <param name="modeInfoColumnCount">The frame width in 4x4 mode-information units.</param>
    /// <param name="modeInfoRowCount">The frame height in 4x4 mode-information units.</param>
    public Av1EncoderMotionField(Configuration configuration, int modeInfoColumnCount, int modeInfoRowCount)
    {
        this.modeInfoColumnCount = modeInfoColumnCount;
        this.modeInfoRowCount = modeInfoRowCount;

        // Rows align to the largest superblock so both superblock sizes address the same grid.
        int stride = Av1Math.AlignPowerOf2(modeInfoColumnCount, MaximumSuperblockModeInfoSizeLog2) >> 1;
        int rowCount = (modeInfoRowCount + (1 << MaximumSuperblockModeInfoSizeLog2)) >> 1;
        this.temporalMotionField = new(
            configuration.MemoryAllocator.Allocate<Av1FrameInfo.TemporalMotionFieldEntry>(stride * rowCount, AllocationOptions.Clean),
            stride);
    }

    /// <summary>
    /// Creates the saved motion field storage for one reference buffer.
    /// </summary>
    /// <param name="configuration">The configuration providing the field allocator.</param>
    /// <returns>The saved motion field storage.</returns>
    public SavedMotionField CreateSavedMotionField(Configuration configuration)
        => new(configuration, this.modeInfoColumnCount, this.modeInfoRowCount);

    /// <summary>
    /// Classifies the references of the current frame and projects the saved motion vectors of its references.
    /// Reference: the order hints that av1_setup_frame_buf_refs() saves, av1_calculate_ref_frame_side(), and
    /// av1_setup_motion_field().
    /// </summary>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The current frame header with its reference indices.</param>
    /// <param name="references">The saved motion field of each reference type, or <see langword="null"/> for an empty slot.</param>
    public void Setup(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, ReadOnlySpan<SavedMotionField?> references)
    {
        ObuOrderHintInfo orderHintInfo = sequenceHeader.OrderHintInfo;
        this.orderHint = frameHeader.OrderHint;
        this.useTemporalMotionField = false;
        Array.Clear(this.referenceOrderHints);
        Array.Clear(this.referenceSides);
        if (!orderHintInfo.EnableOrderHint)
        {
            return;
        }

        for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
        {
            uint referenceOrderHint = references[reference]?.OrderHint ?? 0;
            this.referenceOrderHints[reference] = referenceOrderHint;
            if (orderHintInfo.GetRelativeDistance(referenceOrderHint, this.orderHint) > 0)
            {
                this.referenceSides[reference] = 1;
            }
            else if (referenceOrderHint == this.orderHint)
            {
                this.referenceSides[reference] = -1;
            }
        }

        if (!frameHeader.UseReferenceFrameMotionVectors)
        {
            return;
        }

        this.useTemporalMotionField = true;
        this.temporalMotionField.Owner.Memory.Span.Clear();

        int referenceStamp = ProjectionStackSize - 1;
        SavedMotionField? last = references[(int)Av1ReferenceFrameType.Last];
        if (last is not null)
        {
            // A LAST frame whose ALTREF is the current GOLDEN is an overlay, which the projection skips.
            uint alternateOfLastOrderHint = last.ReferenceOrderHints[(int)Av1ReferenceFrameType.Alternate];
            if (alternateOfLastOrderHint != this.referenceOrderHints[(int)Av1ReferenceFrameType.Golden])
            {
                _ = this.ProjectMotionField(orderHintInfo, last, reverseDirection: true);
            }

            referenceStamp--;
        }

        if (orderHintInfo.GetRelativeDistance(this.referenceOrderHints[(int)Av1ReferenceFrameType.Backward], this.orderHint) > 0 &&
            this.ProjectMotionField(orderHintInfo, references[(int)Av1ReferenceFrameType.Backward], reverseDirection: false))
        {
            referenceStamp--;
        }

        if (orderHintInfo.GetRelativeDistance(this.referenceOrderHints[(int)Av1ReferenceFrameType.Alternate2], this.orderHint) > 0 &&
            this.ProjectMotionField(orderHintInfo, references[(int)Av1ReferenceFrameType.Alternate2], reverseDirection: false))
        {
            referenceStamp--;
        }

        if (orderHintInfo.GetRelativeDistance(this.referenceOrderHints[(int)Av1ReferenceFrameType.Alternate], this.orderHint) > 0 &&
            referenceStamp >= 0 &&
            this.ProjectMotionField(orderHintInfo, references[(int)Av1ReferenceFrameType.Alternate], reverseDirection: false))
        {
            referenceStamp--;
        }

        if (referenceStamp >= 0)
        {
            _ = this.ProjectMotionField(orderHintInfo, references[(int)Av1ReferenceFrameType.Last2], reverseDirection: true);
        }
    }

    /// <summary>
    /// Gets a value indicating whether a reference lies after the current frame in display order.
    /// Reference: cm->ref_frame_side.
    /// </summary>
    /// <param name="referenceFrame">The reference type.</param>
    /// <returns><see langword="true"/> for a future reference.</returns>
    public bool IsReferenceSignBiased(Av1ReferenceFrameType referenceFrame) => this.referenceSides[(int)referenceFrame] > 0;

    /// <summary>
    /// Gets the temporal candidate over a 4x4 position, scaled to a reference of the current frame.
    /// Reference: the tpl_mvs lookup and projection in add_tpl_ref_mv().
    /// </summary>
    /// <param name="modeInfoRow">The zero-based 4x4 row.</param>
    /// <param name="modeInfoColumn">The zero-based 4x4 column.</param>
    /// <param name="referenceFrame">The reference type of the candidate.</param>
    /// <param name="orderHintInfo">The sequence order-hint configuration.</param>
    /// <param name="allowHighPrecision">A value indicating whether one-eighth-sample precision is kept.</param>
    /// <param name="forceInteger">A value indicating whether integer-sample precision is required.</param>
    /// <param name="motionVector">Receives the scaled candidate.</param>
    /// <returns><see langword="true"/> when a projected vector covers the position.</returns>
    public bool TryGetProjectedTemporalMotionVector(
        int modeInfoRow,
        int modeInfoColumn,
        Av1ReferenceFrameType referenceFrame,
        ObuOrderHintInfo orderHintInfo,
        bool allowHighPrecision,
        bool forceInteger,
        out Av1MotionVector motionVector)
    {
        motionVector = default;
        if (!this.useTemporalMotionField)
        {
            return false;
        }

        int index = ((modeInfoRow >> 1) * this.temporalMotionField.Stride) + (modeInfoColumn >> 1);
        Av1FrameInfo.TemporalMotionFieldEntry entry = this.temporalMotionField.Owner.Memory.Span[index];
        if (entry.ReferenceFrameOffset <= 0)
        {
            return false;
        }

        int targetReferenceOffset = orderHintInfo.GetRelativeDistance(this.orderHint, this.referenceOrderHints[(int)referenceFrame]);
        motionVector = entry.MotionVector
            .ProjectTemporal(targetReferenceOffset, entry.ReferenceFrameOffset)
            .LowerPrecision(allowHighPrecision, forceInteger);

        return true;
    }

    /// <summary>
    /// Saves the motion vectors of the coded frame for later projection. Reference: av1_copy_frame_mvs() for every
    /// coded block. A block fills the 8x8 cells it starts in, and a later block that starts in the same cell replaces
    /// the entry, so each cell holds the block that covers its bottom-right 4x4 position within the frame.
    /// </summary>
    /// <param name="picture">The completed frame decisions.</param>
    /// <param name="destination">The saved motion field of the coded frame.</param>
    public void SaveFrameMotionVectors(Av1PictureControlSet picture, SavedMotionField destination)
    {
        ObuFrameHeader frameHeader = picture.Parent.FrameHeader;
        destination.OrderHint = frameHeader.OrderHint;
        destination.IsIntra = frameHeader.IsIntra;
        this.referenceOrderHints.CopyTo(destination.ReferenceOrderHints, 0);

        Av1FrameInfo.MotionFieldStorage<Av1FrameInfo.RetainedMotionFieldEntry> field = destination.Field;
        Span<Av1FrameInfo.RetainedMotionFieldEntry> entries = field.Owner.Memory.Span;
        int rowCount = (this.modeInfoRowCount + 1) >> 1;
        int columnCount = (this.modeInfoColumnCount + 1) >> 1;
        for (int row = 0; row < rowCount; row++)
        {
            int modeInfoRow = Math.Min((row << 1) + 1, this.modeInfoRowCount - 1);
            for (int column = 0; column < columnCount; column++)
            {
                int modeInfoColumn = Math.Min((column << 1) + 1, this.modeInfoColumnCount - 1);
                Point position = new(modeInfoColumn, modeInfoRow);
                ref readonly Av1EncoderBlockModeInfo mode = ref picture.GetFromModeInfoGrid(position).Block;
                Av1ReferenceFrameType selectedReference = Av1ReferenceFrameType.None;
                Av1MotionVector selectedMotionVector = default;
                for (int index = 0; index < 2; index++)
                {
                    Av1ReferenceFrameType referenceFrame = index == 0 ? mode.ReferenceFrame : mode.SecondaryReferenceFrame;
                    if (referenceFrame <= Av1ReferenceFrameType.Intra || this.referenceSides[(int)referenceFrame] != 0)
                    {
                        continue;
                    }

                    Av1MotionVector motionVector = index == 0
                        ? picture.GetDisplacementVector(position)
                        : picture.GetSecondaryDisplacementVector(position);

                    if (Math.Abs(motionVector.Row) > ReferenceMotionVectorLimit ||
                        Math.Abs(motionVector.Column) > ReferenceMotionVectorLimit)
                    {
                        continue;
                    }

                    selectedReference = referenceFrame;
                    selectedMotionVector = motionVector;
                }

                entries[(row * field.Stride) + column] = new(selectedMotionVector, selectedReference);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() => this.temporalMotionField.Dispose();

    /// <summary>
    /// Projects the saved vectors of one reference into the temporal field. Reference: motion_field_projection().
    /// </summary>
    /// <param name="orderHintInfo">The sequence order-hint configuration.</param>
    /// <param name="start">The saved motion field of the reference.</param>
    /// <param name="reverseDirection">Whether the projection runs backwards from a past reference.</param>
    /// <returns><see langword="true"/> when the reference can be projected.</returns>
    private bool ProjectMotionField(ObuOrderHintInfo orderHintInfo, SavedMotionField? start, bool reverseDirection)
    {
        // Key and intra-only frames hold no motion. Every frame of the sequence has the same size.
        if (start is null || start.IsIntra)
        {
            return false;
        }

        int startToCurrentFrameOffset = orderHintInfo.GetRelativeDistance(start.OrderHint, this.orderHint);
        if (reverseDirection)
        {
            startToCurrentFrameOffset = -startToCurrentFrameOffset;
        }

        Av1FrameInfo.ProjectMotionFieldEntries(
            orderHintInfo,
            start.Field,
            start.OrderHint,
            start.ReferenceOrderHints,
            startToCurrentFrameOffset,
            reverseDirection,
            this.modeInfoRowCount,
            this.modeInfoColumnCount,
            this.temporalMotionField);

        return true;
    }

    /// <summary>
    /// The motion vectors that a coded frame keeps for later projection. Reference: the mvs, order_hint,
    /// ref_order_hints, and frame_type fields of RefCntBuffer.
    /// </summary>
    internal sealed class SavedMotionField : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SavedMotionField"/> class.
        /// </summary>
        /// <param name="configuration">The configuration providing the field allocator.</param>
        /// <param name="modeInfoColumnCount">The frame width in 4x4 mode-information units.</param>
        /// <param name="modeInfoRowCount">The frame height in 4x4 mode-information units.</param>
        public SavedMotionField(Configuration configuration, int modeInfoColumnCount, int modeInfoRowCount)
        {
            int stride = (modeInfoColumnCount + 1) >> 1;
            int rowCount = (modeInfoRowCount + 1) >> 1;
            this.Field = new(
                configuration.MemoryAllocator.Allocate<Av1FrameInfo.RetainedMotionFieldEntry>(stride * rowCount, AllocationOptions.Clean),
                stride);
        }

        /// <summary>
        /// Gets the saved vector and reference type of every 8x8 cell.
        /// </summary>
        public Av1FrameInfo.MotionFieldStorage<Av1FrameInfo.RetainedMotionFieldEntry> Field { get; }

        /// <summary>
        /// Gets the order hint of each reference of the coded frame, by reference type.
        /// </summary>
        public uint[] ReferenceOrderHints { get; } = new uint[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// Gets or sets the order hint of the coded frame.
        /// </summary>
        public uint OrderHint { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the coded frame is a key or intra-only frame.
        /// </summary>
        public bool IsIntra { get; set; }

        /// <inheritdoc/>
        public void Dispose() => this.Field.Dispose();
    }
}
