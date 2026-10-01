// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The temporal dependency model: for each frame of a golden group it estimates, per 16x16 block, the cost of intra
/// coding and of the best inter prediction against earlier frames of the group, then propagates backwards how much of
/// each block's information later frames inherit. The rate control and the block coding read the result.
/// Reference: TplParams and the functions of tpl_model.c.
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
    /// The picture identity of an absent picture. Reference: a NULL YV12_BUFFER_CONFIG pointer.
    /// </summary>
    private const int NoPicture = -1;

    /// <summary>
    /// The picture identity of the saved source of the previous group's alternate reference.
    /// </summary>
    private const int PreviousArfSourceId = -2;

    /// <summary>
    /// The picture identity of the saved model reconstruction of the previous group's alternate reference.
    /// </summary>
    private const int PreviousArfReconstructionId = -3;

    /// <summary>
    /// The first picture identity of a reference slot buffer; the caller's buffer identity is added.
    /// </summary>
    private const int SlotIdBase = 1 << 20;

    /// <summary>
    /// The first picture identity of a look-ahead source; the look-ahead offset is added.
    /// </summary>
    private const int LookaheadIdBase = 2 << 20;

    /// <summary>
    /// The first picture identity of a temporally filtered source; the group index is added.
    /// </summary>
    private const int FilteredIdBase = 3 << 20;

    /// <summary>
    /// The quantizer range of the leaf frames of a constant quality model. Reference: MIN_TPL_Q_INDEX and MAX_TPL_Q_INDEX.
    /// </summary>
    private const int MinimumQIndex = 120;

    /// <summary>
    /// The upper quantizer limit of the leaf frames of a constant quality model. Reference: MAX_TPL_Q_INDEX.
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
    /// The number of pool entries. Reference: lag_in_frames of av1_setup_tpl_buffers().
    /// </summary>
    private readonly int lagInFrames;

    /// <summary>
    /// The frame statistics; model frame i is entry i + 9, and reference slot s is entry 8 - s.
    /// Reference: tpl_stats_buffer.
    /// </summary>
    private readonly Av1TplFrameStatistics[] frames;

    /// <summary>
    /// The source picture of each frame entry. Reference: gf_picture.
    /// </summary>
    private readonly Av1EncoderFrame<TSample>[] sourcePictures;

    /// <summary>
    /// The identity of each source picture, or <see cref="NoPicture"/>.
    /// </summary>
    private readonly int[] sourceIds;

    /// <summary>
    /// The reconstructed picture of each frame entry. Reference: rec_picture.
    /// </summary>
    private readonly Av1EncoderFrame<TSample>[] reconstructionPictures;

    /// <summary>
    /// The identity of each reconstructed picture, or <see cref="NoPicture"/>.
    /// </summary>
    private readonly int[] reconstructionIds;

    /// <summary>
    /// The block statistics pool, one entry per look-ahead frame. Reference: tpl_stats_pool.
    /// </summary>
    private readonly IMemoryOwner<Av1TplBlockStatistics>[] statisticsPool;

    /// <summary>
    /// The reconstruction pool, one frame per look-ahead frame. Reference: tpl_rec_pool.
    /// </summary>
    private readonly Av1EncoderFrameBuffer<TSample>[] reconstructionPool;

    /// <summary>
    /// The saved source of the previous group's alternate reference. Reference: prev_gop_arf_src.
    /// </summary>
    private readonly Av1EncoderFrameBuffer<TSample> previousArfSource;

    /// <summary>
    /// The saved model reconstruction of the previous group's alternate reference. Reference: prev_gop_arf_tpl_recon.
    /// </summary>
    private readonly Av1EncoderFrameBuffer<TSample> previousArfReconstruction;

    /// <summary>
    /// The stale block mode-information fields the model reads and writes.
    /// </summary>
    private readonly Av1TplModeInfoGrid modeInfo;

    /// <summary>
    /// Whether the pools were released.
    /// </summary>
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplModel{TSample, TSearchOperator, TSampleOperator}"/> class and
    /// allocates the frame statistics, the statistics pool and the reconstruction pool. Reference: av1_setup_tpl_buffers().
    /// </summary>
    /// <param name="configuration">The configuration providing the allocator.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="colorFormat">The sampling layout.</param>
    /// <param name="chromaPositionX">The horizontal chroma position.</param>
    /// <param name="chromaPositionY">The vertical chroma position.</param>
    /// <param name="lagInFrames">The look-ahead depth; the model is used only above one. Reference: lag_in_frames.</param>
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
        this.statisticsPool = new IMemoryOwner<Av1TplBlockStatistics>[this.lagInFrames];
        this.reconstructionPool = new Av1EncoderFrameBuffer<TSample>[this.lagInFrames];
        for (int frame = 0; frame < this.lagInFrames; frame++)
        {
            this.statisticsPool[frame] = configuration.MemoryAllocator.Allocate<Av1TplBlockStatistics>(
                first.Width * first.Height,
                AllocationOptions.Clean);

            this.reconstructionPool[frame] = this.CreateFrameBuffer();
        }

        this.previousArfSource = this.CreateFrameBuffer();
        this.previousArfReconstruction = this.CreateFrameBuffer();
        this.PreviousArfDisplayOrder = -1;
        this.CreateScratch();
    }

    /// <summary>
    /// Gets the number of mode-information rows of a frame. Reference: mi_rows.
    /// </summary>
    public int ModeInfoRows { get; }

    /// <summary>
    /// Gets the number of mode-information columns of a frame. Reference: mi_cols.
    /// </summary>
    public int ModeInfoColumns { get; }

    /// <summary>
    /// Gets a value indicating whether the statistics of the current group are complete. Reference: ready.
    /// </summary>
    public bool Ready { get; private set; }

    /// <summary>
    /// Gets the factor that compensates the frame importance when leaf frames were skipped. Reference: r0_adjust_factor.
    /// </summary>
    public double R0AdjustFactor { get; private set; } = 1.0;

    /// <summary>
    /// Gets the leaf quantizer the model used, which the rate control records as the base layer quantizer.
    /// Reference: p_rc->base_layer_qp.
    /// </summary>
    public int BaseLayerQIndex { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the last run measured a frame, which sets the encoder's base quantizer
    /// index to <see cref="BaseLayerQIndex"/>. Reference: the cm->quant_params.base_qindex assignment of
    /// init_mc_flow_dispenser().
    /// </summary>
    public bool MeasuredFrame { get; private set; }

    /// <summary>
    /// Gets the display order of the saved alternate reference of the previous group, or -1. Reference:
    /// prev_gop_arf_disp_order.
    /// </summary>
    public long PreviousArfDisplayOrder { get; private set; }

    /// <summary>
    /// Gets the block mode-information fields that the model reads from the encoder's previous frame. The encoder must
    /// record, before each run, the fields its previous frame left at every 16x16-aligned position; the grid starts
    /// zeroed, which is the state before the first frame.
    /// </summary>
    public Av1TplModeInfoGrid ModeInfo => this.modeInfo;

    /// <summary>
    /// Gets the statistics of a model frame. Reference: tpl_frame[frame_index].
    /// </summary>
    /// <param name="frameIndex">The group index, -8 to 95.</param>
    /// <returns>The frame statistics.</returns>
    public Av1TplFrameStatistics GetFrame(int frameIndex) => this.frames[frameIndex + Av1TplModelConstants.ReferenceFrameSlotCount + 1];

    /// <summary>
    /// Gets the reconstructed picture the model produced for a frame of the current group.
    /// </summary>
    /// <param name="frameIndex">The group index.</param>
    /// <returns>The reconstructed picture.</returns>
    public Av1EncoderFrame<TSample> GetReconstruction(int frameIndex)
        => this.reconstructionPictures[frameIndex + Av1TplModelConstants.ReferenceFrameSlotCount + 1];

    /// <summary>
    /// Clears the readiness and validity flags and zeroes the statistics pool. Reference: av1_init_tpl_stats().
    /// </summary>
    public void InitializeStatistics()
    {
        this.Ready = false;
        for (int frame = 0; frame < this.frames.Length; frame++)
        {
            this.frames[frame].IsValid = false;
        }

        for (int frame = 0; frame < this.statisticsPool.Length; frame++)
        {
            this.statisticsPool[frame].Memory.Span.Clear();
        }
    }

    /// <summary>
    /// Marks the saved alternate reference of the previous group as unusable. Reference: the prev_gop_arf_disp_order
    /// reset of av1_get_second_pass_params() at a key frame.
    /// </summary>
    public void ForgetPreviousGroupAlternate() => this.PreviousArfDisplayOrder = -1;

    /// <summary>
    /// Returns whether a frame of the current group has complete statistics. Reference: av1_tpl_stats_ready().
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
    /// Saves the source and the model reconstruction of the last displayed frame of a group, so the next group can use
    /// the pair as a reference that only its own frames may choose. The encoder calls it after coding each frame.
    /// Reference: the prev_gop_arf copy of encode_frame_to_data_rate().
    /// </summary>
    /// <param name="group">The golden group.</param>
    /// <param name="groupIndex">The group index of the coded frame.</param>
    /// <param name="statisticsReady">Whether the statistics of the frame were ready before the frame processed them.</param>
    /// <param name="source">The source the frame was coded from, after temporal filtering. Reference: cpi->source.</param>
    /// <param name="displayOrderHint">The display order of the frame. Reference: display_order_hint.</param>
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
        for (int frame = 0; frame < this.statisticsPool.Length; frame++)
        {
            this.statisticsPool[frame].Dispose();
            this.reconstructionPool[frame].Dispose();
        }

        this.previousArfSource.Dispose();
        this.previousArfReconstruction.Dispose();
        this.DisposeScratch();
    }

    /// <summary>
    /// Copies the visible samples of a frame and extends its borders. Reference: aom_yv12_copy_frame().
    /// </summary>
    private static void CopyFrame(Av1EncoderFrame<TSample> source, Av1EncoderFrame<TSample> destination)
    {
        int planes = source.IsMonochrome ? 1 : 3;
        for (int plane = 0; plane < planes; plane++)
        {
            Av1PlaneRegion<TSample> from = source.CodedView.GetPlane((Av1Plane)plane);
            Av1PlaneRegion<TSample> to = destination.CodedView.GetPlane((Av1Plane)plane);
            int rows = plane == 0 ? source.Height : (source.Height + source.ChromaSubsamplingY) >> source.ChromaSubsamplingY;
            int columns = plane == 0 ? source.Width : (source.Width + source.ChromaSubsamplingX) >> source.ChromaSubsamplingX;
            for (int row = 0; row < rows; row++)
            {
                from.GetRowSpan(row)[..columns].CopyTo(to.GetRowSpan(row));
            }
        }

        destination.ExtendBorders();
    }

    /// <summary>
    /// Allocates one bordered frame of the model geometry. Reference: aom_alloc_frame_buffer() with border_in_pixels.
    /// </summary>
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
