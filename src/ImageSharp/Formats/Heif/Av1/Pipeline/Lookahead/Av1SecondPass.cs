// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// The frame-level decisions of good-quality one-pass coding with a look-ahead: key frame placement, golden frame
/// group length and structure, the alternate reference decisions, and the constant-quality quantizer of each frame.
/// The look-ahead stage runs a first pass on each frame as it enters and pushes its statistics here. The encode
/// stage then asks for one frame at a time. The decisions cover one thread and no forced key frames.
/// </summary>
/// <remarks>
/// <para>A caller drives one frame as follows.</para>
/// <list type="number">
/// <item>Push the statistics of every frame that enters the look-ahead with <see cref="PushStatistics"/>, in display
/// order. Before asking for a frame, push until <see cref="PendingFrameCount"/> reaches
/// <see cref="LookaheadDepth"/> or the input has ended. The frame decisions depend on this full look-ahead.</item>
/// <item>Call <see cref="TryBeginFrame"/>. It returns false when the look-ahead must be filled first or when every
/// frame is coded.</item>
/// <item>When the frame starts a group, run the temporal filter and the temporal dependency model on
/// <see cref="Group"/>. Both can read <see cref="PickQIndex(int, bool)"/> for any frame of the group. For an
/// <see cref="Av1FrameUpdateType.Alternate"/> frame, set <see cref="ShowExistingAlternateReference"/> from the
/// filtered frame test.</item>
/// <item>Unless the frame shows an existing frame, call <see cref="ChooseBaseQIndex"/> with the temporal
/// dependency results of the frame and code it with the returned quantizer index.</item>
/// <item>Call <see cref="CompleteFrame"/> with the coded quantizer index.</item>
/// </list>
/// </remarks>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// The default key frame boost.
    /// </summary>
    private const int DefaultKeyFrameBoost = 2300;

    /// <summary>
    /// The default golden boost.
    /// </summary>
    private const int DefaultGoldenBoost = 2000;

    /// <summary>
    /// The largest golden interval.
    /// </summary>
    private const int MaximumGoldenInterval = 32;

    /// <summary>
    /// The largest golden interval of a short look-ahead.
    /// </summary>
    private const int MaximumLookaheadGoldenLength = 16;

    /// <summary>
    /// The smallest golden interval that can use an alternate reference.
    /// </summary>
    private const int MinimumGoldenInterval = 4;

    /// <summary>
    /// The number of golden intervals the length decision can produce.
    /// </summary>
    private const int MaximumGoldenIntervalCount = 15;

    /// <summary>
    /// The number of frames after a key frame candidate that its test examines.
    /// </summary>
    private const int SceneCutKeyTestInterval = 16;

    /// <summary>
    /// The largest number of look-ahead statistics.
    /// </summary>
    private const int MaximumLookaheadBuffers = 48;

    /// <summary>
    /// The smallest look-ahead that allows an alternate reference.
    /// </summary>
    private const int AlternateReferenceMinimumLag = 3;

    /// <summary>
    /// The smallest key frame distance of the default configuration.
    /// </summary>
    private const int KeyFrameMinimumDistance = 0;

    /// <summary>
    /// The largest pyramid height of golden frame groups in the default configuration.
    /// </summary>
    private const int MaximumPyramidHeight = 5;

    /// <summary>
    /// The smallest pyramid height of golden frame groups in the default configuration. Zero lets a group drop its alternate reference.
    /// </summary>
    private const int MinimumPyramidHeight = 0;

    /// <summary>
    /// The largest number of frames the temporal filter blends in the default configuration.
    /// </summary>
    private const int ArnrMaximumFrames = 7;

    /// <summary>
    /// The lowest pyramid level that reference mapping uses.
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
    /// The linear buffer of look-ahead statistics from the first frame not yet shown to the newest analyzed frame.
    /// The flash, noise and correlation estimates are written here.
    /// </summary>
    private readonly Av1FirstPassStatistics[] statistics;

    /// <summary>
    /// The ring of unmodified statistics around the current frame.
    /// </summary>
    private readonly Av1SecondPassStatisticsInfo statisticsInfo = new();

    /// <summary>
    /// The golden frame group being coded.
    /// </summary>
    private readonly Av1GopStructure group = new();

    /// <summary>
    /// The golden intervals of the length decision.
    /// </summary>
    private readonly int[] goldenIntervals = new int[MaximumGoldenIntervalCount];

    /// <summary>
    /// The number of statistics in <see cref="statistics"/>.
    /// </summary>
    private int statisticsCount;

    /// <summary>
    /// The read position in <see cref="statistics"/>.
    /// </summary>
    private int statisticsPosition;

    private int pushedCount;
    private int shownCount;
    private int frameNumber;
    private int groupFrameIndex;
    private int framesToKey;
    private int framesSinceKey = 8;
    private int framesSinceGolden;
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
    private bool screenContentType;
    private bool isGraphicsAnimation;
    private double macroblockAverageEnergy;
    private double frameAverageHaarEnergy;
    private Av1SecondPassFrame current;

    /// <summary>
    /// The largest number of frames between key frames.
    /// </summary>
    private readonly int keyFrameMaximumDistance;

    /// <summary>
    /// The encoder sharpness.
    /// </summary>
    private readonly int sharpness;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SecondPass"/> class for the color sequence configuration: good-quality usage, the
    /// requested rate control mode at the default target rate, automatic key frames up to the largest key frame distance apart, automatic
    /// alternate references with a pyramid of up to five layers, and the temporal dependency model on.
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="cqLevel">The constant-quality quantizer index.</param>
    /// <param name="bestAllowedQIndex">The lowest allowed quantizer index.</param>
    /// <param name="worstAllowedQIndex">The highest allowed quantizer index.</param>
    /// <param name="speed">The cpu-used tier, 0 to 6.</param>
    /// <param name="lagInFrames">The requested look-ahead, at least 1.</param>
    /// <param name="framerate">The frame rate that sets the golden interval range.</param>
    /// <param name="keyFrameMaximumDistance">The largest number of frames between key frames.</param>
    /// <param name="sharpness">The encoder sharpness, 0 to 7.</param>
    /// <param name="mode">The rate control mode.</param>
    /// <param name="gopLengthEvaluator">
    /// The temporal dependency test that can shorten a golden interval above 16 frames at speeds 0 to 5, or null to keep every interval.
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
        int keyFrameMaximumDistance,
        int sharpness,
        Av1RateControlMode mode,
        IGopLengthEvaluator? gopLengthEvaluator)
    {
        this.mode = mode;
        this.framerate = framerate;
        this.keyFrameMaximumDistance = keyFrameMaximumDistance;
        this.sharpness = sharpness;
        this.width = width;
        this.height = height;
        this.bitDepth = bitDepth;
        this.cqLevel = cqLevel;
        this.bestQuality = bestAllowedQIndex;
        this.worstQuality = worstAllowedQIndex;
        this.lossless = bestAllowedQIndex == 0 && worstAllowedQIndex == 0;
        this.gopLengthEvaluator = gopLengthEvaluator;
        this.activeWorstQuality = cqLevel;

        // Count the 16x16 macroblocks over the frame aligned to 8 pixels. The frame splits into 4x4 mode info units, and four units in each
        // direction make one macroblock. The macroblock count rounds to the nearest whole macroblock.
        int modeInfoRows = Av1Math.AlignPowerOf2(height, 3) >> 2;
        int modeInfoColumns = Av1Math.AlignPowerOf2(width, 3) >> 2;
        this.macroblockRows = (modeInfoRows + 2) >> 2;
        this.macroblockCount = this.macroblockRows * ((modeInfoColumns + 2) >> 2);

        // A good-quality look-ahead of 32 to 38 frames becomes 39 frames for better temporal filtering. The look-ahead stage keeps the
        // requested length. Its buffers take their size from the requested value, and the stage itself has no lag. As a result, the encode
        // stage waits for that many frames.
        this.lagInFrames = Math.Clamp(lagInFrames, 0, MaximumLookaheadBuffers);
        if (this.lagInFrames is >= 32 and < 39)
        {
            this.lagInFrames = 39;
        }

        int lookaheadBuffers = Math.Min(lagInFrames, Math.Min(MaximumLookaheadBuffers, keyFrameMaximumDistance + SceneCutKeyTestInterval));
        this.lookaheadDepth = lookaheadBuffers;
        this.statistics = new Av1FirstPassStatistics[Math.Max(lookaheadBuffers + 1, MaximumLookaheadGoldenLength + 1)];

        // Scene cut detection needs enough look-ahead to test the frames after a candidate. Mode 0 turns detection off below 19 frames.
        // Mode 1 applies below 33 frames. Mode 2 applies from 33 frames on, which covers the full test interval after a candidate.
        this.sceneCutDetection = lookaheadBuffers < MaximumLookaheadGoldenLength + 3
            ? 0
            : lookaheadBuffers < MaximumLookaheadGoldenLength + SceneCutKeyTestInterval + 1 ? 1 : 2;

        // Set the default golden interval range. The minimum is one eighth of the frame rate, clamped to 4 to 32 frames. Above the pixel rate
        // of 4K at 20 frames per second, the minimum grows with the pixel rate. The maximum is three quarters of the frame rate, at most 32
        // frames, rounded up to an even count. It is then at least 32 frames and at least the minimum. A static scene can use one more frame.
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

        // Speeds 0 to 4 do the group length test on three alternate layers. Speed 5 does the test on two layers, after a boost test. Speed 6
        // does no test.
        this.gopLengthDecisionMethod = speed >= 6 ? 2 : speed >= 5 ? 1 : 0;

        // Speeds 0 and 1 can code any frame again. Faster speeds code only key frames, golden frames and alternate references again.
        this.recodesEveryFrame = speed < 2;
        this.recodeTolerance = GetRecodeTolerance(width, height, speed);

        this.averageKeyFrameQIndex = (worstAllowedQIndex + bestAllowedQIndex) / 2;
        this.averageInterFrameQIndex = (worstAllowedQIndex + bestAllowedQIndex) / 2;

        // Set the bits of one frame at the target rate, and the bits of the largest frame.
        this.averageFrameBandwidth = (int)Math.Min(Math.Round(TargetBandwidth / framerate), int.MaxValue);
        long maximumSectionBits = (long)this.averageFrameBandwidth * VariableBitrateMaximumSection / 100;
        this.maximumFrameBandwidth = (int)Math.Max(Math.Max((long)this.macroblockCount * MaximumMacroblockRate, MaximumRate1080P), maximumSectionBits);
        this.maximumBufferSize = MaximumBufferMilliseconds * TargetBandwidth / 1000;
        this.InitializeRateControl();
    }

    /// <summary>
    /// Runs the temporal dependency model on the trial group of a golden interval above 16 frames.
    /// </summary>
    internal interface IGopLengthEvaluator
    {
        /// <summary>
        /// Filters the key frame and alternate reference of the trial <see cref="Group"/> before the group length test.
        /// </summary>
        /// <param name="secondPass">The decisions that own the trial group.</param>
        void FilterGroup(Av1SecondPass secondPass);

        /// <summary>
        /// Runs the temporal dependency model for a group length evaluation. The model reads the preloaded <see cref="Av1GopStructure.QValues"/>.
        /// </summary>
        /// <param name="secondPass">The decisions that own the trial group.</param>
        /// <returns>The evaluation result: 0 to shorten, 1 to keep.</returns>
        int SetupTplStatistics(Av1SecondPass secondPass);

        /// <summary>
        /// Discards the filtered frames of the previous group before a new group is defined.
        /// </summary>
        void BeginGroup();

        /// <summary>
        /// Marks the source of the alternate reference of the previous group as unusable at a new key frame.
        /// </summary>
        void BeginKeyFrameInterval();
    }

    /// <summary>
    /// Gets the number of frames the look-ahead holds before the encode stage codes a frame.
    /// </summary>
    public int LookaheadDepth => this.lookaheadDepth;

    /// <summary>
    /// Gets the number of pushed frames that are not yet shown.
    /// </summary>
    public int PendingFrameCount => this.pushedCount - this.shownCount;

    /// <summary>
    /// Gets the golden frame group being coded.
    /// </summary>
    public Av1GopStructure Group => this.group;

    /// <summary>
    /// Gets the decisions of the frame being coded.
    /// </summary>
    public Av1SecondPassFrame Current => this.current;

    /// <summary>
    /// Sets a value indicating whether the overlay of the current alternate reference repeats the filtered
    /// alternate reference. The temporal filter decides it when the alternate reference is coded.
    /// </summary>
    public bool ShowExistingAlternateReference { private get; set; }

    /// <summary>
    /// Appends the first-pass statistics of the newest frame of the look-ahead.
    /// </summary>
    /// <param name="frameStatistics">The statistics.</param>
    public void PushStatistics(Av1FirstPassStatistics frameStatistics)
    {
        if (this.statisticsCount == this.statistics.Length)
        {
            throw new InvalidOperationException("The look-ahead holds more frames than its depth.");
        }

        this.statistics[this.statisticsCount++] = frameStatistics;
        this.statisticsInfo.Push(frameStatistics);
        Av1FirstPassStatisticsAccumulator.Accumulate(ref this.totalStatistics, frameStatistics);
        this.pushedCount++;
    }

    /// <summary>
    /// Makes the frame-level decisions of the next coded frame: the group position, the frame source, whether the frame is shown, the order hint
    /// and the pyramid level.
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

        bool startsGroup = this.groupFrameIndex == this.group.Size;
        this.GetSecondPassParameters();

        int index = this.groupFrameIndex;
        Av1FrameUpdateType updateType = this.group.UpdateTypes[index];

        // Mark an overlay, then set the bit target of the frame from its allocation. Both happen before a key frame restarts the display count.
        this.sourceIsAlternate = updateType is Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay;
        this.SetFrameTarget();

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

        // The first frame never repeats an existing frame.
        showExisting &= this.frameNumber != 0;
        if (updateType == Av1FrameUpdateType.Overlay)
        {
            this.ShowExistingAlternateReference = false;
        }

        // An alternate reference codes a future source and is hidden.
        int sourceOffset = showExisting ? 0 : this.group.ArfSourceOffsets[index];
        bool showFrame = showExisting || sourceOffset == 0;
        bool keyFrame = this.group.KeyFrames[index] && !showExisting;
        bool resetsReferences = this.group.ReferenceResets[index];
        int frameNumber = this.frameNumber;
        if (keyFrame && resetsReferences)
        {
            // A key frame that resets the references restarts the display count.
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
            IsGraphicsAnimation = this.isGraphicsAnimation,
            MacroblockAverageEnergy = this.macroblockAverageEnergy,
            FrameAverageHaarEnergy = this.frameAverageHaarEnergy
        };

        frame = this.current;
        return true;
    }

    /// <summary>
    /// Returns the quantizer index of the current frame after the temporal dependency model adjusts the golden boost. Under a bit budget, the
    /// rate model chooses the index. Otherwise the constant-quality choice applies, and the temporal dependency choice replaces it when the
    /// model has statistics for the frame.
    /// </summary>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="tplReady">
    /// Whether the temporal dependency model has ready statistics for the frame that show a dependency. This value gates the boost blend.
    /// </param>
    /// <param name="tplFrameValid">
    /// Whether the statistics entry of the frame is valid, ready or not. This value gates the quantizer replacement.
    /// </param>
    /// <param name="tplR0">The ratio of propagated cost to intra cost of the frame.</param>
    /// <param name="tplQStepRatio">
    /// The quantizer step ratio of the frame, or one without ready statistics.
    /// </param>
    /// <returns>The base quantizer index.</returns>
    public int ChooseBaseQIndex(bool screenContent, bool tplReady, bool tplFrameValid, double tplR0, double tplQStepRatio)
    {
        int index = this.groupFrameIndex;
        Av1FrameUpdateType updateType = this.group.UpdateTypes[index];
        if (tplReady && updateType is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Key)
        {
            // Project the boost of the model to the frames that the boost covers. Then blend it with the boost from the statistics.
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

        // The frame picks its quantizer with its own refresh flags, not with the flags of the last coded frame.
        this.screenContentType = screenContent;
        GetReferenceRefreshes(in this.current, out bool refreshGolden, out bool refreshAlternate);
        int q = this.PickQIndex(index, screenContent, refreshGolden || refreshAlternate);
        if (this.UsesBitBudget)
        {
            this.BeginRecode();
        }

        // Constant quality replaces the quantizer by the temporal dependency choice.
        if (!this.UsesBitBudget && tplFrameValid && !this.lossless)
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
    /// Returns the quantizer index of one frame of the group without the temporal dependency replacement. The choice uses the reference
    /// refresh that the encoder holds before the frame is coded, which is the refresh of the last coded frame. An alternate reference records
    /// its index for the internal alternate references.
    /// </summary>
    /// <param name="groupIndex">The index of the frame in the group.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <returns>The quantizer index.</returns>
    public int PickQIndex(int groupIndex, bool screenContent)
        => this.PickQIndex(groupIndex, screenContent, this.lastRefreshesBoostedReference);

    /// <summary>
    /// Returns the quantizer index of one frame of the group without the temporal dependency replacement: the constant-quality choice, or
    /// under a bit budget the choice of the rate model. An alternate reference records its index for the internal alternate references.
    /// </summary>
    /// <param name="groupIndex">The index of the frame in the group.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="refreshesBoostedReference">
    /// Whether the current refresh flags of the encoder refresh the golden or the alternate reference.
    /// </param>
    /// <returns>The quantizer index.</returns>
    private int PickQIndex(int groupIndex, bool screenContent, bool refreshesBoostedReference)
    {
        int q;
        if (this.UsesBitBudget)
        {
            q = this.PickBitBudgetQIndex(groupIndex, screenContent, refreshesBoostedReference);
        }
        else
        {
            int activeBestQuality = 0;
            int activeWorst = this.activeWorstQuality;
            if (this.group.KeyFrames[groupIndex])
            {
                this.GetIntraQAndBounds(ref activeBestQuality, ref activeWorst, screenContent);
            }
            else
            {
                activeBestQuality = this.GetActiveBestQuality(activeWorst, groupIndex, this.cqLevel);
            }

            if (this.cqLevel > 0)
            {
                activeBestQuality = Math.Max(1, activeBestQuality);
            }

            q = Math.Clamp(activeBestQuality, this.bestQuality, this.worstQuality);
        }

        if (this.group.UpdateTypes[groupIndex] == Av1FrameUpdateType.Alternate)
        {
            this.arfQ = q;
        }

        return q;
    }

    /// <summary>
    /// Records a coded frame: the rate control update from its size, the quantizer averages, the consumed statistics, the key frame counters
    /// and the group position.
    /// </summary>
    /// <param name="baseQIndex">The base quantizer index of the frame. A frame that shows an existing frame ignores this value.</param>
    /// <param name="frameBits">The coded size of the frame in bits, without the temporal delimiter.</param>
    public void CompleteFrame(int baseQIndex, long frameBits)
    {
        Av1SecondPassFrame frame = this.current;
        int index = frame.GroupIndex;
        Av1FrameUpdateType updateType = frame.UpdateType;

        // A frame that shows an existing one leaves the quantizer of the last coded frame in place.
        int q = frame.ShowExistingFrame ? this.lastCodedQIndex : baseQIndex;
        GetReferenceRefreshes(in frame, out bool refreshGolden, out bool refreshAlternate);
        bool sourceIsAlternate = updateType is Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay;

        // Update the rate correction from the frame size. Then update the running quantizer averages and the last boosted quantizer. Each
        // average keeps three quarters of its old value and adds one quarter of the new index, rounded to nearest.
        int projectedFrameSize = (int)Math.Min(frameBits, int.MaxValue);
        this.UpdateRateAfterFrame(projectedFrameSize, q, frame.IsKeyFrame, frame.ShowFrame);
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
        }

        // An alternate reference, a golden refresh or an overlay restarts the golden count. Any other shown frame advances it.
        if (this.lagInFrames >= AlternateReferenceMinimumLag && refreshAlternate && !frame.IsKeyFrame)
        {
            this.framesSinceGolden = 0;
        }
        else if (refreshGolden || sourceIsAlternate)
        {
            this.framesSinceGolden = 0;
        }
        else if (frame.ShowFrame)
        {
            this.framesSinceGolden++;
        }

        // A frame that is not an alternate reference consumes the statistics of its display position. These statistics leave the front of
        // the linear buffer. An alternate reference moves the read position back to the start.
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

        // Update the bits off target and the quantizer extensions.
        this.UpdateBitsAfterFrame(projectedFrameSize, q, frame.IsKeyFrame);
        this.lastRefreshesBoostedReference = refreshGolden || refreshAlternate;

        // A shown frame advances the ring and the key frame counters.
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

        // Advance the group position. The position wraps at the largest group length.
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
    /// Returns the references that a frame refreshes by its update role. A shown key frame that is coded refreshes every reference.
    /// </summary>
    /// <param name="frame">The decisions of the frame.</param>
    /// <param name="refreshGolden">Receives whether the frame refreshes the golden reference.</param>
    /// <param name="refreshAlternate">Receives whether the frame refreshes the alternate reference.</param>
    private static void GetReferenceRefreshes(in Av1SecondPassFrame frame, out bool refreshGolden, out bool refreshAlternate)
    {
        switch (frame.UpdateType)
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

        // A shown key frame that is coded, not shown from a slot, refreshes every reference.
        if (frame.IsKeyFrame && frame.ShowFrame && !frame.ShowExistingFrame)
        {
            refreshGolden = true;
            refreshAlternate = true;
        }
    }

    /// <summary>
    /// Gets the pyramid level that ranks a frame for reference mapping. The first frame ranks at the lowest level. A frame on the leaf
    /// layer ranks at the deepest layer of its group. A frame on the layer below the leaf layer ranks at the lowest level.
    /// </summary>
    /// <param name="frameLevel">The layer depth of the frame.</param>
    /// <param name="frameOrder">The display order of the frame.</param>
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
    /// Decides the key frame and golden frame group state of the next frame. When the current group is finished, the method defines a new
    /// key frame group and a new golden frame group.
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

        // Constant quality restarts the highest quantizer at the quality level. A bit budget keeps the estimate of the group.
        if (!this.UsesBitBudget)
        {
            this.activeWorstQuality = this.cqLevel;
        }

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
            this.FindNextKeyFrame(thisFrame);

            // The source of the alternate reference of the previous group cannot be used after a key frame.
            this.gopLengthEvaluator?.BeginKeyFrameInterval();
        }

        if (this.framesToForwardKeyFrame <= 0)
        {
            // The default configuration turns off the forward key frame distance, so the count stays at -1.
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
    /// </summary>
    /// <param name="thisFrame">The statistics of the current frame.</param>
    private void DefineNewGoldenGroup(ref Av1FirstPassStatistics thisFrame)
    {
        this.gopLengthEvaluator?.BeginGroup();
        int maximumGopLength = this.lagInFrames >= 32
            ? Math.Min(MaximumGoldenInterval, this.lagInFrames - (ArnrMaximumFrames / 2))
            : MaximumLookaheadGoldenLength;

        maximumGopLength = Math.Min(maximumGopLength, this.framesToKey);

        // Identify the stable, varying, blending and scene cut regions of the look-ahead. The regions are indexed from the frame being coded,
        // but their readers index them from the key frame. The encoder keeps this offset so that the group decisions stay the same as other
        // AV1 encoders.
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

        // The group length test needs a maximum group length above 16 frames, the temporal dependency model, a look-ahead of at least 32
        // frames and a speed that does the test.
        if (maximumGopLength > MaximumLookaheadGoldenLength &&
            this.gopLengthEvaluator is not null &&
            this.lagInFrames >= 32 &&
            this.gopLengthDecisionMethod != 2)
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
                // Define the long group as a trial and filter it once. Then the temporal dependency model judges its length. If a shorter
                // interval is better but the group ends at a scene cut, a cut of fewer than 4 frames keeps the original interval.
                this.DefineGoldenGroup(false);
                this.gopLengthEvaluator!.FilterGroup(this);
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
    /// Asks the temporal dependency model whether a golden interval of 16 frames codes better than the trial interval.
    /// </summary>
    /// <returns>Whether to shorten the interval.</returns>
    private bool IsShorterGoldenIntervalBetter()
    {
        if (this.gopLengthEvaluator is null)
        {
            return false;
        }

        // The model codes each frame of the trial group at its estimated quantizer. The screen content type is still that of the last coded
        // frame, because a new key frame is not classified yet.
        this.PreloadTplQuantizers();

        // Both methods use approximate statistics of the lower alternate layers. The second method also needs a low boost. The third method
        // does no test.
        if (this.gopLengthDecisionMethod == 1)
        {
            return this.goldenBoost < this.statisticsUsedForGoldenBoost * GoldenMinimumBoost * 1.4 && this.gopLengthEvaluator.SetupTplStatistics(this) == 0;
        }

        return this.gopLengthDecisionMethod == 0 && this.gopLengthEvaluator.SetupTplStatistics(this) == 0;
    }

    /// <summary>
    /// Reads the statistics of the current frame and advances the read position. Under a bit budget, the first frame
    /// of the sequence first estimates the highest quantizer from the statistics the look-ahead holds.
    /// </summary>
    /// <param name="thisFrame">Receives the statistics. The value does not change at the end of the buffer.</param>
    private void ProcessFirstPassStatistics(ref Av1FirstPassStatistics thisFrame)
    {
        if (this.UsesBitBudget && this.frameNumber == 0 && this.groupFrameIndex == 0)
        {
            this.InitializeFirstFrameQuality();
        }

        if (this.statisticsPosition < this.statisticsCount)
        {
            thisFrame = this.statistics[this.statisticsPosition];
            ++this.statisticsPosition;
        }

        this.SetParameters(thisFrame);
    }

    /// <summary>
    /// Sets the per-frame values the encoder reads from the statistics at a buffer position, unless the position is outside the buffer.
    /// </summary>
    /// <param name="position">The buffer position.</param>
    private void SetParametersFromStatistics(int position)
    {
        if (position >= 0 && position < this.statisticsCount)
        {
            this.SetParameters(this.statistics[position]);
        }
    }

    /// <summary>
    /// Sets the per-frame energy and content class the encoder reads from the statistics of a frame. The wavelet energy changes only when
    /// the look-ahead total has a valid energy.
    /// </summary>
    /// <param name="frameStatistics">The statistics.</param>
    private void SetParameters(Av1FirstPassStatistics frameStatistics)
    {
        this.macroblockAverageEnergy = Av1FirstPassMath.Log1P(frameStatistics.IntraError);
        if (this.totalStatistics.FrameAverageWaveletEnergy >= 0)
        {
            this.frameAverageHaarEnergy = Av1FirstPassMath.Log1P(frameStatistics.FrameAverageWaveletEnergy);
        }

        // A frame with at least 15 percent of intra skip blocks counts as animation or graphics.
        this.isGraphicsAnimation = frameStatistics.IntraSkipPercent >= 0.15;
    }

    /// <summary>
    /// Limits the frames to the next key frame to the frames left in a draining look-ahead.
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
