// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Estimates the noise of a real-time source from the change between successive sources in areas that stayed still for
/// a few frames. Cyclic refresh, the estimated search and variance partitioning read the level. Reference:
/// NOISE_ESTIMATE.
/// </summary>
internal sealed class Av1NoiseEstimate
{
    /// <summary>
    /// The lowest noise level. Reference: kLowLow.
    /// </summary>
    public const int LowestLevel = 0;

    /// <summary>
    /// The low noise level. Reference: kLow.
    /// </summary>
    public const int LowLevel = 1;

    /// <summary>
    /// The medium noise level. Reference: kMedium.
    /// </summary>
    public const int MediumLevel = 2;

    /// <summary>
    /// The high noise level. Reference: kHigh.
    /// </summary>
    public const int HighLevel = 3;

    /// <summary>
    /// The histogram bins of the block variances. Reference: MAX_VAR_HIST_BINS.
    /// </summary>
    private const int VarianceBinCount = 20;

    /// <summary>
    /// The frames between two estimates. Reference: the frame_period of av1_update_noise_estimate().
    /// </summary>
    private const int FramePeriod = 8;

    /// <summary>
    /// The frames an 8x8 block must keep a small motion vector to count as still. Reference: thresh_consec_zeromv.
    /// </summary>
    private const int StillFrameThreshold = 2;

    /// <summary>
    /// The frames each 8x8 block kept a small motion vector to the last frame, at most 255. Reference:
    /// cpi->consec_zero_mv.
    /// </summary>
    private readonly byte[] consecutiveZeroMotion;

    /// <summary>
    /// The frame width in 4x4 units. Reference: mi_params.mi_cols.
    /// </summary>
    private readonly int modeInfoColumns;

    /// <summary>
    /// The frame height in 4x4 units. Reference: mi_params.mi_rows.
    /// </summary>
    private readonly int modeInfoRows;

    /// <summary>
    /// The frame width in samples. Reference: cm->width.
    /// </summary>
    private readonly int width;

    /// <summary>
    /// The frame height in samples. Reference: cm->height.
    /// </summary>
    private readonly int height;

    /// <summary>
    /// The running estimate above which the source counts as noisy: above half of it is low noise, above it medium
    /// and above twice it high. Reference: ne->thresh.
    /// </summary>
    private readonly int threshold;

    /// <summary>
    /// The running estimate above which a sudden rise completes the estimate at once. Reference: ne->adapt_thresh.
    /// </summary>
    private readonly int adaptThreshold;

    /// <summary>
    /// The running estimate: a weighted average of 40 times the largest histogram bin. Reference: ne->value.
    /// </summary>
    private int value;

    /// <summary>
    /// The estimates since the level last changed. Reference: ne->count.
    /// </summary>
    private int count;

    /// <summary>
    /// The estimates the level waits for before it changes: 15 at first, then 30. Reference: ne->num_frames_estimate.
    /// </summary>
    private int framesToEstimate = 15;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1NoiseEstimate"/> class for a sequence that allows the
    /// estimate: one-pass constant bit rate cyclic refresh of 8-bit frames without layers. Reference:
    /// av1_noise_estimate_init() with enable_noise_estimation().
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="modeInfoColumns">The frame width in 4x4 units.</param>
    /// <param name="modeInfoRows">The frame height in 4x4 units.</param>
    public Av1NoiseEstimate(int width, int height, int modeInfoColumns, int modeInfoRows)
    {
        this.width = width;
        this.height = height;
        this.modeInfoColumns = modeInfoColumns;
        this.modeInfoRows = modeInfoRows;
        this.consecutiveZeroMotion = new byte[(modeInfoRows * modeInfoColumns) >> 2];

        // Larger frames start at a higher level and need a larger estimate to count as noisy, because their 16x16
        // blocks change more between frames for the same noise. A rise half again above the threshold completes the
        // estimate early.
        long area = (long)width * height;
        this.Level = area < 1280 * 720 ? LowestLevel : LowLevel;
        this.threshold = area >= 1920 * 1080 ? 200 : area >= 1280 * 720 ? 140 : area >= 640 * 360 ? 115 : 90;
        this.adaptThreshold = (3 * this.threshold) >> 1;
    }

