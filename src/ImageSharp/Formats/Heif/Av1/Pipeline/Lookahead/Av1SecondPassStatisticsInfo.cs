// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// A ring of first-pass statistics around the frame being coded: the frames the look-ahead has analysed after it
/// and up to two frames before it. Key frame detection addresses the statistics relative to the current frame, so
/// an offset of -1 is the previous frame. The ring holds unmodified copies; the flash, noise and correlation
/// estimates are written to the separate linear buffer only. Reference: FIRSTPASS_INFO.
/// </summary>
internal sealed class Av1SecondPassStatisticsInfo
{
    /// <summary>
    /// The capacity of the ring. Reference: FIRSTPASS_INFO_STATIC_BUF_SIZE, which is MAX_LAP_BUFFERS plus
    /// FIRSTPASS_INFO_STATS_PAST_MIN.
    /// </summary>
    private const int BufferSize = 48 + 1;

    /// <summary>
    /// The statistics. Reference: static_stats_buf.
    /// </summary>
    private readonly Av1FirstPassStatistics[] buffer = new Av1FirstPassStatistics[BufferSize];

    /// <summary>
    /// The ring position of the oldest held frame. Reference: start_index.
    /// </summary>
    private int startIndex;

    /// <summary>
    /// The ring position of the current frame. Reference: cur_index.
    /// </summary>
    private int currentIndex;

    /// <summary>
    /// The number of held frames. Reference: stats_count.
    /// </summary>
    private int statisticsCount;

    /// <summary>
    /// The number of held frames from the current frame on. Reference: future_stats_count.
    /// </summary>
    private int futureCount;

    /// <summary>
    /// The number of held frames before the current frame. Reference: past_stats_count.
    /// </summary>
    private int pastCount;

    /// <summary>
    /// Gets the number of held frames before the current frame. Reference: past_stats_count.
    /// </summary>
    public int PastCount => this.pastCount;

    /// <summary>
    /// Appends the statistics of the newest analysed frame. A full ring drops the statistics.
    /// Reference: av1_firstpass_info_push(), without the running total that only the variable-bitrate error
    /// weighting reads.
    /// </summary>
    /// <param name="statistics">The statistics.</param>
    public void Push(Av1FirstPassStatistics statistics)
    {
        if (this.statisticsCount < BufferSize)
        {
            int next = (this.startIndex + this.statisticsCount) % BufferSize;
            this.buffer[next] = statistics;
            ++this.statisticsCount;
            ++this.futureCount;
        }
    }

    /// <summary>
    /// Makes the next frame the current frame, unless the current frame is the newest held frame.
    /// Reference: av1_firstpass_info_move_cur_index().
    /// </summary>
    /// <returns>Whether the current frame moved.</returns>
    public bool MoveCurrentIndex()
    {
        if (this.futureCount > 1)
        {
            this.currentIndex = (this.currentIndex + 1) % BufferSize;
            --this.futureCount;
            ++this.pastCount;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Drops the oldest held frame when it is before the current frame. Reference: av1_firstpass_info_pop().
    /// </summary>
    public void Pop()
    {
        if (this.statisticsCount > 0 && this.pastCount > 0)
        {
            this.startIndex = (this.startIndex + 1) % BufferSize;
            --this.statisticsCount;
            --this.pastCount;
        }
    }

    /// <summary>
    /// Makes the next frame the current frame and drops the oldest held frame.
    /// Reference: av1_firstpass_info_move_cur_index_and_pop().
    /// </summary>
    public void MoveCurrentIndexAndPop()
    {
        if (this.MoveCurrentIndex())
        {
            this.Pop();
        }
    }

    /// <summary>
    /// Returns whether the ring holds the frame at an offset from the current frame. Reference: the null test of
    /// av1_firstpass_info_peek().
    /// </summary>
    /// <param name="offset">The offset from the current frame.</param>
    /// <returns>Whether the frame is held.</returns>
    public bool Contains(int offset) => offset >= -this.pastCount && offset < this.futureCount;

    /// <summary>
    /// Returns the statistics of a held frame at an offset from the current frame. The caller has checked
    /// <see cref="Contains(int)"/>. Reference: av1_firstpass_info_peek().
    /// </summary>
    /// <param name="offset">The offset from the current frame.</param>
    /// <returns>The statistics.</returns>
    public ref readonly Av1FirstPassStatistics Peek(int offset)
        => ref this.buffer[(this.currentIndex + offset + BufferSize) % BufferSize];

    /// <summary>
    /// Returns the number of held frames from an offset from the current frame on.
    /// Reference: av1_firstpass_info_future_count().
    /// </summary>
    /// <param name="offset">The offset from the current frame.</param>
    /// <returns>The number of frames.</returns>
    public int GetFutureCount(int offset) => offset < this.futureCount ? this.futureCount - offset : 0;
}
