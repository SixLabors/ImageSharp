// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The temporal dependency model. For each frame of a golden group it estimates, per 16x16 block, the cost of intra
/// coding and of the best inter prediction against earlier frames of the group. Then it propagates backwards how much of
/// the information of each block later frames inherit. The rate control and the block coding read the result.
/// </summary>
/// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
/// <typeparam name="TSearchOperator">The motion search sample operator.</typeparam>
/// <typeparam name="TSampleOperator">The model sample operator.</typeparam>
internal sealed partial class Av1TplModel<TSample, TSearchOperator, TSampleOperator> : IDisposable
    where TSample : unmanaged
    where TSearchOperator : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    where TSampleOperator : struct, IAv1TplSampleOperator<TSample>
{
    /// <summary>
    /// The picture identity of an absent picture.
    /// </summary>
    private const int NoPicture = -1;

    /// <summary>
    /// The picture identity of the saved source of the alternate reference of the previous group.
    /// </summary>
    private const int PreviousArfSourceId = -2;

    /// <summary>
    /// The picture identity of the saved model reconstruction of the alternate reference of the previous group.
    /// </summary>
    private const int PreviousArfReconstructionId = -3;

    /// <summary>
    /// The first picture identity of a reference slot buffer. The model adds the buffer identity of the caller.
    /// </summary>
    private const int SlotIdBase = 1 << 20;

    /// <summary>
    /// The first picture identity of a look-ahead source. The model adds the look-ahead offset.
    /// </summary>
    private const int LookaheadIdBase = 2 << 20;

    /// <summary>
    /// The first picture identity of a temporally filtered source. The model adds the group index.
    /// </summary>
    private const int FilteredIdBase = 3 << 20;

    /// <summary>
    /// The lower quantizer limit of the leaf frames of a constant quality model.
    /// </summary>
    private const int MinimumQIndex = 120;

    /// <summary>
    /// The upper quantizer limit of the leaf frames of a constant quality model.
    /// </summary>
    private const int MaximumQIndex = 220;

    /// <summary>
    /// The configuration providing the allocator.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The visible frame width.
    /// </summary>
    private readonly int width;

    /// <summary>
    /// The visible frame height.
    /// </summary>
    private readonly int height;

    /// <summary>
    /// The sample precision.
    /// </summary>
    private readonly Av1BitDepth bitDepth;

    /// <summary>
    /// The sampling layout.
    /// </summary>
    private readonly Av1ColorFormat colorFormat;

    /// <summary>
    /// The horizontal chroma position of the allocated frames.
    /// </summary>
    private readonly int chromaPositionX;

    /// <summary>
    /// The vertical chroma position of the allocated frames.
    /// </summary>
    private readonly int chromaPositionY;

    /// <summary>
    /// The horizontal chroma subsampling shift.
    /// </summary>
    private readonly int subsamplingX;

    /// <summary>
    /// The vertical chroma subsampling shift.
    /// </summary>
    private readonly int subsamplingY;

    /// <summary>
    /// The number of coded planes.
    /// </summary>
    private readonly int planeCount;

    /// <summary>
    /// The number of pool entries: the look-ahead depth, at most <see cref="Av1TplModelConstants.MaximumLagBuffers"/>.
    /// </summary>
    private readonly int lagInFrames;

    /// <summary>
    /// The frame statistics. Model frame i is entry i + 9, and reference slot s is entry 8 - s.
    /// </summary>
    private readonly Av1TplFrameStatistics[] frames;

    /// <summary>
    /// The source picture of each frame entry.
    /// </summary>
    private readonly Av1EncoderFrame<TSample>[] sourcePictures;

    /// <summary>
    /// The identity of each source picture, or <see cref="NoPicture"/>.
    /// </summary>
    private readonly int[] sourceIds;

    /// <summary>
    /// The reconstructed picture of each frame entry.
    /// </summary>
    private readonly Av1EncoderFrame<TSample>[] reconstructionPictures;

    /// <summary>
    /// The identity of each reconstructed picture, or <see cref="NoPicture"/>.
    /// </summary>
    private readonly int[] reconstructionIds;

    /// <summary>
    /// The block statistics pool, one contiguous entry of <see cref="statisticsEntryLength"/> blocks per look-ahead
    /// frame.
    /// </summary>
    private readonly IMemoryOwner<Av1TplBlockStatistics> statisticsPool;

    /// <summary>
    /// The number of block statistics in each entry of <see cref="statisticsPool"/>.
    /// </summary>
    private readonly int statisticsEntryLength;

    /// <summary>
    /// The reconstruction pool, one frame per look-ahead frame.
    /// </summary>
    private readonly Av1EncoderFrameBuffer<TSample>[] reconstructionPool;

    /// <summary>
    /// The saved source of the alternate reference of the previous group.
    /// </summary>
    private readonly Av1EncoderFrameBuffer<TSample> previousArfSource;

    /// <summary>
    /// The saved model reconstruction of the alternate reference of the previous group.
    /// </summary>
    private readonly Av1EncoderFrameBuffer<TSample> previousArfReconstruction;

    /// <summary>
    /// The stale block mode-information fields that the model reads and writes.
    /// </summary>
    private readonly Av1TplModeInfoGrid modeInfo;

    /// <summary>
    /// Whether the pools were released.
    /// </summary>
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplModel{TSample, TSearchOperator, TSampleOperator}"/> class and
    /// allocates the frame statistics, the statistics pool and the reconstruction pool.
    /// </summary>
    /// <param name="configuration">The configuration providing the allocator.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="colorFormat">The sampling layout.</param>
    /// <param name="chromaPositionX">The horizontal chroma position.</param>
    /// <param name="chromaPositionY">The vertical chroma position.</param>
    /// <param name="lagInFrames">The look-ahead depth. The encoder uses the model only when it is more than one.</param>
    public Av1TplModel(
        Configuration configuration,
        int width,
        int height,
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat,
        int chromaPositionX,
        int chromaPositionY,
        int lagInFrames)
    {
        this.configuration = configuration;
        this.width = width;
        this.height = height;
        this.bitDepth = bitDepth;
        this.colorFormat = colorFormat;
        this.chromaPositionX = chromaPositionX;
        this.chromaPositionY = chromaPositionY;
        this.subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422 ? 1 : 0;
        this.subsamplingY = colorFormat == Av1ColorFormat.Yuv420 ? 1 : 0;
        this.planeCount = colorFormat == Av1ColorFormat.Yuv400 ? 1 : 3;
        this.lagInFrames = Math.Min(lagInFrames, Av1TplModelConstants.MaximumLagBuffers);
        this.ModeInfoColumns = Av1Math.AlignPowerOf2(width, 3) >> Av1Constants.ModeInfoSizeLog2;
        this.ModeInfoRows = Av1Math.AlignPowerOf2(height, 3) >> Av1Constants.ModeInfoSizeLog2;

        int length = Av1TplModelConstants.FrameStatisticsLength;
        this.frames = new Av1TplFrameStatistics[length];
        this.sourcePictures = new Av1EncoderFrame<TSample>[length];
        this.sourceIds = new int[length];
        this.reconstructionPictures = new Av1EncoderFrame<TSample>[length];
        this.reconstructionIds = new int[length];
        for (int frame = 0; frame < length; frame++)
        {
            this.frames[frame] = new Av1TplFrameStatistics(this.ModeInfoRows, this.ModeInfoColumns);
        }

        this.sourceIds.AsSpan().Fill(NoPicture);
        this.reconstructionIds.AsSpan().Fill(NoPicture);

        Av1TplFrameStatistics first = this.frames[0];
        this.modeInfo = new Av1TplModeInfoGrid(this.ModeInfoColumns, this.ModeInfoRows);
        this.statisticsEntryLength = first.Width * first.Height;
        this.statisticsPool = configuration.MemoryAllocator.Allocate<Av1TplBlockStatistics>(
            this.statisticsEntryLength * this.lagInFrames,
            AllocationOptions.Clean);

        this.reconstructionPool = new Av1EncoderFrameBuffer<TSample>[this.lagInFrames];
        for (int frame = 0; frame < this.lagInFrames; frame++)
        {
            this.reconstructionPool[frame] = this.CreateFrameBuffer();
        }

        this.previousArfSource = this.CreateFrameBuffer();
        this.previousArfReconstruction = this.CreateFrameBuffer();
        this.PreviousArfDisplayOrder = -1;
        this.CreateScratch();
    }

    /// <summary>
    /// Gets the number of mode-information rows of a frame.
    /// </summary>
    public int ModeInfoRows { get; }

    /// <summary>
    /// Gets the number of mode-information columns of a frame.
    /// </summary>
    public int ModeInfoColumns { get; }

    /// <summary>
    /// Gets a value indicating whether the statistics of the current group are complete.
    /// </summary>
    public bool Ready { get; private set; }

    /// <summary>
    /// Gets the factor that compensates the frame importance when leaf frames were skipped.
    /// </summary>
    public double R0AdjustFactor { get; private set; } = 1.0;

    /// <summary>
    /// Gets the leaf quantizer that the model used. The rate control records it as the base layer quantizer.
    /// </summary>
    public int BaseLayerQIndex { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the last run measured a frame. If it did, the base quantizer index of the encoder
    /// becomes <see cref="BaseLayerQIndex"/>.
    /// </summary>
    public bool MeasuredFrame { get; private set; }

    /// <summary>
    /// Gets the display order of the saved alternate reference of the previous group, or -1.
    /// </summary>
    public long PreviousArfDisplayOrder { get; private set; }

    /// <summary>
    /// Gets the block mode-information fields that the model reads from the previous frame of the encoder. Before each
    /// run, the encoder must record the fields that its previous frame left at every 16x16-aligned position. The grid
    /// starts zeroed, which is the state before the first frame.
    /// </summary>
    public Av1TplModeInfoGrid ModeInfo => this.modeInfo;

    /// <summary>
    /// Gets the statistics of a model frame.
    /// </summary>
    /// <param name="frameIndex">The group index, -8 to 95.</param>
    /// <returns>The frame statistics.</returns>
    public Av1TplFrameStatistics GetFrame(int frameIndex) => this.frames[frameIndex + Av1TplModelConstants.ReferenceFrameSlotCount + 1];

    /// <summary>
    /// Gets the reconstructed picture that the model produced for a frame of the current group.
    /// </summary>
    /// <param name="frameIndex">The group index.</param>
    /// <returns>The reconstructed picture.</returns>
    public Av1EncoderFrame<TSample> GetReconstruction(int frameIndex)
        => this.reconstructionPictures[frameIndex + Av1TplModelConstants.ReferenceFrameSlotCount + 1];

    /// <summary>
    /// Clears the readiness and validity flags and zeroes the statistics pool.
    /// </summary>
    public void InitializeStatistics()
    {
        this.Ready = false;
        for (int frame = 0; frame < this.frames.Length; frame++)
        {
            this.frames[frame].IsValid = false;
        }

        this.statisticsPool.Memory.Span.Clear();
    }

    /// <summary>
    /// Marks the saved alternate reference of the previous group as unusable. The encoder calls it at a key frame.
    /// </summary>
    public void ForgetPreviousGroupAlternate() => this.PreviousArfDisplayOrder = -1;

    /// <summary>
    /// Returns whether a frame of the current group has complete statistics.
    /// </summary>
    /// <param name="groupIndex">The group index of the frame.</param>
    /// <returns><see langword="true"/> when the statistics are ready.</returns>
    public bool IsStatisticsReady(int groupIndex)
    {
        if (!this.Ready)
        {
            return false;
        }

        // A group longer than the buffer capacity has no statistics past it.
        if (groupIndex >= Av1TplModelConstants.MaximumFrameIndex)
        {
            return false;
        }

        return this.GetFrame(groupIndex).IsValid;
    }

    /// <summary>
    /// Saves the source and the model reconstruction of the last displayed frame of a group. The next group can use the
    /// pair as a reference that only its own frames can choose. The encoder calls it after it codes each frame.
    /// </summary>
    /// <param name="group">The golden group.</param>
    /// <param name="groupIndex">The group index of the coded frame.</param>
    /// <param name="statisticsReady">Whether the statistics of the frame were ready before the frame processed them.</param>
    /// <param name="source">The source that the frame was coded from, after temporal filtering.</param>
    /// <param name="displayOrderHint">The display order of the frame.</param>
    public void SavePreviousGroupAlternate(
        Av1TplGroup group, int groupIndex, bool statisticsReady, Av1EncoderFrame<TSample> source, int displayOrderHint)
    {
        if (!statisticsReady ||
            group.UpdateType[groupIndex] is Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay)
        {
            return;
        }

        for (int index = 0; index < group.Size; index++)
        {
            if (group.DisplayIndex[index] > group.DisplayIndex[groupIndex])
            {
                return;
            }
        }

        // The last frame of the group in display order: keep its source and the reconstruction the model made of it.
        CopyFrame(source, this.previousArfSource.Frame);
        CopyFrame(this.GetReconstruction(groupIndex), this.previousArfReconstruction.Frame);
        this.PreviousArfDisplayOrder = displayOrderHint;
    }

    /// <summary>
    /// Releases the pools and the scratch storage.
    /// </summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.statisticsPool.Dispose();
        for (int frame = 0; frame < this.reconstructionPool.Length; frame++)
        {
            this.reconstructionPool[frame].Dispose();
        }

        this.previousArfSource.Dispose();
        this.previousArfReconstruction.Dispose();
        this.DisposeScratch();
    }

    /// <summary>
    /// Copies the visible samples of a frame and extends its borders.
    /// </summary>
    /// <param name="source">The frame to copy.</param>
    /// <param name="destination">The frame that receives the copy. It has the same geometry as the source.</param>
    private static void CopyFrame(Av1EncoderFrame<TSample> source, Av1EncoderFrame<TSample> destination)
    {
        int planes = source.IsMonochrome ? 1 : 3;
        for (int plane = 0; plane < planes; plane++)
        {
            Av1PlaneRegion<TSample> from = source.CodedView.GetPlane((Av1Plane)plane);
            Av1PlaneRegion<TSample> to = destination.CodedView.GetPlane((Av1Plane)plane);
            int rows = plane == 0 ? source.Height : (source.Height + source.ChromaSubsamplingY) >> source.ChromaSubsamplingY;
            int columns = plane == 0 ? source.Width : (source.Width + source.ChromaSubsamplingX) >> source.ChromaSubsamplingX;
            ReadOnlySpan<TSample> fromSamples = from.Samples;
            Span<TSample> toSamples = to.Samples;
            for (int row = 0; row < rows; row++)
            {
                fromSamples.Slice(from.GetOffset(0, row), columns).CopyTo(toSamples.Slice(to.GetOffset(0, row), columns));
            }
        }

        destination.ExtendBorders();
    }

    /// <summary>
    /// Allocates one frame of the model geometry with a border of <see cref="Av1TplModelConstants.Border"/> samples.
    /// </summary>
    /// <returns>The frame buffer.</returns>
    private Av1EncoderFrameBuffer<TSample> CreateFrameBuffer()
        => new(
            this.configuration,
            this.width,
            this.height,
            this.bitDepth.GetBitCount(),
            this.colorFormat,
            this.chromaPositionX,
            this.chromaPositionY,
            Av1TplModelConstants.Border);
}