    /// <summary>
    /// Gets a value indicating whether the estimate is on, which it is from the first update. Reference: ne->enabled.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Gets the noise level of the last completed estimate. Reference: ne->level.
    /// </summary>
    public int Level { get; private set; }

    /// <summary>
    /// Returns the noise level of the running estimate. Reference: av1_noise_estimate_extract_level().
    /// </summary>
    /// <returns>The noise level.</returns>
    public int ExtractLevel()
        => this.value > (this.threshold << 1) ? HighLevel
            : this.value > this.threshold ? MediumLevel
            : this.value > (this.threshold >> 1) ? LowLevel
            : LowestLevel;

    /// <summary>
    /// Clears the still block counts at an intra frame. Reference: the consec_zero_mv reset of
    /// encode_without_recode().
    /// </summary>
    public void ResetStillBlocks() => this.consecutiveZeroMotion.AsSpan().Clear();

    /// <summary>
    /// Counts the frames each 8x8 unit of a coded block kept a small motion vector to the last frame. Reference:
    /// update_zeromv_cnt().
    /// </summary>
    /// <param name="modeInfoPosition">The block position in 4x4 units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="lastReference">Whether the block predicts from the last frame.</param>
    /// <param name="vector">The block's first motion vector.</param>
    public void CountStillBlock(Point modeInfoPosition, Av1BlockSize blockSize, bool lastReference, Av1MotionVector vector)
    {
        if (!lastReference)
        {
            return;
        }

        int stride = this.modeInfoColumns >> 1;
        int columns = Math.Min((this.modeInfoColumns - modeInfoPosition.X) >> 1, blockSize.Get4x4WideCount() >> 1);
        int rows = Math.Min((this.modeInfoRows - modeInfoPosition.Y) >> 1, blockSize.Get4x4HighCount() >> 1);
        int blockIndex = ((modeInfoPosition.Y >> 1) * stride) + (modeInfoPosition.X >> 1);
        bool still = Math.Abs((int)vector.Row) < 10 && Math.Abs((int)vector.Column) < 10;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                ref byte frames = ref this.consecutiveZeroMotion[blockIndex + (y * stride) + x];
                frames = still ? (byte)Math.Min(frames + 1, 255) : (byte)0;
            }
        }
    }

    /// <summary>
    /// Updates the estimate every eighth frame from a histogram of the 16x16 source changes in still areas, and
    /// changes the level after enough estimates. The frame size of a sequence never changes, so the resize check is
    /// left out. Reference: av1_update_noise_estimate().
    /// </summary>
    /// <param name="frameNumber">
    /// The index of the frame from its key frame, which a key frame restarts. Reference: current_frame.frame_number.
    /// </param>
    /// <param name="encodedFrameCount">The frames coded before this one. Reference: svc.num_encoded_top_layer.</param>
    /// <param name="framesSinceKey">The frames since the last key frame. Reference: rc->frames_since_key.</param>
    /// <param name="averageFrameLowMotion">The running zero motion percentage. Reference: rc->avg_frame_low_motion.</param>
    /// <param name="sceneChange">Whether the frame is a scene change. Reference: rc->high_source_sad.</param>
    /// <param name="source">The luma plane of the frame's source.</param>
    /// <param name="lastSource">The luma plane of the previous source.</param>
    /// <param name="hasLastSource">Whether a previous source exists, which the first frame lacks.</param>
    public void Update(
        int frameNumber,
        int encodedFrameCount,
        int framesSinceKey,
        int averageFrameLowMotion,
        bool sceneChange,
        Av1PlaneRegion<byte> source,
        Av1PlaneRegion<byte> lastSource,
        bool hasLastSource)
    {
        bool lowResolution = this.width <= 352 && this.height <= 288;
        this.Enabled = true;
        if (frameNumber % FramePeriod != 0 || !hasLastSource)
        {
            return;
        }

        // High motion content forces the lowest level.
        if (frameNumber > 60 && encodedFrameCount > 1 && framesSinceKey > 1 &&
            averageFrameLowMotion < (lowResolution ? 60 : 40))
        {
            this.Level = LowestLevel;
            this.count = 0;
            this.framesToEstimate = 10;
            return;
        }

        const uint binSize = 100;
        Span<uint> histogram = stackalloc uint[VarianceBinCount];
        histogram.Clear();
        int stride = this.modeInfoColumns >> 1;

        // The frame is still when enough of its 8x8 blocks are.
        int stillBlocks = 0;
        for (int row = 0; row < this.modeInfoRows; row += 2)
        {
            for (int column = 0; column < this.modeInfoColumns; column += 2)
            {
                if (this.consecutiveZeroMotion[((row >> 1) * stride) + (column >> 1)] > StillFrameThreshold)
                {
                    stillBlocks++;
                }
            }
        }

        // The frame has (rows x columns) / 4 units of 8x8, so the bound is three eighths of them: the frame is still
        // when at least that share of its 8x8 blocks are.
        bool frameLowMotion = stillBlocks >= ((3 * (this.modeInfoRows * this.modeInfoColumns)) >> 2) >> 3;

        // One 16x16 block in four, away from the right and bottom edges, adds its change against the previous source
        // when its four 8x8 blocks stayed still.
        for (int row = 0; row < this.modeInfoRows - 3; row += 8)
        {
            for (int column = 0; column < this.modeInfoColumns - 3; column += 8)
            {
                int blockIndex = ((row >> 1) * stride) + (column >> 1);
                int stillFrames = Math.Min(
                    this.consecutiveZeroMotion[blockIndex],
                    Math.Min(
                        this.consecutiveZeroMotion[blockIndex + 1],
                        Math.Min(this.consecutiveZeroMotion[blockIndex + stride], this.consecutiveZeroMotion[blockIndex + stride + 1])));

                if (frameLowMotion && stillFrames > StillFrameThreshold && !sceneChange)
                {
                    Point origin = new(column << 2, row << 2);
                    Av1MotionSearchBase.ByteOperator.GetMoments(
                        Av1TransformBlockEncoder.GetPlaneSpan(source, origin),
                        source.Stride,
                        Av1TransformBlockEncoder.GetPlaneSpan(lastSource, origin),
                        lastSource.Stride,
                        16,
                        16,
                        out int sum,
                        out long squaredError);

                    uint variance = (uint)(squaredError - (((long)sum * sum) >> 8));
                    uint bin = variance / binSize;
                    if (bin < VarianceBinCount)
                    {
                        histogram[(int)bin]++;
                    }
                    else if (bin < 3 * (VarianceBinCount >> 1))
                    {
                        // The tail.
                        histogram[VarianceBinCount - 1]++;
                    }
                }
            }
        }

        // A darker scene flattens the histogram and moves it toward zero.
        if (histogram[0] > 10 && histogram[VarianceBinCount - 1] > histogram[0] >> 2)
        {
            histogram[0] = 0;
            histogram[1] >>= 2;
            histogram[2] >>= 2;
            histogram[3] >>= 2;
            histogram[4] >>= 1;
            histogram[5] >>= 1;
            histogram[6] = (3 * histogram[6]) >> 1;
            histogram[VarianceBinCount - 1] >>= 1;
        }

        // Smooth the histogram and find its largest bin.
        uint largestBin = 0;
        uint largestCount = 0;
        for (int bin = 0; bin < VarianceBinCount; bin++)
        {
            uint average = bin == 0 ? (histogram[0] + histogram[1] + histogram[2]) / 3
                : bin == VarianceBinCount - 1 ? histogram[VarianceBinCount - 1] >> 2
                : bin == VarianceBinCount - 2 ? (histogram[bin - 1] + (2 * histogram[bin]) + (histogram[bin + 1] >> 1) + 2) >> 2
                : (histogram[bin - 1] + (2 * histogram[bin]) + histogram[bin + 1] + 2) >> 2;

            if (average > largestCount)
            {
                largestCount = average;
                largestBin = (uint)bin;
            }
        }

        // The scale of 40 matches the thresholds.
        this.value = (int)(((3 * this.value) + (largestBin * 40)) >> 2);

        // A sudden rise completes the estimate at once.
        if (this.Level < MediumLevel && this.value > this.adaptThreshold)
        {
            this.count = this.framesToEstimate;
        }
        else
        {
            this.count++;
        }

        if (this.count == this.framesToEstimate)
        {
            this.framesToEstimate = 30;
            this.count = 0;
            this.Level = this.ExtractLevel();
        }
    }
}
