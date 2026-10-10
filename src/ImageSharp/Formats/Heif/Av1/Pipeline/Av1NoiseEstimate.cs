// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Estimates the noise of a real-time source from the change between successive sources in areas that stayed still for a few frames.
/// Cyclic refresh, the estimated search and variance partitioning read the level.
/// </summary>
internal sealed class Av1NoiseEstimate
{
    /// <summary>
    /// The lowest noise level.
    /// </summary>
    public const int LowestLevel = 0;

    /// <summary>
    /// The low noise level.
    /// </summary>
    public const int LowLevel = 1;

    /// <summary>
    /// The medium noise level.
    /// </summary>
    public const int MediumLevel = 2;

    /// <summary>
    /// The high noise level.
    /// </summary>
    public const int HighLevel = 3;

    /// <summary>
    /// The number of histogram bins for the block variances.
    /// </summary>
    private const int VarianceBinCount = 20;

    /// <summary>
    /// The number of frames between two estimates.
    /// </summary>
    private const int FramePeriod = 8;

    /// <summary>
    /// The number of frames that an 8x8 block must keep a small motion vector to count as still.
    /// </summary>
    private const int StillFrameThreshold = 2;

    /// <summary>
    /// The number of frames that each 8x8 block kept a small motion vector to the last frame, at most 255.
    /// </summary>
    private readonly byte[] consecutiveZeroMotion;

    /// <summary>
    /// The frame width in 4x4 units.
    /// </summary>
    private readonly int modeInfoColumns;

    /// <summary>
    /// The frame height in 4x4 units.
    /// </summary>
    private readonly int modeInfoRows;

    /// <summary>
    /// The frame width in samples.
    /// </summary>
    private readonly int width;

    /// <summary>
    /// The frame height in samples.
    /// </summary>
    private readonly int height;

    /// <summary>
    /// The noise threshold of the running estimate. An estimate above half of it is low noise.
    /// An estimate above the threshold is medium noise, and an estimate above twice the threshold is high noise.
    /// </summary>
    private readonly int threshold;

    /// <summary>
    /// The running estimate above which a sudden rise completes the estimate at once.
    /// </summary>
    private readonly int adaptThreshold;

    /// <summary>
    /// The running estimate. This is a weighted average of 40 times the largest histogram bin.
    /// </summary>
    private int value;

    /// <summary>
    /// The number of estimates since the level last changed.
    /// </summary>
    private int count;

    /// <summary>
    /// The number of estimates that the level waits for before it changes.
    /// The value is 15 at first, 30 after each level change, and 10 after high motion forces the lowest level.
    /// </summary>
    private int framesToEstimate = 15;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1NoiseEstimate"/> class for a sequence that allows the estimate.
    /// Such a sequence uses one-pass constant bit rate with cyclic refresh, and has 8-bit frames without layers.
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

        // Larger frames start at a higher level and need a larger estimate to count as noisy.
        // Their 16x16 blocks change more between frames for the same noise.
        // A rise to 1.5 times the threshold completes the estimate early.
        long area = (long)width * height;
        this.Level = area < 1280 * 720 ? LowestLevel : LowLevel;
        this.threshold = area >= 1920 * 1080 ? 200 : area >= 1280 * 720 ? 140 : area >= 640 * 360 ? 115 : 90;
        this.adaptThreshold = (3 * this.threshold) >> 1;
    }

    /// <summary>
    /// Gets a value indicating whether the estimate is on. The estimate is on from the first update.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Gets the noise level of the last completed estimate.
    /// </summary>
    public int Level { get; private set; }

    /// <summary>
    /// Returns the noise level of the running estimate.
    /// </summary>
    /// <returns>The noise level.</returns>
    public int ExtractLevel()
        => this.value > (this.threshold << 1) ? HighLevel
            : this.value > this.threshold ? MediumLevel
            : this.value > (this.threshold >> 1) ? LowLevel
            : LowestLevel;

    /// <summary>
    /// Clears the still block counts at an intra frame.
    /// </summary>
    public void ResetStillBlocks() => this.consecutiveZeroMotion.AsSpan().Clear();

    /// <summary>
    /// Counts the frames that each 8x8 unit of a coded block kept a small motion vector to the last frame.
    /// </summary>
    /// <param name="modeInfoPosition">The block position in 4x4 units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="lastReference">Whether the block predicts from the last frame.</param>
    /// <param name="vector">The first motion vector of the block.</param>
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

        // A vector shorter than 10 eighth-samples in each direction counts as still. Any other vector restarts the count.
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
    /// Updates the estimate every eighth frame from a histogram of the 16x16 source changes in still areas.
    /// The level changes after enough estimates. The frame size of a sequence never changes, so no resize check occurs.
    /// </summary>
    /// <param name="frameNumber">
    /// The index of the frame from its key frame. A key frame restarts the index.
    /// </param>
    /// <param name="encodedFrameCount">The number of frames coded before this one.</param>
    /// <param name="framesSinceKey">The number of frames since the last key frame.</param>
    /// <param name="averageFrameLowMotion">The running zero motion percentage.</param>
    /// <param name="sceneChange">Whether the frame is a scene change.</param>
    /// <param name="source">The luma plane of the source of the frame.</param>
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

        // The frame has (rows x columns) / 4 units of 8x8, so the bound is three eighths of them.
        // The frame is still when at least that share of its 8x8 blocks are still.
        bool frameLowMotion = stillBlocks >= ((3 * (this.modeInfoRows * this.modeInfoColumns)) >> 2) >> 3;

        // The loop visits the top-left 16x16 block of each 32x32 area, away from the right and bottom edges.
        // The block adds its change against the previous source when its four 8x8 blocks stayed still.
        ReadOnlySpan<byte> sourceSamples = source.Samples;
        ReadOnlySpan<byte> lastSamples = lastSource.Samples;
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
                        sourceSamples[source.GetOffset(origin.X, origin.Y)..],
                        source.Stride,
                        lastSamples[lastSource.GetOffset(origin.X, origin.Y)..],
                        lastSource.Stride,
                        16,
                        16,
                        out int sum,
                        out long squaredError);

                    // The variance of the 256 sample differences is the squared error minus the squared sum divided by 256.
                    uint variance = (uint)(squaredError - (((long)sum * sum) >> 8));
                    uint bin = variance / binSize;
                    if (bin < VarianceBinCount)
                    {
                        histogram[(int)bin]++;
                    }
                    else if (bin < 3 * (VarianceBinCount >> 1))
                    {
                        // A variance up to 1.5 times the histogram range counts in the last bin. A larger variance is not counted.
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

        // The new estimate gets a weight of one quarter. The scale of 40 matches the thresholds.
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
