// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// The frame-level decisions of good-quality one-pass coding with a look-ahead: key frame placement, golden frame
/// group length and structure, the alternate reference decisions, and the constant-quality quantizer of each frame.
/// The look-ahead stage runs a first pass on each frame as it enters and pushes its statistics here; the encode
/// stage then asks for one frame at a time.
/// Reference: the one-pass look-ahead (LAP) path of av1_get_second_pass_params(), with the parts of
/// av1_encode_strategy(), av1_rc_pick_q_and_bounds(), av1_rc_postencode_update() and
/// av1_twopass_postencode_update() that the decisions read and update, for AOM_Q, one thread and no forced
/// key frames.
/// </summary>
/// <remarks>
/// <para>A caller drives one frame as follows.</para>
/// <list type="number">
/// <item>Push the statistics of every frame that enters the look-ahead with <see cref="PushStatistics"/>, in display
/// order. Before asking for a frame, push until <see cref="PendingFrameCount"/> reaches
/// <see cref="LookaheadDepth"/> or the input has ended; libaom's encode stage waits for exactly that.</item>
/// <item>Call <see cref="TryBeginFrame"/>. It returns false when the look-ahead must be filled first or when every
/// frame is coded.</item>
/// <item>When the frame starts a group, run the temporal filter and the temporal dependency model on
/// <see cref="Group"/>; both may read <see cref="PickQIndex(int, bool)"/> for any frame of the group. For an
/// <see cref="Av1FrameUpdateType.Alternate"/> frame set <see cref="ShowExistingAlternateReference"/> from the
/// filtered frame test.</item>
/// <item>Unless the frame shows an existing frame, call <see cref="ChooseBaseQIndex"/> with the temporal
/// dependency results of the frame and code it with the returned quantizer index.</item>
/// <item>Call <see cref="CompleteFrame"/> with the coded quantizer index.</item>
/// </list>
/// </remarks>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// The default key frame boost. Reference: DEFAULT_KF_BOOST.
    /// </summary>
    private const int DefaultKeyFrameBoost = 2300;

    /// <summary>
    /// The default golden boost. Reference: DEFAULT_GF_BOOST.
    /// </summary>
    private const int DefaultGoldenBoost = 2000;

    /// <summary>
    /// The largest golden interval. Reference: MAX_GF_INTERVAL.
    /// </summary>
    private const int MaximumGoldenInterval = 32;

    /// <summary>
    /// The largest golden interval of a short look-ahead. Reference: MAX_GF_LENGTH_LAP.
    /// </summary>
    private const int MaximumLookaheadGoldenLength = 16;

    /// <summary>
    /// The smallest golden interval that may use an alternate reference. Reference: MIN_GF_INTERVAL.
    /// </summary>
    private const int MinimumGoldenInterval = 4;

    /// <summary>
    /// The number of golden intervals the length decision may produce. Reference: MAX_NUM_GF_INTERVALS.
    /// </summary>
    private const int MaximumGoldenIntervalCount = 15;

    /// <summary>
    /// The number of frames after a key frame candidate that its test examines. Reference:
    /// SCENE_CUT_KEY_TEST_INTERVAL.
    /// </summary>
    private const int SceneCutKeyTestInterval = 16;

    /// <summary>
    /// The largest number of look-ahead statistics. Reference: MAX_LAP_BUFFERS.
    /// </summary>
    private const int MaximumLookaheadBuffers = 48;

    /// <summary>
    /// The smallest look-ahead that allows an alternate reference. Reference: ALT_MIN_LAG.
    /// </summary>
    private const int AlternateReferenceMinimumLag = 3;

    /// <summary>
    /// The largest key frame distance of the default configuration. Reference: the kf_max_dist default.
    /// </summary>
    private const int KeyFrameMaximumDistance = 9999;

    /// <summary>
    /// The smallest key frame distance of the default configuration. Reference: the kf_min_dist default.
    /// </summary>
    private const int KeyFrameMinimumDistance = 0;

    /// <summary>
    /// The largest pyramid height of golden frame groups. Reference: the gf_max_pyr_height default.
    /// </summary>
    private const int MaximumPyramidHeight = 5;

    /// <summary>
    /// The smallest pyramid height of golden frame groups; zero lets a group drop its alternate reference.
    /// Reference: the gf_min_pyr_height default.
    /// </summary>
    private const int MinimumPyramidHeight = 0;

    /// <summary>
    /// The largest number of frames the temporal filter blends. Reference: the arnr_max_frames default.
    /// </summary>
    private const int ArnrMaximumFrames = 7;

    /// <summary>
    /// The lowest pyramid level that reference mapping uses. Reference: MIN_PYR_LEVEL.
    /// </summary>
    private const int MinimumPyramidLevel = 1;

    private readonly int width;
    private readonly int height;
    private readonly Av1BitDepth bitDepth;
    private readonly int macroblockRows;
    private readonly int macroblockCount;
    private readonly int cqLevel;
    private readonly int bestQuality;
    private readonly int worstQuality;
    private readonly bool lossless;
    private readonly int lagInFrames;
    private readonly int lookaheadDepth;
    private readonly int sceneCutDetection;
    private readonly int minimumGoldenInterval;
    private readonly int maximumGoldenInterval;
    private readonly int staticSceneMaximumGoldenInterval;
    private readonly int gopLengthDecisionMethod;
    private IGopLengthEvaluator? gopLengthEvaluator;

    /// <summary>
    /// The linear buffer of look-ahead statistics from the first frame not yet shown to the newest analysed frame.
    /// The flash, noise and correlation estimates are written here. Reference: the stats_buf_ctx buffer.
    /// </summary>
    private readonly Av1FirstPassStatistics[] statistics;

    /// <summary>
    /// The ring of unmodified statistics around the current frame. Reference: firstpass_info.
    /// </summary>
    private readonly Av1SecondPassStatisticsInfo statisticsInfo = new();

    /// <summary>
    /// The golden frame group being coded. Reference: gf_group.
    /// </summary>
    private readonly Av1GopStructure group = new();

    /// <summary>
    /// The golden intervals of the length decision. Reference: gf_intervals.
    /// </summary>
    private readonly int[] goldenIntervals = new int[MaximumGoldenIntervalCount];

    /// <summary>
    /// The number of statistics in <see cref="statistics"/>. Reference: stats_in_end.
    /// </summary>
    private int statisticsCount;

    /// <summary>
    /// The read position in <see cref="statistics"/>. Reference: twopass_frame.stats_in.
    /// </summary>
    private int statisticsPosition;

    /// <summary>
    /// The sum of the wavelet energies of every analysed frame, whose sign marks the energy invalid.
    /// Reference: frame_avg_wavelet_energy of total_stats.
    /// </summary>
    private double totalWaveletEnergy;

    private int pushedCount;
    private int shownCount;
    private int frameNumber;
    private int groupFrameIndex;
    private int framesToKey;
    private int framesSinceKey = 8;
    private int framesToForwardKeyFrame;
    private int activeWorstQuality;
    private int currentGoldenIndex;
    private int baselineGoldenInterval;
    private bool constrainedGoldenGroup;
    private bool useArfInThisKeyFrameGroup;
    private bool thisKeyFrameForced;
    private bool nextKeyFrameForced;
    private bool internalAltrefAllowed;
    private bool arfGoldenBoostLast;
    private int keyFrameBoost;
    private int goldenBoost;
    private int averageGoldenBoost;
    private double arfBoostFactor;
    private int statisticsUsedForKeyFrameBoost;
    private int statisticsUsedForGoldenBoost;
    private int statisticsRequiredForGoldenBoost;
    private int averageKeyFrameQIndex;
    private int averageInterFrameQIndex;
    private int lastBoostedQIndex;
    private int lastKeyFrameQIndex;
    private int arfQ;
    private int lastCodedQIndex;
    private bool skipTplSetupStatistics;
    private bool screenContentType;
    private bool isGraphicsAnimation;
    private double macroblockAverageEnergy;
    private double frameAverageHaarEnergy;
    private Av1SecondPassFrame current;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SecondPass"/> class for libavif's color sequence
    /// configuration: good-quality usage, AOM_Q, automatic key frames up to 9999 frames apart, automatic alternate
    /// references with a pyramid of up to five layers, and the temporal dependency model on. Reference:
    /// av1_primary_rc_init(), av1_rc_init(), av1_init_single_pass_lap(), set_gf_interval_range(), the look-ahead
    /// sizing of encoder_init() and av1_lookahead_init(), and the scene cut mode of av1_create_primary_compressor().
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="cqLevel">The constant-quality quantizer index. Reference: cq_level.</param>
    /// <param name="bestAllowedQIndex">The lowest allowed quantizer index. Reference: best_allowed_q.</param>
    /// <param name="worstAllowedQIndex">The highest allowed quantizer index. Reference: worst_allowed_q.</param>
    /// <param name="speed">The cpu-used tier, 0 to 6.</param>
    /// <param name="lagInFrames">The requested look-ahead, at least 1. Reference: g_lag_in_frames.</param>
    /// <param name="framerate">The frame rate that sets the golden interval range. Reference: framerate.</param>
    /// <param name="gopLengthEvaluator">
    /// The temporal dependency test that may shorten a golden interval above 16 frames at speeds 0 to 5, or null
    /// to keep every interval.
    /// </param>
    public Av1SecondPass(
        int width,
        int height,
        Av1BitDepth bitDepth,
        int cqLevel,
        int bestAllowedQIndex,
        int worstAllowedQIndex,
        int speed,
        int lagInFrames,
        double framerate,
        IGopLengthEvaluator? gopLengthEvaluator)
    {
        this.width = width;
        this.height = height;
        this.bitDepth = bitDepth;
        this.cqLevel = cqLevel;
        this.bestQuality = bestAllowedQIndex;
        this.worstQuality = worstAllowedQIndex;
        this.lossless = bestAllowedQIndex == 0 && worstAllowedQIndex == 0;
        this.gopLengthEvaluator = gopLengthEvaluator;
        this.activeWorstQuality = cqLevel;

        // FRAME_INFO and mi_params: 16x16 macroblocks over the 8-aligned frame.
        int modeInfoRows = Av1Math.AlignPowerOf2(height, 3) >> 2;
        int modeInfoColumns = Av1Math.AlignPowerOf2(width, 3) >> 2;
        this.macroblockRows = (modeInfoRows + 2) >> 2;
        this.macroblockCount = this.macroblockRows * ((modeInfoColumns + 2) >> 2);

        // set_encoder_config() raises a good-quality look-ahead of 32 to 38 frames to 39 for better temporal
        // filtering. The look-ahead stage keeps the requested length: encoder_init() sizes its buffers from the
        // configuration value, and the look-ahead stage itself has no lag, so the encode stage waits for that many
        // frames. Reference: set_encoder_config(), encoder_init() and av1_lookahead_init().
        this.lagInFrames = Math.Clamp(lagInFrames, 0, MaximumLookaheadBuffers);
        if (this.lagInFrames is >= 32 and < 39)
        {
            this.lagInFrames = 39;
        }

        int lookaheadBuffers = Math.Min(lagInFrames, Math.Min(MaximumLookaheadBuffers, KeyFrameMaximumDistance + SceneCutKeyTestInterval));
        this.lookaheadDepth = lookaheadBuffers;
        this.statistics = new Av1FirstPassStatistics[Math.Max(lookaheadBuffers + 1, MaximumLookaheadGoldenLength + 1)];

        // Scene cut detection needs enough look-ahead to test the frames after a candidate.
        // Reference: ENABLE_SCENECUT_MODE_2, ENABLE_SCENECUT_MODE_1 and DISABLE_SCENECUT.
        this.sceneCutDetection = lookaheadBuffers < MaximumLookaheadGoldenLength + 3
            ? 0
            : lookaheadBuffers < MaximumLookaheadGoldenLength + SceneCutKeyTestInterval + 1 ? 1 : 2;

        // av1_rc_get_default_min_gf_interval() and get_default_max_gf_interval(), limited by set_gf_interval_range()
        // to one past the maximum for a look-ahead.
        const double factorSafe = 3840 * 2160 * 20.0;
        double factor = (double)width * height * framerate;
        int defaultInterval = Math.Clamp((int)(framerate * 0.125), MinimumGoldenInterval, MaximumGoldenInterval);
        int minimum = factor <= factorSafe ? defaultInterval : Math.Max(defaultInterval, (int)((MinimumGoldenInterval * factor / factorSafe) + 0.5));
        int maximum = Math.Min(MaximumGoldenInterval, (int)(framerate * 0.75));
        maximum += maximum & 0x01;
        maximum = Math.Max(MaximumGoldenInterval, maximum);
        maximum = Math.Max(maximum, minimum);
        this.staticSceneMaximumGoldenInterval = maximum + 1;
        this.maximumGoldenInterval = Math.Min(maximum, this.staticSceneMaximumGoldenInterval);
        this.minimumGoldenInterval = Math.Min(minimum, this.maximumGoldenInterval);
        this.baselineGoldenInterval = (this.minimumGoldenInterval + this.maximumGoldenInterval) / 2;

        // Speeds 0 to 4 run the complete group test, speed 5 the cheaper boost-gated test, and speed 6 none.
        // Reference: gop_length_decision_method of set_good_speed_features_framesize_independent().
        this.gopLengthDecisionMethod = speed >= 6 ? 3 : speed >= 5 ? 2 : 1;

        this.averageKeyFrameQIndex = (worstAllowedQIndex + bestAllowedQIndex) / 2;
        this.averageInterFrameQIndex = (worstAllowedQIndex + bestAllowedQIndex) / 2;
    }

    /// <summary>
    /// Runs the temporal dependency model on the trial group of a golden interval above 16 frames. Reference: the
    /// av1_tf_info_filtering() and av1_tpl_setup_stats() calls of av1_get_second_pass_params() and
    /// is_shorter_gf_interval_better().
    /// </summary>
    internal interface IGopLengthEvaluator
    {
        /// <summary>
        /// Filters the alternate reference of <see cref="Group"/> when the trial group asks for it and then runs the
        /// temporal dependency model for a group length evaluation, reading the preloaded
        /// <see cref="Av1GopStructure.QValues"/>. Reference: av1_tf_info_filtering() once per trial group, then
        /// av1_tpl_setup_stats().
        /// </summary>
        /// <param name="secondPass">The decisions that own the trial group.</param>
        /// <param name="gopEvaluation">The evaluation: 1 for the complete group, 2 for three layers, 3 for two.</param>
        /// <returns>The evaluation result: 0 to shorten, 1 to keep, 2 when undecided.</returns>
        int SetupTplStatistics(Av1SecondPass secondPass, int gopEvaluation);

        /// <summary>
        /// Discards the filtered frames of the previous group before a new group is defined. Reference:
        /// av1_tf_info_reset() in av1_get_second_pass_params().
        /// </summary>
        void BeginGroup();
    }

    /// <summary>
    /// Gets the number of frames the look-ahead holds before the encode stage codes a frame.
    /// Reference: pop_sz of the encode stage.
    /// </summary>
    public int LookaheadDepth => this.lookaheadDepth;

    /// <summary>
    /// Gets the number of pushed frames that are not yet shown. Reference: av1_lookahead_depth().
    /// </summary>
    public int PendingFrameCount => this.pushedCount - this.shownCount;

    /// <summary>
    /// Gets the golden frame group being coded. Reference: gf_group.
    /// </summary>
    public Av1GopStructure Group => this.group;

    /// <summary>
    /// Gets the decisions of the frame being coded.
    /// </summary>
    public Av1SecondPassFrame Current => this.current;

    /// <summary>
    /// Sets a value indicating whether the overlay of the current alternate reference repeats the filtered
    /// alternate reference. The temporal filter decides it when the alternate reference is coded.
    /// Reference: show_existing_alt_ref.
    /// </summary>
    public bool ShowExistingAlternateReference { private get; set; }

    /// <summary>
    /// Appends the first-pass statistics of the newest frame of the look-ahead. Reference: the statistics push of
    /// update_firstpass_stats() in the look-ahead stage.
    /// </summary>
    /// <param name="frameStatistics">The statistics.</param>
    public void PushStatistics(in Av1FirstPassStatistics frameStatistics)
    {
        if (this.statisticsCount == this.statistics.Length)
        {
            throw new InvalidOperationException("The look-ahead holds more frames than its depth.");
        }

        this.statistics[this.statisticsCount++] = frameStatistics;
        this.statisticsInfo.Push(frameStatistics);
        this.totalWaveletEnergy += frameStatistics.FrameAverageWaveletEnergy;
        this.pushedCount++;
    }

    /// <summary>
    /// Makes the frame-level decisions of the next coded frame.
    /// Reference: av1_encode_strategy() up to the reference frame selection, with av1_get_second_pass_params(),
    /// choose_frame_source() and the order hint and pyramid level of av1_encode().
    /// </summary>
    /// <param name="flush">Whether the input has ended, so that the look-ahead drains.</param>
    /// <param name="frame">Receives the decisions.</param>
    /// <returns>False when the look-ahead must be filled first or every frame is coded.</returns>
    public bool TryBeginFrame(bool flush, out Av1SecondPassFrame frame)
    {
        frame = default;
        int pending = this.PendingFrameCount;
        if ((!flush && pending < this.lookaheadDepth) || pending == 0)
        {
            return false;
        }

        // Each frame starts without reusing the model's statistics; only the length test of a new group may reuse
        // them. Reference: the skip_tpl_setup_stats reset of av1_encode_strategy().
        this.skipTplSetupStatistics = false;
        bool startsGroup = this.groupFrameIndex == this.group.Size;
        this.GetSecondPassParameters();

        int index = this.groupFrameIndex;
        Av1FrameUpdateType updateType = this.group.UpdateTypes[index];
        bool showExisting;
        if (updateType == Av1FrameUpdateType.Overlay && this.group.ReferenceResets[index])
        {
            showExisting = true;
        }
        else
        {
            showExisting = (this.ShowExistingAlternateReference && updateType == Av1FrameUpdateType.Overlay) ||
                updateType == Av1FrameUpdateType.IntermediateOverlay;
        }

        // allow_show_existing(): the first frame never repeats one.
        showExisting &= this.frameNumber != 0;
        if (updateType == Av1FrameUpdateType.Overlay)
        {
            this.ShowExistingAlternateReference = false;
        }

        // choose_frame_source(): an alternate reference codes a future source and is hidden.
        int sourceOffset = showExisting ? 0 : this.group.ArfSourceOffsets[index];
        bool showFrame = showExisting || sourceOffset == 0;
        bool keyFrame = this.group.KeyFrames[index] && !showExisting;
        bool resetsReferences = this.group.ReferenceResets[index];
        int frameNumber = this.frameNumber;
        if (keyFrame && resetsReferences)
        {
            // av1_encode() restarts the display count at a key frame that resets the references.
            this.frameNumber = 0;
        }

        int displayOrder = this.frameNumber + this.group.ArfSourceOffsets[index];
        this.current = new Av1SecondPassFrame
        {
            GroupIndex = index,
            StartsGroup = startsGroup,
            UpdateType = updateType,
            IsKeyFrame = keyFrame,
            ResetsReferences = resetsReferences,
            ShowFrame = showFrame,
            ShowableFrame = !showFrame,
            ShowExistingFrame = showExisting,
            SourceOffset = sourceOffset,
            DisplayOrder = displayOrder,
            FrameNumber = frameNumber,
            LayerDepth = this.group.LayerDepths[index],
            MaxLayerDepth = this.group.MaxLayerDepth,
            PyramidLevel = GetTruePyramidLevel(this.group.LayerDepths[index], displayOrder, this.group.MaxLayerDepth),
            ArfBoost = this.group.ArfBoosts[index],
            ReusesTplStatistics = this.skipTplSetupStatistics,
            IsGraphicsAnimation = this.isGraphicsAnimation,
            MacroblockAverageEnergy = this.macroblockAverageEnergy,
            FrameAverageHaarEnergy = this.frameAverageHaarEnergy
        };

        frame = this.current;
        return true;
    }

    /// <summary>
    /// Returns the quantizer index of the current frame: the constant-quality choice, after the temporal dependency
    /// model adjusts the golden boost, replaced by the temporal dependency choice when the model has statistics
    /// for the frame. Reference: av1_set_size_dependent_vars() with process_tpl_stats_frame(),
    /// av1_rc_pick_q_and_bounds() and av1_tpl_get_q_index() in AOM_Q mode.
    /// </summary>
    /// <param name="screenContent">Whether the frame is screen content. Reference: is_screen_content_type.</param>
    /// <param name="tplValid">
    /// Whether the temporal dependency model has valid statistics for the frame. Reference: av1_tpl_stats_ready()
    /// with a nonzero mc_dep_cost_base.
    /// </param>
    /// <param name="tplR0">The frame's ratio of propagated to intra cost. Reference: r0.</param>
    /// <param name="tplQStepRatio">The frame's quantizer step ratio. Reference: av1_tpl_get_qstep_ratio().</param>
    /// <returns>The base quantizer index.</returns>
    public int ChooseBaseQIndex(bool screenContent, bool tplValid, double tplR0, double tplQStepRatio)
    {
        int index = this.groupFrameIndex;
        Av1FrameUpdateType updateType = this.group.UpdateTypes[index];
        if (tplValid && updateType is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Key)
        {
            // process_tpl_stats_frame(): the model's boost, projected to the frames the boost was meant to cover,
            // is blended with the boost from the statistics.
            double minimumBoostFactor = Math.Sqrt(this.baselineGoldenInterval);
            int tplBoost = Av1ConstantQuality.GetGoldenBoostFromR0(
                minimumBoostFactor,
                Av1ConstantQuality.MaximumBoostFactor,
                tplR0,
                this.statisticsRequiredForGoldenBoost);

            this.goldenBoost = Av1ConstantQuality.CombinePriorWithTplBoost(
                minimumBoostFactor,
                Av1ConstantQuality.MaximumBoostCombineFactor,
                this.goldenBoost,
                tplBoost,
                this.statisticsUsedForGoldenBoost);
        }

        this.screenContentType = screenContent;
        int q = this.PickQIndex(index, screenContent);
        if (tplValid && !this.lossless)
        {
            int tplQ = Av1ConstantQuality.GetQIndexFromQStepRatio(this.activeWorstQuality, tplQStepRatio, this.bitDepth);
            q = Math.Clamp(tplQ, this.bestQuality, this.worstQuality);
            if (updateType == Av1FrameUpdateType.Alternate)
            {
                this.arfQ = q;
            }
        }

        return q;
    }

    /// <summary>
    /// Returns the constant-quality quantizer index of one frame of the group without the temporal dependency
    /// replacement. An alternate reference records its index for the internal alternate references.
    /// Reference: av1_rc_pick_q_and_bounds() with rc_pick_q_and_bounds() and rc_pick_q_and_bounds_q_mode().
    /// </summary>
    /// <param name="groupIndex">The frame's index in the group.</param>
    /// <param name="screenContent">Whether the frame is screen content. Reference: is_screen_content_type.</param>
    /// <returns>The quantizer index.</returns>
    public int PickQIndex(int groupIndex, bool screenContent)
    {
        int activeBestQuality = 0;
        int activeWorst = this.activeWorstQuality;
        if (this.group.KeyFrames[groupIndex])
        {
            this.GetIntraQAndBounds(ref activeBestQuality, ref activeWorst, screenContent);
        }
        else
        {
            activeBestQuality = this.GetActiveBestQuality(activeWorst, groupIndex);
        }

        if (this.cqLevel > 0)
        {
            activeBestQuality = Math.Max(1, activeBestQuality);
        }

        int q = Math.Clamp(activeBestQuality, this.bestQuality, this.worstQuality);
        if (this.group.UpdateTypes[groupIndex] == Av1FrameUpdateType.Alternate)
        {
            this.arfQ = q;
        }

        return q;
    }

    /// <summary>
    /// Records a coded frame: the quantizer averages, the consumed statistics, the key frame counters and the group
    /// position. Reference: av1_rc_postencode_update(), av1_twopass_postencode_update(),
    /// update_keyframe_counters(), update_gf_group_index() and update_counters_for_show_frame().
    /// </summary>
    /// <param name="baseQIndex">The frame's base quantizer index; ignored for a frame that shows an existing one.</param>
    public void CompleteFrame(int baseQIndex)
    {
        Av1SecondPassFrame frame = this.current;
        int index = frame.GroupIndex;
        Av1FrameUpdateType updateType = frame.UpdateType;

        // A frame that shows an existing one leaves the quantizer of the last coded frame in place.
        int q = frame.ShowExistingFrame ? this.lastCodedQIndex : baseQIndex;

        // av1_configure_buffer_updates(): the refreshed references of the update role, where a shown key frame
        // refreshes every reference.
        bool refreshGolden;
        bool refreshAlternate;
        bool sourceIsAlternate = updateType is Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay;
        switch (updateType)
        {
            case Av1FrameUpdateType.Key:
                refreshGolden = true;
                refreshAlternate = true;
                break;
            case Av1FrameUpdateType.Golden:
                refreshGolden = true;
                refreshAlternate = false;
                break;
            case Av1FrameUpdateType.Overlay:
                refreshGolden = true;
                refreshAlternate = frame.ResetsReferences;
                break;
            case Av1FrameUpdateType.Alternate:
                refreshGolden = frame.ResetsReferences;
                refreshAlternate = true;
                break;
            default:
                refreshGolden = false;
                refreshAlternate = false;
                break;
        }

        if (frame.IsKeyFrame && frame.ShowFrame)
        {
            refreshGolden = true;
            refreshAlternate = true;
        }

        // av1_rc_postencode_update(): the running quantizer averages and the last boosted quantizer.
        bool intermediateArf = updateType == Av1FrameUpdateType.IntermediateAlternate;
        if (frame.IsKeyFrame)
        {
            this.averageKeyFrameQIndex = ((3 * this.averageKeyFrameQIndex) + q + 2) >> 2;
        }
        else if (!sourceIsAlternate && !(refreshGolden || intermediateArf || refreshAlternate))
        {
            this.averageInterFrameQIndex = ((3 * this.averageInterFrameQIndex) + q + 2) >> 2;
        }

        if (q < this.lastBoostedQIndex ||
            frame.IsKeyFrame ||
            (!this.constrainedGoldenGroup && (refreshAlternate || intermediateArf || (refreshGolden && !sourceIsAlternate))))
        {
            this.lastBoostedQIndex = q;
        }

        if (frame.IsKeyFrame)
        {
            this.lastKeyFrameQIndex = q;
            this.framesSinceKey = 0;
        }

        // av1_twopass_postencode_update(): a frame that is not an alternate reference consumes the statistics of
        // its display position, which input_stats_lap() removes from the front of the linear buffer.
        if (index < this.group.Size || this.framesToKey == 0)
        {
            if (updateType is not (Av1FrameUpdateType.Alternate or Av1FrameUpdateType.IntermediateAlternate))
            {
                --this.statisticsPosition;
                if (this.statisticsPosition < this.statisticsCount)
                {
                    int moved = this.statisticsCount - this.statisticsPosition - 1;
                    Array.Copy(this.statistics, 1, this.statistics, 0, moved);
                    --this.statisticsCount;
                }
            }
            else
            {
                this.statisticsPosition = 0;
            }
        }

        // update_keyframe_counters(): a shown frame advances the ring and the key frame counters.
        if (frame.ShowFrame && this.framesToKey != 0)
        {
            if (this.statisticsInfo.PastCount > 1)
            {
                this.statisticsInfo.MoveCurrentIndexAndPop();
            }
            else
            {
                this.statisticsInfo.MoveCurrentIndex();
            }

            this.framesSinceKey++;
            this.framesToKey--;
            this.framesToForwardKeyFrame--;
        }

        // update_gf_group_index().
        if (++this.groupFrameIndex == Av1GopStructure.MaximumLength)
        {
            this.groupFrameIndex = 0;
        }

        if (frame.ShowFrame)
        {
            this.frameNumber++;
            this.shownCount++;
        }

        if (!frame.ShowExistingFrame)
        {
            this.lastCodedQIndex = q;
        }
    }

    /// <summary>
    /// Gets the pyramid level that ranks a frame for reference mapping. Reference: get_true_pyr_level().
    /// </summary>
    /// <param name="frameLevel">The frame's layer depth.</param>
    /// <param name="frameOrder">The frame's display order.</param>
    /// <param name="maximumLayerDepth">The deepest layer of the group.</param>
    /// <returns>The pyramid level.</returns>
    private static int GetTruePyramidLevel(int frameLevel, int frameOrder, int maximumLayerDepth)
    {
        if (frameOrder == 0)
        {
            return MinimumPyramidLevel;
        }

        if (frameLevel == Av1GopStructure.MaximumArfLayers)
        {
            return maximumLayerDepth;
        }

        return frameLevel == Av1GopStructure.MaximumArfLayers + 1 ? MinimumPyramidLevel : Math.Max(MinimumPyramidLevel, frameLevel);
    }

    /// <summary>
    /// Decides the key frame and golden frame group state of the next frame, defining a new key frame group and a
    /// new golden frame group when the current group is finished.
    /// Reference: av1_get_second_pass_params() in the one-pass look-ahead stage.
    /// </summary>
    private void GetSecondPassParameters()
    {
        int startPosition = this.statisticsPosition;
        int index = this.groupFrameIndex;
        if (index < this.group.Size)
        {
            // An alternate reference reads the statistics of its source without consuming them.
            Av1FrameUpdateType updateType = this.group.UpdateTypes[index];
            if (updateType is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.IntermediateAlternate)
            {
                this.SetParametersFromStatistics(this.statisticsPosition + this.group.ArfSourceOffsets[index]);
                return;
            }
        }

        this.activeWorstQuality = this.cqLevel;
        if (this.groupFrameIndex == this.group.Size && this.sceneCutDetection != 0)
        {
            // A scene cut in the next 17 frames ends the key frame group there.
            int framesToKeyFrame = this.DefineKeyFrameInterval(MaximumLookaheadGoldenLength + 1, 0);
            if (framesToKeyFrame != -1)
            {
                this.framesToKey = Math.Min(this.framesToKey, framesToKeyFrame);
            }
        }

        Av1FirstPassStatistics thisFrame = default;
        if (this.groupFrameIndex < this.group.Size || this.framesToKey == 0)
        {
            this.ProcessFirstPassStatistics(ref thisFrame);
        }

        if (this.framesToKey <= 0)
        {
            this.FindNextKeyFrame(in thisFrame);
        }

        if (this.framesToForwardKeyFrame <= 0)
        {
            // The forward key frame distance is disabled. Reference: the fwd_kf_dist default of -1.
            this.framesToForwardKeyFrame = -1;
        }

        if (this.groupFrameIndex == this.group.Size)
        {
            this.DefineNewGoldenGroup(ref thisFrame);
        }

        index = this.groupFrameIndex;
        Av1FrameUpdateType currentType = this.group.UpdateTypes[index];
        if (currentType is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.IntermediateAlternate)
        {
            this.statisticsPosition = startPosition;
            this.SetParametersFromStatistics(this.statisticsPosition + this.group.ArfSourceOffsets[index]);
        }
    }

    /// <summary>
    /// Decides the length and the structure of a new golden frame group.
    /// Reference: the new group part of av1_get_second_pass_params() in the one-pass look-ahead stage.
    /// </summary>
    /// <param name="thisFrame">The statistics of the current frame.</param>
    private void DefineNewGoldenGroup(ref Av1FirstPassStatistics thisFrame)
    {
        this.gopLengthEvaluator?.BeginGroup();
        int maximumGopLength = this.lagInFrames >= 32
            ? Math.Min(MaximumGoldenInterval, this.lagInFrames - (ArnrMaximumFrames / 2))
            : MaximumLookaheadGoldenLength;

        maximumGopLength = Math.Min(maximumGopLength, this.framesToKey);

        // Identify the stable, varying, blending and scene cut regions of the look-ahead. The regions are indexed
        // from the frame being coded while their readers index them from the key frame; libaom keeps that offset.
        if (this.framesSinceKey == 0 ||
            this.framesSinceKey == 1 ||
            (this.framesTillRegionsUpdate - this.framesSinceKey < this.framesToKey &&
             this.framesTillRegionsUpdate - this.framesSinceKey < maximumGopLength + 1))
        {
            int restFrames = Math.Min(this.framesToKey, MaximumAnalysisFrames);
            int availableFrames = this.statisticsCount - this.statisticsPosition;
            restFrames = Math.Min(restFrames, availableFrames);
            this.framesTillRegionsUpdate = restFrames;
            this.MarkFlashes();
            this.EstimateNoise();
            this.EstimateCoefficients();
            this.IdentifyRegions(this.statisticsPosition, restFrames);
        }

        int currentRegion = this.FindRegionsIndex(this.framesSinceKey);
        if ((currentRegion >= 0 && this.regions[currentRegion].Type == RegionType.SceneCut) || this.framesSinceKey == 0)
        {
            // A group that starts at a scene cut has no alternate reference of the previous group before it.
            this.arfGoldenBoostLast = false;
        }

        this.CalculateGoldenLength(maximumGopLength);
        if (maximumGopLength > MaximumLookaheadGoldenLength &&
            this.lagInFrames >= 32 &&
            this.gopLengthDecisionMethod != 3)
        {
            int thisIndex = this.framesSinceKey + this.goldenIntervals[this.currentGoldenIndex] - 1;
            int thisRegion = this.FindRegionsIndex(thisIndex);
            int nextRegion = this.FindRegionsIndex(thisIndex + 1);
            bool isLastSceneCut = this.goldenIntervals[this.currentGoldenIndex] >= this.framesToKey ||
                (thisRegion != -1 && this.regions[thisRegion].Type == RegionType.SceneCut) ||
                (nextRegion != -1 && this.regions[nextRegion].Type == RegionType.SceneCut);

            int originalInterval = this.goldenIntervals[this.currentGoldenIndex];
            if (this.goldenIntervals[this.currentGoldenIndex] > MaximumLookaheadGoldenLength &&
                this.minimumGoldenInterval <= MaximumLookaheadGoldenLength)
            {
                // A trial definition of the long group lets the temporal dependency model judge its length.
                this.DefineGoldenGroup(false);
                if (this.IsShorterGoldenIntervalBetter())
                {
                    this.CalculateGoldenLength(MaximumLookaheadGoldenLength);
                    if (isLastSceneCut && originalInterval - this.goldenIntervals[this.currentGoldenIndex] < 4)
                    {
                        this.goldenIntervals[this.currentGoldenIndex] = originalInterval;
                    }
                }
            }
        }

        this.DefineGoldenGroup(false);
        if (this.group.UpdateTypes[this.groupFrameIndex] != Av1FrameUpdateType.Alternate && this.framesSinceKey > 0)
        {
            this.ProcessFirstPassStatistics(ref thisFrame);
        }

        this.DefineGoldenGroup(true);
    }

    /// <summary>
    /// Asks the temporal dependency model whether a golden interval of 16 frames codes better than the trial
    /// interval. Reference: is_shorter_gf_interval_better() with av1_tpl_preload_rc_estimate().
    /// </summary>
    /// <returns>Whether to shorten the interval.</returns>
    private bool IsShorterGoldenIntervalBetter()
    {
        if (this.gopLengthEvaluator is null)
        {
            return false;
        }

        // The model codes each frame of the trial group at its estimated quantizer. The screen content type is still
        // that of the last coded frame, because the new key frame, if any, has not yet been classified.
        this.PreloadTplQuantizers();

        bool shorten;
        if (this.gopLengthDecisionMethod == 2)
        {
            shorten = this.goldenBoost < this.statisticsUsedForGoldenBoost * GoldenMinimumBoost * 1.4 &&
                this.gopLengthEvaluator.SetupTplStatistics(this, 3) == 0;
        }
        else
        {
            bool completeTpl = true;
            bool temporalFilterEnabled = this.framesSinceKey > 0 && this.group.ArfIndex > -1;
            shorten = false;
            if (this.gopLengthDecisionMethod == 1)
            {
                int evaluation = this.gopLengthEvaluator.SetupTplStatistics(this, 2);
                if (evaluation != 2)
                {
                    completeTpl = false;
                    shorten = evaluation == 0;
                }
            }

            if (completeTpl)
            {
                shorten = this.gopLengthEvaluator.SetupTplStatistics(this, 1) == 0;

                // The model's statistics of the kept group are reused when the alternate reference is filtered.
                if (temporalFilterEnabled && !shorten)
                {
                    this.skipTplSetupStatistics = true;
                }
            }
        }

        return shorten;
    }

    /// <summary>
    /// Reads the statistics of the current frame and advances the read position.
    /// Reference: process_first_pass_stats() in AOM_Q mode.
    /// </summary>
    /// <param name="thisFrame">Receives the statistics, unchanged at the end of the buffer.</param>
    private void ProcessFirstPassStatistics(ref Av1FirstPassStatistics thisFrame)
    {
        if (this.statisticsPosition < this.statisticsCount)
        {
            thisFrame = this.statistics[this.statisticsPosition];
            ++this.statisticsPosition;
        }

        this.SetParameters(in thisFrame);
    }

    /// <summary>
    /// Sets the per-frame values the encoder reads from the statistics at a buffer position, unless the position
    /// is outside the buffer. Reference: read_frame_stats() with set_twopass_params_based_on_fp_stats().
    /// </summary>
    /// <param name="position">The buffer position.</param>
    private void SetParametersFromStatistics(int position)
    {
        if (position >= 0 && position < this.statisticsCount)
        {
            this.SetParameters(in this.statistics[position]);
        }
    }

    /// <summary>
    /// Sets the per-frame energy and content class the encoder reads from a frame's statistics.
    /// Reference: set_twopass_params_based_on_fp_stats().
    /// </summary>
    /// <param name="frameStatistics">The statistics.</param>
    private void SetParameters(in Av1FirstPassStatistics frameStatistics)
    {
        this.macroblockAverageEnergy = Av1FirstPassMath.Log1P(frameStatistics.IntraError);
        if (this.totalWaveletEnergy >= 0)
        {
            this.frameAverageHaarEnergy = Av1FirstPassMath.Log1P(frameStatistics.FrameAverageWaveletEnergy);
        }

        // Reference: FC_ANIMATION_THRESH.
        this.isGraphicsAnimation = frameStatistics.IntraSkipPercent >= 0.15;
    }

    /// <summary>
    /// Limits the frames to the next key frame to the frames left in a draining look-ahead.
    /// Reference: correct_frames_to_key() without a frame limit.
    /// </summary>
    private void CorrectFramesToKey()
    {
        int lookaheadSize = this.PendingFrameCount;
        if (lookaheadSize < this.lookaheadDepth)
        {
            this.framesToKey = Math.Min(this.framesToKey, lookaheadSize);
        }
    }
}
