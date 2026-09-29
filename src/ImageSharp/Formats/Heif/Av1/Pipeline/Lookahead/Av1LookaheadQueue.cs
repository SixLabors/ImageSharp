// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// Holds the source frames that wait for coding, in display order, and the most recent frame that left the queue.
/// The buffers are allocated once and reused as a ring. Reference: struct lookahead_ctx with av1_lookahead_init(),
/// av1_lookahead_push(), av1_lookahead_pop(), and av1_lookahead_peek() for the encode stage.
/// </summary>
/// <typeparam name="TSample">The component sample type.</typeparam>
internal sealed class Av1LookaheadQueue<TSample> : IDisposable
    where TSample : unmanaged
{
    /// <summary>
    /// The number of earlier frames kept for backward peeks. Reference: MAX_PRE_FRAMES.
    /// </summary>
    private const int PreviousFrameCount = 1;

    private readonly Av1EncoderFrameBuffer<TSample>[] buffers;
    private readonly int[] displayIndices;
    private int readIndex;
    private int writeIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LookaheadQueue{TSample}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration that supplies the memory allocator.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="colorFormat">The chroma layout.</param>
    /// <param name="chromaPositionX">The horizontal chroma sample position.</param>
    /// <param name="chromaPositionY">The vertical chroma sample position.</param>
    /// <param name="lumaBorder">The luma border in samples.</param>
    /// <param name="depth">The number of frames the queue must hold before the encode stage takes one.
    /// Reference: the depth plus num_lap_buffers of av1_lookahead_init().</param>
    public Av1LookaheadQueue(
        Configuration configuration,
        int width,
        int height,
        int bitDepth,
        Av1ColorFormat colorFormat,
        int chromaPositionX,
        int chromaPositionY,
        int lumaBorder,
        int depth)
    {
        this.PopSize = Math.Max(depth, 1);
        int capacity = this.PopSize + PreviousFrameCount;
        this.buffers = new Av1EncoderFrameBuffer<TSample>[capacity];
        this.displayIndices = new int[capacity];
        try
        {
            for (int i = 0; i < capacity; i++)
            {
                this.buffers[i] = new Av1EncoderFrameBuffer<TSample>(
                    configuration,
                    width,
                    height,
                    bitDepth,
                    colorFormat,
                    chromaPositionX,
                    chromaPositionY,
                    lumaBorder);
            }
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the number of frames that wait for coding. Reference: read_ctxs[ENCODE_STAGE].sz.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets the number of frames the queue holds when it is full. Reference: read_ctxs[ENCODE_STAGE].pop_sz.
    /// </summary>
    public int PopSize { get; }

    /// <summary>
    /// Gets the number of frames pushed so far. Reference: push_frame_count.
    /// </summary>
    public int PushCount { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the queue holds as many frames as the encode stage waits for.
    /// Reference: av1_lookahead_full().
    /// </summary>
    public bool IsFull => this.Count >= this.PopSize;

    /// <summary>
    /// Returns the buffer that receives the next source frame. The frame joins the queue with <see cref="EndPush"/>.
    /// </summary>
    /// <returns>The buffer to fill.</returns>
    public Av1EncoderFrameBuffer<TSample> BeginPush()
    {
        if (this.Count + PreviousFrameCount >= this.buffers.Length)
        {
            throw new InvalidOperationException("The lookahead queue is full.");
        }

        return this.buffers[this.writeIndex];
    }

    /// <summary>
    /// Adds the frame written into the buffer from <see cref="BeginPush"/>. Reference: av1_lookahead_push().
    /// </summary>
    public void EndPush()
    {
        this.displayIndices[this.writeIndex] = this.PushCount;
        this.writeIndex = this.Next(this.writeIndex);
        this.PushCount++;
        this.Count++;
    }

    /// <summary>
    /// Returns a queued frame, or the frame that most recently left the queue. Reference: av1_lookahead_peek().
    /// </summary>
    /// <param name="index">The position after the first queued frame, or -1 for the most recent frame that left.</param>
    /// <returns>The frame buffer, or <see langword="null"/> when no frame is at that position.</returns>
    public Av1EncoderFrameBuffer<TSample>? Peek(int index)
    {
        if (index >= 0)
        {
            if (index >= this.Count)
            {
                return null;
            }

            index += this.readIndex;
            if (index >= this.buffers.Length)
            {
                index -= this.buffers.Length;
            }

            return this.buffers[index];
        }

        if (-index > PreviousFrameCount || this.PushCount - this.Count < -index)
        {
            return null;
        }

        index += this.readIndex;
        if (index < 0)
        {
            index += this.buffers.Length;
        }

        return this.buffers[index];
    }

    /// <summary>
    /// Returns the display index of a queued frame. Reference: the display_idx of struct lookahead_entry.
    /// </summary>
    /// <param name="index">The position after the first queued frame.</param>
    /// <returns>The display index of the frame.</returns>
    public int GetDisplayIndex(int index)
    {
        index += this.readIndex;
        if (index >= this.buffers.Length)
        {
            index -= this.buffers.Length;
        }

        return this.displayIndices[index];
    }

    /// <summary>
    /// Removes the first queued frame. It stays available to a backward peek until the next pop.
    /// Reference: av1_lookahead_pop() with drain.
    /// </summary>
    public void Pop()
    {
        if (this.Count == 0)
        {
            throw new InvalidOperationException("The lookahead queue is empty.");
        }

        this.readIndex = this.Next(this.readIndex);
        this.Count--;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (int i = 0; i < this.buffers.Length; i++)
        {
            this.buffers[i]?.Dispose();
        }
    }

    private int Next(int index) => index + 1 == this.buffers.Length ? 0 : index + 1;
}
