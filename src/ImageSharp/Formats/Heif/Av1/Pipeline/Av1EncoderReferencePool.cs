// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Holds the eight reference-map slots of a sequence and the reconstructed frames they point at. A frame buffer
/// stays alive while at least one slot points at it, and returns to the pool when the last slot is refreshed away.
/// Reference: the RefCntBuffer pool behind cm->ref_frame_map, with assign_frame_buffer_p() and
/// decrease_ref_count().
/// </summary>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
internal sealed class Av1EncoderReferencePool<TSample> : IDisposable
    where TSample : unmanaged
{
    private readonly Configuration configuration;
    private readonly int width;
    private readonly int height;
    private readonly int bitDepth;
    private readonly Av1ColorFormat colorFormat;
    private readonly int chromaPositionX;
    private readonly int chromaPositionY;
    private readonly int lumaBorder;
    private readonly int qIndex;
    private readonly Av1EncoderMotionField motionField;
    private readonly Entry?[] slots = new Entry?[Av1Constants.ReferenceFrameCount];
    private readonly List<Entry> free = [];
    private readonly List<Entry> entries = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderReferencePool{TSample}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the frame allocator.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="bitDepth">The native component precision.</param>
    /// <param name="colorFormat">The native luma and chroma sampling layout.</param>
    /// <param name="chromaPositionX">The horizontal chroma position in half-luma-sample units.</param>
    /// <param name="chromaPositionY">The vertical chroma position in half-luma-sample units.</param>
    /// <param name="lumaBorder">The border width and height in luma samples.</param>
    /// <param name="qIndex">The quantizer index that selects the initial distributions of a new frame context.</param>
    /// <param name="motionField">The motion field of the sequence, which sizes the saved motion vectors of a buffer.</param>
    public Av1EncoderReferencePool(
        Configuration configuration,
        int width,
        int height,
        int bitDepth,
        Av1ColorFormat colorFormat,
        int chromaPositionX,
        int chromaPositionY,
        int lumaBorder,
        int qIndex,
        Av1EncoderMotionField motionField)
    {
        this.configuration = configuration;
        this.width = width;
        this.height = height;
        this.bitDepth = bitDepth;
        this.colorFormat = colorFormat;
        this.chromaPositionX = chromaPositionX;
        this.chromaPositionY = chromaPositionY;
        this.lumaBorder = lumaBorder;
        this.qIndex = qIndex;
        this.motionField = motionField;
    }

    /// <summary>
    /// Returns the unused frame buffer with the lowest index for a frame of the sequence size, which keeps whatever
    /// segment map its last frame left. Reference: get_free_fb().
    /// </summary>
    /// <returns>A buffer that no slot points at.</returns>
    public Entry Acquire() => this.Acquire(this.width, this.height);

    /// <summary>
    /// Returns the unused frame buffer with the lowest index for a frame of the given size. A buffer of another size
    /// gets new planes of the frame size and keeps its identity. Reference: get_free_fb(), and the
    /// aom_realloc_frame_buffer() of the current frame buffer in av1_set_frame_size().
    /// </summary>
    /// <param name="frameWidth">The visible luma width of the frame.</param>
    /// <param name="frameHeight">The visible luma height of the frame.</param>
    /// <returns>A buffer that no slot points at.</returns>
    public Entry Acquire(int frameWidth, int frameHeight)
    {
        Entry? entry = null;
        foreach (Entry candidate in this.free)
        {
            if (entry is null || candidate.Id < entry.Id)
            {
                entry = candidate;
            }
        }

        if (entry is not null)
        {
            this.free.Remove(entry);
            if (entry.Buffer.Frame.Width != frameWidth || entry.Buffer.Frame.Height != frameHeight)
            {
                entry.ReplaceBuffer(this.CreateBuffer(frameWidth, frameHeight, entry.Buffer));
            }

            return entry;
        }

        entry = new Entry(
            this.entries.Count,
            this.CreateBuffer(frameWidth, frameHeight, null),
            new Av1FrameEntropyContext(this.qIndex),
            this.motionField.CreateSavedMotionField(this.configuration));

        this.entries.Add(entry);
        return entry;
    }

    /// <summary>
    /// Allocates the planes of a frame of the given size, over the memory of an earlier buffer when it is large enough.
    /// Reference: aom_realloc_frame_buffer().
    /// </summary>
    /// <param name="frameWidth">The visible luma width of the frame.</param>
    /// <param name="frameHeight">The visible luma height of the frame.</param>
    /// <param name="previous">The earlier buffer of the entry, or <see langword="null"/> for a new entry.</param>
    /// <returns>The frame storage.</returns>
    private Av1EncoderFrameBuffer<TSample> CreateBuffer(int frameWidth, int frameHeight, Av1EncoderFrameBuffer<TSample>? previous)
        => new(
            this.configuration,
            frameWidth,
            frameHeight,
            this.bitDepth,
            this.colorFormat,
            this.chromaPositionX,
            this.chromaPositionY,
            this.lumaBorder,
            previous);

    /// <summary>
    /// Gets the buffer a slot points at.
    /// </summary>
    /// <param name="slot">The reference-map slot.</param>
    /// <returns>The buffer, or <see langword="null"/> when no frame has refreshed the slot.</returns>
    public Entry? GetSlot(int slot) => this.slots[slot];

    /// <summary>
    /// Points every slot selected by the refresh mask at the coded frame, and releases buffers that no slot points
    /// at any more. Reference: the refresh_frame_flags loop of av1_update_reference_frames().
    /// </summary>
    /// <param name="current">The buffer holding the coded frame.</param>
    /// <param name="refreshFrameFlags">The eight-bit refresh mask of the coded frame.</param>
    public void Refresh(Entry current, uint refreshFrameFlags)
    {
        for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
        {
            if ((refreshFrameFlags & (1U << slot)) == 0)
            {
                continue;
            }

            Entry? previous = this.slots[slot];
            if (previous == current)
            {
                continue;
            }

            current.ReferenceCount++;
            this.slots[slot] = current;
            if (previous is not null && --previous.ReferenceCount == 0)
            {
                this.free.Add(previous);
            }
        }

        if (current.ReferenceCount == 0)
        {
            this.free.Add(current);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (Entry entry in this.entries)
        {
            entry.Buffer.Dispose();
            entry.MotionField.Dispose();
        }

        this.entries.Clear();
        this.free.Clear();
        Array.Clear(this.slots);
    }

    /// <summary>
    /// One reconstructed frame and the entropy context saved with it.
    /// </summary>
    internal sealed class Entry
    {
        private byte[] segmentMap = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="Entry"/> class.
        /// </summary>
        /// <param name="id">The identity used to detect two references to the same buffer.</param>
        /// <param name="buffer">The frame storage.</param>
        /// <param name="context">The saved frame context.</param>
        /// <param name="motionField">The saved motion vectors.</param>
        public Entry(int id, Av1EncoderFrameBuffer<TSample> buffer, Av1FrameEntropyContext context, Av1EncoderMotionField.SavedMotionField motionField)
        {
            this.Id = id;
            this.Buffer = buffer;
            this.Context = context;
            this.MotionField = motionField;
        }

        /// <summary>
        /// Gets the identity used to detect two references to the same buffer.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the frame storage.
        /// </summary>
        public Av1EncoderFrameBuffer<TSample> Buffer { get; private set; }

        /// <summary>
        /// Gets the adapted distributions saved at the end of the frame. Reference: cur_frame->frame_context.
        /// </summary>
        public Av1FrameEntropyContext Context { get; }

        /// <summary>
        /// Gets the motion vectors saved at the end of the frame. Reference: cur_frame->mvs.
        /// </summary>
        public Av1EncoderMotionField.SavedMotionField MotionField { get; }

        /// <summary>
        /// Gets or sets the number of slots that point at this buffer.
        /// </summary>
        public int ReferenceCount { get; set; }

        /// <summary>
        /// Gets the segmentation state of the frame in the buffer. Reference: cur_frame->seg.
        /// </summary>
        public ObuSegmentationParameters Segmentation { get; } = new();

        /// <summary>
        /// Replaces the frame storage with planes of another size. The new storage took the old memory or released it.
        /// </summary>
        /// <param name="buffer">The new frame storage.</param>
        public void ReplaceBuffer(Av1EncoderFrameBuffer<TSample> buffer) => this.Buffer = buffer;

        /// <summary>
        /// Gets the segment map that the last frame coded into the buffer left, one identifier per 4x4 block, or
        /// zeros before any frame wrote it. Reference: cur_frame->seg_map, which aom_calloc() clears once.
        /// </summary>
        /// <param name="length">The number of 4x4 blocks in the frame.</param>
        /// <returns>The segment map.</returns>
        public Memory<byte> GetSegmentMap(int length)
        {
            if (this.segmentMap.Length != length)
            {
                this.segmentMap = new byte[length];
            }

            return this.segmentMap;
        }
    }
}
