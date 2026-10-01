// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// Runs the first pass of the look-ahead stage once per source frame in display order, producing the frame's
/// first-pass statistics. The stage owns the reconstructed LAST, LAST2 and GOLDEN references of its own
/// compressor and its own frame counter; nothing it holds is shared with the coding pass.
/// </summary>
/// <remarks>
/// The first frame and every forced key frame are intra-only. Every other frame measures intra prediction,
/// motion-compensated prediction from the three stage references, and zero motion against the previous source.
/// Only luma is read, so every chroma format behaves the same. Reference: av1_first_pass().
/// </remarks>
/// <typeparam name="TSample">The unsigned component storage type.</typeparam>
/// <typeparam name="TOperator">The operator closing the sample work over <typeparamref name="TSample"/>.</typeparam>
internal sealed partial class Av1FirstPass<TSample, TOperator> : IDisposable
    where TSample : unmanaged
    where TOperator : struct, Av1FirstPassOperator.IOperator<TSample>
{
    /// <summary>
    /// The real quantizer of the first pass. Reference: FIRST_PASS_Q.
    /// </summary>
    private const double FirstPassQ = 10.0;

    /// <summary>
    /// The intra error surcharge matching the overhead of a zero motion vector. Reference: INTRA_MODE_PENALTY.
    /// </summary>
    private const int IntraModePenalty = 1024;

    /// <summary>
    /// The inter error surcharge of a searched vector. Reference: NEW_MV_MODE_PENALTY.
    /// </summary>
    private const int NewMotionVectorModePenalty = 32;

    /// <summary>
    /// The eight-bit level below which a flat block counts as dark. Reference: DARK_THRESH.
    /// </summary>
    private const int DarkThreshold = 64;

    /// <summary>
    /// The intra error above which a close inter error counts as partly neutral. Reference: NCOUNT_INTRA_THRESH.
    /// </summary>
    private const int NeutralCountIntraThreshold = 8192;

    /// <summary>
    /// The ratio of intra to inter error below which a block counts as partly neutral. Reference: NCOUNT_INTRA_FACTOR.
    /// </summary>
    private const int NeutralCountIntraFactor = 3;

    /// <summary>
    /// The wavelet energy stored when it is not measured. Reference: INVALID_FP_STATS_TO_PREDICT_FLAT_GOP.
    /// </summary>
    private const int InvalidWaveletEnergy = -1;

    /// <summary>
    /// The intra error below which a block counts as skipped. Reference: UL_INTRA_THRESH.
    /// </summary>
    private const int LowIntraThreshold = 50;

    /// <summary>
    /// The start row of a unit or frame that has not yet seen image data. Reference: INVALID_ROW.
    /// </summary>
    private const int InvalidRow = -1;

    /// <summary>
    /// The largest full-sample vector component. Reference: MAX_FULL_PEL_VAL.
    /// </summary>
    private const int MaximumFullPixelValue = (1 << 10) - 1;

    /// <summary>
    /// The interpolation margin the vector limits keep inside the border. Reference: AOM_INTERP_EXTEND.
    /// </summary>
    private const int InterpolationExtend = 4;

    /// <summary>
    /// The border of the stage references. The vector limits never reach more than a unit plus two
    /// interpolation margins, 24 samples, beyond the coded frame.
    /// </summary>
    private const int ReconstructionBorder = 32;

    /// <summary>
    /// The number of residual samples the intra error sums, one 16x16 block. Reference: aom_get_mb_ss().
    /// </summary>
    private const int MacroblockArea = 256;

    /// <summary>
    /// The slot of the LAST reference in <see cref="slots"/>.
    /// </summary>
    private const int LastSlot = 0;

    /// <summary>
    /// The slot of the LAST2 reference in <see cref="slots"/>.
    /// </summary>
    private const int Last2Slot = 1;

    /// <summary>
    /// The slot of the GOLDEN reference in <see cref="slots"/>.
    /// </summary>
    private const int GoldenSlot = 2;

    private readonly int width;
    private readonly int height;
    private readonly int miColumns;
    private readonly int miRows;

    /// <summary>
    /// The first mode-info column of each tile column, followed by the frame width in mode-info units.
    /// </summary>
    private readonly int[] tileColumnStarts;

    /// <summary>
    /// The first mode-info row of each tile row, followed by the frame height in mode-info units.
    /// </summary>
    private readonly int[] tileRowStarts;
    private readonly int macroblockColumns;
    private readonly int macroblockRows;
    private readonly Av1BitDepth bitDepth;
    private readonly HeifEncodingSpeed speed;
    private readonly int border;
    private readonly bool doBorderPad;
    private readonly bool calculateWaveletEnergy;
    private readonly int sharpness;
    private readonly Av1Tuning tuning;
    private readonly int reduceMotionVectorStepParameter;
    private readonly int skipMotionSearchThreshold;
    private readonly bool disableReconstruction;
    private readonly bool skipZeroMotionSearch;
    private readonly bool pruneMeshSearch;
    private readonly int qIndex;
    private readonly int sadPerBit;
    private readonly int searchRange;
    private readonly Av1EncoderFrameBuffer<TSample>[] buffers;
    private readonly int[] slots = [-1, -1, -1];
    private readonly FrameStatistics[] unitStatistics;
    private readonly int[] rawMotionErrors;
    private readonly int[] waveletEnergies;
    private readonly short[] residual = new short[MacroblockArea];
    private readonly int[] transformed = new int[16];
    private readonly int[] quantized = new int[16];
    private readonly int[] dequantized = new int[16];
    private readonly int[] transformWorkspace = new int[Av1TransformWorkspace.MaximumLength];
    private readonly int[] searchSites = new int[Av1MotionSearchSites.StorageLength];
    private IMemoryOwner<int>? motionCostOwner;
    private int frameNumber;
    private int secondReferenceUpdateLag;
    private bool isScreenContentType;
    private bool useScreenContentTools;
    private bool allowIntraBlockCopy;
    private int pixelsToRightEdge;
    private int pixelsToBottomEdge;
    private FullMotionVectorLimits motionLimits;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FirstPass{TSample, TOperator}"/> class with empty references.
    /// Reference: av1_create_compressor().
    /// </summary>
    /// <param name="configuration">The configuration providing the reference and rate-table allocations.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="speed">The good-quality speed, from zero through six.</param>
    /// <param name="superblockSize">The sequence superblock size in samples, 64 or 128, which sets the frame border.</param>
    /// <param name="doBorderPad">Whether residuals outside the visible frame are replaced by the visible mean.</param>
    /// <param name="calculateWaveletEnergy">Whether the perceptual delta-quantizer mode requests the wavelet energy.</param>
    /// <param name="sharpness">The encoder sharpness; at three it keeps searched blocks inside the visible frame.</param>
    /// <param name="tuning">The tune metric, which scales the rate multiplier.</param>
    /// <param name="lookaheadStage">
    /// Whether the statistics feed a one-pass encode through the look-ahead stage, whose second-reference lag starts
    /// at one, rather than the first pass of a two-pass encode, whose lag starts at zero.
    /// </param>
    /// <param name="tiles">The tile layout of the coded frames, which the stage shares. Reference: cm->tiles.</param>
    public Av1FirstPass(
        Configuration configuration,
        int width,
        int height,
        Av1BitDepth bitDepth,
        HeifEncodingSpeed speed,
        int superblockSize,
        bool doBorderPad,
        bool calculateWaveletEnergy,
        int sharpness,
        Av1Tuning tuning,
        bool lookaheadStage,
        ObuTileGroupHeader tiles)
    {
        Guard.MustBeBetweenOrEqualTo((int)speed, 0, 6, nameof(speed));
        this.tileColumnStarts = new int[tiles.TileColumnCount + 1];
        for (int i = 0; i <= tiles.TileColumnCount; i++)
        {
            this.tileColumnStarts[i] = tiles.TileColumnStartModeInfo[i];
        }

        this.tileRowStarts = new int[tiles.TileRowCount + 1];
        for (int i = 0; i <= tiles.TileRowCount; i++)
        {
            this.tileRowStarts[i] = tiles.TileRowStartModeInfo[i];
        }

        this.width = width;
        this.height = height;
        this.bitDepth = bitDepth;
        this.speed = speed;
        this.doBorderPad = doBorderPad;
        this.calculateWaveletEnergy = calculateWaveletEnergy;
        this.sharpness = sharpness;
        this.tuning = tuning;

        // The stage keeps the mode-information grid of a statistics compressor: mode-information units cover
        // the frame rounded up to eight samples, and 16x16 macroblocks round those units up again.
        // Reference: stat_stage_set_mb_mi().
        this.miColumns = ((width + 7) & ~7) >> 2;
        this.miRows = ((height + 7) & ~7) >> 2;
        this.macroblockColumns = (this.miColumns + 2) >> 2;
        this.macroblockRows = (this.miRows + 2) >> 2;

        // The frame border of a coding compressor without resizing or all-intra coding. Reference:
        // av1_get_enc_border_size().
        this.border = superblockSize + 32;

        // The first-pass speed features. Reference: set_good_speed_features_framesize_independent().
        this.reduceMotionVectorStepParameter = speed >= HeifEncodingSpeed.Level5 ? 4 : 3;
        this.skipMotionSearchThreshold = speed >= HeifEncodingSpeed.Level2 ? 25 : 0;
        this.disableReconstruction = speed >= HeifEncodingSpeed.Level5;
        this.skipZeroMotionSearch = speed >= HeifEncodingSpeed.Level6;
        this.pruneMeshSearch = speed >= HeifEncodingSpeed.Level4;

        this.qIndex = FindFirstPassQIndex(bitDepth);
        this.sadPerBit = Av1RateDistortion.GetMotionSearchSadPerBit(this.qIndex, bitDepth);
        this.searchRange = GetSearchRange(width, height);

        // A screen-content key frame halves the unit size, so size the unit records for 8x8 units.
        int unitCount = GetUnitRows(1, this.macroblockRows) * GetUnitColumns(1, this.macroblockColumns);
        this.unitStatistics = new FrameStatistics[unitCount];
        this.rawMotionErrors = new int[unitCount];

        // A row of 16x16 units holds two 8x8 blocks per unit, which is at most two more than half the mode-info
        // columns. A row of 8x8 units holds fewer.
        this.waveletEnergies = new int[(this.miColumns >> 1) + 2];

        // Three reference slots can hold three distinct buffers, so a fourth is always free for the frame.
        this.buffers = new Av1EncoderFrameBuffer<TSample>[4];
        for (int i = 0; i < this.buffers.Length; i++)
        {
            this.buffers[i] = new Av1EncoderFrameBuffer<TSample>(
                configuration, width, height, bitDepth.GetBitCount(), Av1ColorFormat.Yuv400, 0, 0, ReconstructionBorder);
        }

        new Av1MotionSearchSites(this.searchSites).ConfigureFirstPass(this.buffers[0].Frame.CodedView.GetPlane(Av1Plane.Y).Stride);

        // The stage restores the default vector distributions and allows eighth-sample precision before every
        // frame, so one table serves the whole sequence. Reference: av1_init_mv_probs() and av1_fill_mv_costs().
        this.motionCostOwner = configuration.MemoryAllocator.Allocate<int>(Av1MotionVectorCosts.StorageLength);
        new Av1MotionVectorCosts(this.motionCostOwner.Memory.Span, Av1MotionVectorPrecision.EighthSample)
            .Fill(new Av1MotionVectorContext());

        // The look-ahead stage shares the lag of the coding compressor, which starts it at one; a two-pass first
        // pass leaves it zeroed. Reference: av1_init_single_pass_lap().
        this.secondReferenceUpdateLag = lookaheadStage ? 1 : 0;
    }

    /// <summary>
    /// Gets the number of frames processed so far, which is the display index of the next frame.
    /// </summary>
    public int FrameNumber => this.frameNumber;

    /// <summary>
    /// Measures one source frame and advances the stage references. Reference: av1_first_pass().
    /// </summary>
    /// <param name="source">The source frame, with its coded padding and border replicated from the visible edges.</param>
    /// <param name="previousSource">The source frame preceding <paramref name="source"/> in display order; not read for intra-only frames.</param>
    /// <param name="duration">The frame duration in time-stamp ticks.</param>
    /// <param name="forceKeyFrame">Whether the frame is a forced key frame.</param>
    /// <param name="groupUpdateType">
    /// The update type at the first index of the coding compressor's current group of frames, <see cref="Av1FrameUpdateType.Key"/>
    /// before the coding compressor has built one. The stage shares that group and never advances its own index,
    /// so this selects the rate multiplier of every vector cost.
    /// </param>
    /// <returns>The first-pass statistics of the frame.</returns>
    public Av1FirstPassStatistics Process(
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> previousSource,
        long duration,
        bool forceKeyFrame,
        Av1FrameUpdateType groupUpdateType)
    {
        bool intraOnly = this.frameNumber == 0 || forceKeyFrame;

        // Detect whether a key frame is screen content; later frames keep the decision. Reference: the
        // av1_set_screen_content_options() call of av1_first_pass().
        if (intraOnly)
        {
            this.isScreenContentType = TOperator.DetectScreenContent(source, out this.useScreenContentTools, out this.allowIntraBlockCopy);
        }

        int unitLog2 = GetFirstPassBlockSize(this.isScreenContentType) == Av1BlockSize.Block8x8 ? 1 : 2;
        int unitRows = GetUnitRows(unitLog2, this.macroblockRows);
        int unitColumns = GetUnitColumns(unitLog2, this.macroblockColumns);
        Span<FrameStatistics> unitStatistics = this.unitStatistics.AsSpan(0, unitRows * unitColumns);
        Span<int> rawMotionErrors = this.rawMotionErrors.AsSpan(0, unitRows * unitColumns);

        // Every unit record starts empty with no image data seen. Reference: setup_firstpass_data().
        unitStatistics.Clear();
        foreach (ref FrameStatistics unit in unitStatistics)
        {
            unit.ImageDataStartRow = InvalidRow;
        }

        rawMotionErrors.Clear();

        int current = 0;
        while (Array.IndexOf(this.slots, current) >= 0)
        {
            current++;
        }

        Av1PlaneRegion<TSample> sourcePlane = source.CodedView.GetPlane(Av1Plane.Y);
        Av1PlaneRegion<TSample> reconstructionPlane = this.buffers[current].Frame.CodedView.GetPlane(Av1Plane.Y);
        FrameContext frame = new()
        {
            IntraOnly = intraOnly,
            UnitLog2 = unitLog2,
            FirstPassBlockSize = unitLog2 == 1 ? Av1BlockSize.Block8x8 : Av1BlockSize.Block16x16,
            UnitRows = unitRows,
            UnitColumns = unitColumns,
            Source = sourcePlane.Samples,
            SourceStride = sourcePlane.Stride,
            SourceOrigin = (sourcePlane.Bounds.Y * sourcePlane.Stride) + sourcePlane.Bounds.X,
            Reconstruction = reconstructionPlane.Samples,
            ReconstructionStride = reconstructionPlane.Stride,
            ReconstructionOrigin = (reconstructionPlane.Bounds.Y * reconstructionPlane.Stride) + reconstructionPlane.Bounds.X,
            Costs = new Av1MotionVectorCosts(this.motionCostOwner!.Memory.Span, Av1MotionVectorPrecision.EighthSample),
            Sites = new Av1MotionSearchSites(this.searchSites),

            // The rate multiplier follows the shared group's first update type; the statistics stage never
            // applies the layer and boost terms of a consuming stage. Reference: av1_initialize_rd_consts().
            RateMultiplier = Av1RateDistortion.GetRateMultiplier(this.qIndex, this.bitDepth, groupUpdateType, this.tuning),

            // Graphics content lowers the exhaustive-search threshold; the stage never classifies content as
            // animation, so only the screen-content tools do. Reference: set_good_speed_features_framesize_independent().
            MeshThreshold = (this.useScreenContentTools ? 1 << 20 : 1 << 25) << (this.speed >= HeifEncodingSpeed.Level1 ? 1 : 0),
        };

        if (!intraOnly)
        {
            Av1PlaneRegion<TSample> previousPlane = previousSource.CodedView.GetPlane(Av1Plane.Y);
            frame.PreviousSource = previousPlane.Samples;
            frame.PreviousSourceStride = previousPlane.Stride;
            frame.PreviousSourceOrigin = (previousPlane.Bounds.Y * previousPlane.Stride) + previousPlane.Bounds.X;
            frame.Last = this.GetReference(LastSlot);
            frame.Golden = this.GetReference(GoldenSlot);
            frame.Last2 = this.GetReference(Last2Slot);
        }

        // Tiles run in raster order and rows run top to bottom in each tile. The vector of the first unit of a row
        // seeds the next row of the same tile, and every tile starts each frame from a zero vector. Reference:
        // first_pass_tiles(), first_pass_tile() and the firstpass_top_mv reset of av1_init_tile_data().
        int unitHeight = 1 << unitLog2;
        for (int tileRow = 0; tileRow < this.tileRowStarts.Length - 1; tileRow++)
        {
            int tileRowStart = this.tileRowStarts[tileRow];
            int tileRowEnd = this.tileRowStarts[tileRow + 1];
            for (int tileColumn = 0; tileColumn < this.tileColumnStarts.Length - 1; tileColumn++)
            {
                Av1MotionVector firstTopMotionVector = default;
                for (int miRow = tileRowStart; miRow < tileRowEnd; miRow += unitHeight)
                {
                    this.ProcessRow(
                        ref frame,
                        miRow >> unitLog2,
                        tileRowStart >> unitLog2,
                        this.tileColumnStarts[tileColumn],
                        this.tileColumnStarts[tileColumn + 1],
                        ref firstTopMotionVector);
                }
            }
        }

        FrameStatistics statistics = AccumulateFrameStatistics(unitStatistics, unitRows, unitColumns);
        int rawMotionErrorCount = intraOnly ? 0 : unitRows * unitColumns;
        double rawErrorStandardDeviation = GetRawMotionErrorStandardDeviation(rawMotionErrors[..rawMotionErrorCount]);

        // Clamp the image start to half the rows; that many rows are discarded top and bottom as dead data, so
        // half the rows means a blank frame. The dead rows are then excluded from the skipped blocks.
        if (statistics.ImageDataStartRow > unitRows / 2 || statistics.ImageDataStartRow == InvalidRow)
        {
            statistics.ImageDataStartRow = unitRows / 2;
        }

        if (statistics.ImageDataStartRow > 0)
        {
            statistics.IntraSkipCount = Math.Max(0, statistics.IntraSkipCount - (statistics.ImageDataStartRow * unitColumns * 2));
        }

        int macroblockCount = this.macroblockRows * this.macroblockColumns;
        int unitCount = GetUnitCount(unitLog2, macroblockCount);
        statistics.IntraFactor /= unitCount;
        statistics.BrightnessFactor /= unitCount;
        Av1FirstPassStatistics result = this.UpdateFirstPassStatistics(
            in statistics, rawErrorStandardDeviation, duration, unitLog2);

        this.UpdateReferences(in result, intraOnly, current);
        return result;
    }

    /// <summary>
    /// Releases the stage references and the rate table. Reference: av1_remove_compressor().
    /// </summary>
    public void Dispose()
    {
        foreach (Av1EncoderFrameBuffer<TSample> buffer in this.buffers)
        {
            buffer.Dispose();
        }

        this.motionCostOwner?.Dispose();
        this.motionCostOwner = null;
    }

    /// <summary>
    /// Gets the unit size of the first pass: screen content halves it to capture its finer detail.
    /// Reference: get_fp_block_size().
    /// </summary>
    /// <param name="isScreenContentType">Whether the last key frame was screen content.</param>
    /// <returns>The first-pass unit size.</returns>
    private static Av1BlockSize GetFirstPassBlockSize(bool isScreenContentType)
        => isScreenContentType ? Av1BlockSize.Block8x8 : Av1BlockSize.Block16x16;

    /// <summary>
    /// Converts a row count of 16x16 macroblocks to first-pass units. Reference: get_unit_rows().
    /// </summary>
    /// <param name="unitLog2">The base-two logarithm of the unit height in 4x4 units.</param>
    /// <param name="macroblockRows">The number of macroblock rows.</param>
    /// <returns>The number of unit rows.</returns>
    private static int GetUnitRows(int unitLog2, int macroblockRows) => macroblockRows << (2 - unitLog2);

    /// <summary>
    /// Converts a column count of 16x16 macroblocks to first-pass units. Reference: get_unit_cols().
    /// </summary>
    /// <param name="unitLog2">The base-two logarithm of the unit width in 4x4 units.</param>
    /// <param name="macroblockColumns">The number of macroblock columns.</param>
    /// <returns>The number of unit columns.</returns>
    private static int GetUnitColumns(int unitLog2, int macroblockColumns) => macroblockColumns << (2 - unitLog2);

    /// <summary>
    /// Converts a count of 16x16 macroblocks to square first-pass units. Reference: get_num_mbs().
    /// </summary>
    /// <param name="unitLog2">The base-two logarithm of the unit size in 4x4 units.</param>
    /// <param name="macroblockCount">The number of macroblocks.</param>
    /// <returns>The number of units.</returns>
    private static int GetUnitCount(int unitLog2, int macroblockCount) => macroblockCount << (2 * (2 - unitLog2));

    /// <summary>
    /// Finds the quantizer index whose real quantizer first reaches the first-pass quantizer.
    /// Reference: find_fp_qindex().
    /// </summary>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <returns>The first-pass quantizer index.</returns>
    private static int FindFirstPassQIndex(Av1BitDepth bitDepth)
    {
        // The real quantizer scales the AC step back to eight-bit precision. Reference: av1_find_qindex() over
        // av1_convert_qindex_to_q().
        double divisor = 4 << ((int)bitDepth * 2);
        int low = 0;
        int high = Av1Constants.MaxQ;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (Av1QuantizationLookup.GetAcQuant(middle, 0, bitDepth) / divisor < FirstPassQ)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    /// <summary>
    /// Gets the number of outer search stages the frame size makes unnecessary: a doubling of the shorter
    /// dimension still inside the largest vector removes one. Reference: get_search_range().
    /// </summary>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <returns>The search-range step adjustment.</returns>
    private static int GetSearchRange(int width, int height)
    {
        int range = 0;
        int dimension = Math.Max(Math.Min(width, height), 4);
        while ((dimension << range) < MaximumFullPixelValue)
        {
            range++;
        }

        return range;
    }

    /// <summary>
    /// Sums the unit records of a frame in raster order, taking the first unit that saw image data as the image
    /// start. Floating-point factors are summed in the same order as the reference. Reference: accumulate_frame_stats().
    /// </summary>
    /// <param name="units">The unit records.</param>
    /// <param name="unitRows">The number of unit rows.</param>
    /// <param name="unitColumns">The number of unit columns.</param>
    /// <returns>The frame totals.</returns>
    private static FrameStatistics AccumulateFrameStatistics(ReadOnlySpan<FrameStatistics> units, int unitRows, int unitColumns)
    {
        FrameStatistics statistics = default;
        statistics.ImageDataStartRow = InvalidRow;
        for (int index = 0; index < unitRows * unitColumns; index++)
        {
            ref readonly FrameStatistics unit = ref units[index];
            statistics.BrightnessFactor += unit.BrightnessFactor;
            statistics.CodedError += unit.CodedError;
            statistics.FrameAverageWaveletEnergy += unit.FrameAverageWaveletEnergy;
            if (statistics.ImageDataStartRow == InvalidRow && unit.ImageDataStartRow != InvalidRow)
            {
                statistics.ImageDataStartRow = unit.ImageDataStartRow;
            }

            statistics.InterCount += unit.InterCount;
            statistics.IntraError += unit.IntraError;
            statistics.IntraFactor += unit.IntraFactor;
            statistics.IntraSkipCount += unit.IntraSkipCount;
            statistics.MotionVectorCount += unit.MotionVectorCount;
            statistics.NeutralCount += unit.NeutralCount;
            statistics.NewMotionVectorCount += unit.NewMotionVectorCount;
            statistics.SecondReferenceCount += unit.SecondReferenceCount;
            statistics.SecondReferenceCodedError += unit.SecondReferenceCodedError;
            statistics.LongTermCodedError += unit.LongTermCodedError;
            statistics.SumInVectors += unit.SumInVectors;
            statistics.SumMotionVectorColumn += unit.SumMotionVectorColumn;
            statistics.SumMotionVectorColumnAbsolute += unit.SumMotionVectorColumnAbsolute;
            statistics.SumMotionVectorColumnSquares += unit.SumMotionVectorColumnSquares;
            statistics.SumMotionVectorRow += unit.SumMotionVectorRow;
            statistics.SumMotionVectorRowAbsolute += unit.SumMotionVectorRowAbsolute;
            statistics.SumMotionVectorRowSquares += unit.SumMotionVectorRowSquares;
        }

        return statistics;
    }

    /// <summary>
    /// Gets the standard deviation of the zero-motion errors against the previous source over every unit.
    /// Reference: raw_motion_error_stdev().
    /// </summary>
    /// <param name="errors">The unit errors, empty for an intra-only frame.</param>
    /// <returns>The standard deviation, or zero without errors.</returns>
    private static double GetRawMotionErrorStandardDeviation(ReadOnlySpan<int> errors)
    {
        if (errors.IsEmpty)
        {
            return 0;
        }

        long sum = 0;
        foreach (int error in errors)
        {
            sum += error;
        }

        double average = (double)sum / errors.Length;
        double deviation = 0;
        foreach (int error in errors)
        {
            deviation += (error - average) * (error - average);
        }

        return Math.Sqrt(deviation / errors.Length);
    }

    /// <summary>
    /// Normalizes errors and counters to one 16x16 macroblock and vectors to the frame dimensions.
    /// Reference: normalize_firstpass_stats().
    /// </summary>
    /// <param name="statistics">The frame record to normalize.</param>
    /// <param name="macroblockCount">The number of 16x16 macroblocks.</param>
    /// <param name="frameWidth">The visible width.</param>
    /// <param name="frameHeight">The visible height.</param>
    private static void NormalizeFirstPassStatistics(
        ref Av1FirstPassStatistics statistics,
        double macroblockCount,
        double frameWidth,
        double frameHeight)
    {
        statistics.CodedError /= macroblockCount;
        statistics.SecondReferenceCodedError /= macroblockCount;
        statistics.LongTermCodedError /= macroblockCount;
        statistics.IntraError /= macroblockCount;
        statistics.FrameAverageWaveletEnergy /= macroblockCount;
        statistics.LogCodedError = Av1FirstPassMath.Log1P(statistics.CodedError);
        statistics.LogIntraError = Av1FirstPassMath.Log1P(statistics.IntraError);
        statistics.MotionVectorRow /= frameHeight;
        statistics.MotionVectorRowAbsolute /= frameHeight;
        statistics.MotionVectorColumn /= frameWidth;
        statistics.MotionVectorColumnAbsolute /= frameWidth;
        statistics.MotionVectorRowVariance /= frameHeight * frameHeight;
        statistics.MotionVectorColumnVariance /= frameWidth * frameWidth;
        statistics.NewMotionVectorCount /= macroblockCount;
    }

    /// <summary>
    /// Builds the frame record from its totals. Each error receives a floor that grows with the square root of
    /// the unit count, so static frames still receive bits; counters become shares of the units, and vector
    /// sums become means and variances over the units with motion. Reference: update_firstpass_stats().
    /// </summary>
    /// <param name="statistics">The frame totals.</param>
    /// <param name="rawErrorStandardDeviation">The standard deviation of the zero-motion source errors.</param>
    /// <param name="duration">The frame duration in time-stamp ticks.</param>
    /// <param name="unitLog2">The base-two logarithm of the unit size in 4x4 units.</param>
    /// <returns>The frame record.</returns>
    private Av1FirstPassStatistics UpdateFirstPassStatistics(
        in FrameStatistics statistics,
        double rawErrorStandardDeviation,
        long duration,
        int unitLog2)
    {
        int macroblockCount = this.macroblockRows * this.macroblockColumns;
        int unitCount = GetUnitCount(unitLog2, macroblockCount);
        double minimumError = 200 * Math.Sqrt(unitCount);

        Av1FirstPassStatistics result = default;
        result.Weight = statistics.IntraFactor * statistics.BrightnessFactor;
        result.Frame = this.frameNumber;
        result.CodedError = (double)(statistics.CodedError >> 8) + minimumError;
        result.SecondReferenceCodedError = (double)(statistics.SecondReferenceCodedError >> 8) + minimumError;
        result.LongTermCodedError = (double)(statistics.LongTermCodedError >> 8) + minimumError;
        result.IntraError = (double)(statistics.IntraError >> 8) + minimumError;
        result.FrameAverageWaveletEnergy = statistics.FrameAverageWaveletEnergy;
        result.Count = 1.0;
        result.PercentInter = (double)statistics.InterCount / unitCount;
        result.PercentSecondReference = (double)statistics.SecondReferenceCount / unitCount;
        result.PercentNeutral = statistics.NeutralCount / unitCount;
        result.IntraSkipPercent = (double)statistics.IntraSkipCount / unitCount;
        result.InactiveZoneRows = statistics.ImageDataStartRow;
        result.InactiveZoneColumns = 0.0;
        result.RawErrorStandardDeviation = rawErrorStandardDeviation;
        result.IsFlash = 0;
        result.NoiseVariance = 0.0;
        result.CorrelationCoefficient = 1.0;
        result.LogCodedError = 0.0;
        result.LogIntraError = 0.0;

        int count = statistics.MotionVectorCount;
        if (count > 0)
        {
            result.MotionVectorRow = (double)statistics.SumMotionVectorRow / count;
            result.MotionVectorRowAbsolute = (double)statistics.SumMotionVectorRowAbsolute / count;
            result.MotionVectorColumn = (double)statistics.SumMotionVectorColumn / count;
            result.MotionVectorColumnAbsolute = (double)statistics.SumMotionVectorColumnAbsolute / count;
            result.MotionVectorRowVariance = ((double)statistics.SumMotionVectorRowSquares -
                ((double)statistics.SumMotionVectorRow * statistics.SumMotionVectorRow / count)) / count;

            result.MotionVectorColumnVariance = ((double)statistics.SumMotionVectorColumnSquares -
                ((double)statistics.SumMotionVectorColumn * statistics.SumMotionVectorColumn / count)) / count;

            result.MotionVectorInOutCount = (double)statistics.SumInVectors / (count * 2);
            result.NewMotionVectorCount = statistics.NewMotionVectorCount;
            result.PercentMotion = (double)count / unitCount;
        }

        result.Duration = duration;
        NormalizeFirstPassStatistics(ref result, macroblockCount, this.width, this.height);
        return result;
    }

    /// <summary>
    /// Rotates the stage references after a frame: a poorly predicted frame moves GOLDEN into LAST2, a well
    /// predicted frame (or a lag of more than three frames) moves LAST into GOLDEN, and the frame itself becomes
    /// LAST. The first frame also fills GOLDEN and LAST2. Reference: the reference updates at the end of
    /// av1_first_pass().
    /// </summary>
    /// <param name="statistics">The record of the frame just measured.</param>
    /// <param name="intraOnly">Whether the frame was intra-only, which leaves GOLDEN and LAST2 unread.</param>
    /// <param name="current">The buffer holding the frame's reconstruction.</param>
    private void UpdateReferences(in Av1FirstPassStatistics statistics, bool intraOnly, int current)
    {
        if (statistics.PercentInter < 0.2 && !intraOnly)
        {
            this.slots[Last2Slot] = this.slots[GoldenSlot];
        }

        // DOUBLE_DIVIDE_CHECK keeps the ratio finite for a zero error.
        if (this.secondReferenceUpdateLag > 3 ||
            (this.frameNumber > 0 && statistics.PercentInter > 0.20 &&
            (statistics.IntraError / (statistics.CodedError + 0.000001)) > 2.0))
        {
            if (!intraOnly)
            {
                this.slots[GoldenSlot] = this.slots[LastSlot];
            }

            this.secondReferenceUpdateLag = 1;
        }
        else
        {
            this.secondReferenceUpdateLag++;
        }

        // Motion search reads beyond the coded frame, so the new reference replicates its visible edges.
        // Reference: aom_extend_frame_borders().
        this.buffers[current].Frame.ExtendBorders();
        this.slots[LastSlot] = current;
        if (this.frameNumber == 0)
        {
            this.slots[GoldenSlot] = current;
            this.slots[Last2Slot] = current;
        }

        this.frameNumber++;
    }

    /// <summary>
    /// Gets the complete bordered luma plane of a reference slot, which shares the stride and origin of every
    /// stage buffer. Reference: get_ref_frame_yv12_buf().
    /// </summary>
    /// <param name="slot">The reference slot.</param>
    /// <returns>The reference samples.</returns>
    private ReadOnlySpan<TSample> GetReference(int slot)
        => this.buffers[this.slots[slot]].Frame.CodedView.GetPlane(Av1Plane.Y).Samples;
}
