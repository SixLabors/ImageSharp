// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The one-pass rate control without first-pass statistics, for every rate control mode. The encoder codes at a default rate of 256 kbps.
/// In the bit-rate modes, the quantizer can move four steps on each side of the requested one. This matches the output of the AVIF reference encoder.
/// Real-time usage sets the target of each frame. It uses constant bit rate by default, a 1000 ms buffer, a 600 ms starting and optimal level, and 50%
/// undershoot and overshoot. The other usages allocate the bits of each golden group. They use a 6000 ms buffer, a 4000 ms starting level, a 5000 ms
/// optimal level, and 25% undershoot and overshoot.
/// </summary>
internal sealed class Av1RateControl
{
    /// <summary>
    /// The number of fraction bits in a bits-per-macroblock value.
    /// </summary>
    private const int BitsPerMacroblockShift = 9;

    /// <summary>
    /// The smallest bit target of a frame.
    /// </summary>
    private const int FrameOverheadBits = 200;

    /// <summary>
    /// The smallest rate correction factor.
    /// </summary>
    private const double MinimumBitsPerBlockFactor = 0.005;

    /// <summary>
    /// The largest rate correction factor.
    /// </summary>
    private const double MaximumBitsPerBlockFactor = 50;

    /// <summary>
    /// The bits per macroblock that the largest frame target always allows.
    /// </summary>
    private const int MaximumMacroblockRate = 250;

    /// <summary>
    /// The bits that the largest frame target always allows. This is <see cref="MaximumMacroblockRate"/> for the 8100 macroblocks of a 1080p frame.
    /// </summary>
    private const int MaximumRate1080P = 2025000;

    /// <summary>
    /// The key frame boost of one-pass coding. Real-time usage and the other usages without first-pass statistics both use 2300.
    /// </summary>
    private const int DefaultKeyFrameBoost = 2300;

    /// <summary>
    /// The key frame boost at and below which the high motion floor applies in real-time usage.
    /// </summary>
    private const int RealtimeKeyFrameLowBoost = 400;

    /// <summary>
    /// The key frame boost at and above which the low motion floor applies in real-time usage.
    /// </summary>
    private const int RealtimeKeyFrameHighBoost = 5000;

    /// <summary>
    /// The key frame boost at and below which the high motion floor applies in the other usages.
    /// </summary>
    private const int KeyFrameLowBoost = 553;

    /// <summary>
    /// The key frame boost at and above which the low motion floor applies in the other usages.
    /// </summary>
    private const int KeyFrameHighBoost = 8000;

    /// <summary>
    /// The golden boost of one-pass coding without first-pass statistics.
    /// </summary>
    private const int DefaultGoldenBoost = 2000;

    /// <summary>
    /// The golden boost at and below which the high motion floor applies in real-time usage.
    /// </summary>
    private const int RealtimeGoldenLowBoost = 300;

    /// <summary>
    /// The golden boost at and above which the low motion floor applies in real-time usage.
    /// </summary>
    private const int RealtimeGoldenHighBoost = 2400;

    /// <summary>
    /// The golden boost at and below which the high motion floor applies in the other usages while the average golden boost is below its threshold.
    /// Without first-pass statistics, the average is always below the threshold.
    /// </summary>
    private const int GoldenLowBoost = 562;

    /// <summary>
    /// The golden boost at and above which the low motion floor applies in the other usages while the average golden boost is below its threshold.
    /// </summary>
    private const int GoldenHighBoost = 2875;

    /// <summary>
    /// The bit target of a variable-bitrate key frame, in average frames.
    /// </summary>
    private const int VariableBitrateKeyFrameRatio = 25;

    /// <summary>
    /// The weight of the frames of a variable-bitrate golden group against its boosted first frame, outside real-time usage with a lookahead.
    /// </summary>
    private const int VariableBitrateGoldenRatio = 10;

    /// <summary>
    /// The ratio below which the actual bits of a constrained-quality coding lower its quality level.
    /// </summary>
    private const double ConstrainedQualityAdjustThreshold = 0.1;

    /// <summary>
    /// The target rate in bits per second. Every usage uses this default.
    /// </summary>
    private const long TargetBandwidth = 256 * 1000;

    /// <summary>
    /// The rate control mode.
    /// </summary>
    private readonly Av1RateControlMode mode;

    /// <summary>
    /// Whether the encoder runs in real-time usage. This selects the real-time quantizer tables and the real-time scene adjustments.
    /// </summary>
    private readonly bool realtime;

    /// <summary>
    /// The largest share, in percent, by which a constant-bitrate inter target falls below the average frame bandwidth while the buffer is below the
    /// optimal level.
    /// </summary>
    private readonly int underShootPercentage;

    /// <summary>
    /// The largest share, in percent, by which a constant-bitrate inter target rises above the average frame bandwidth while the buffer is above the
    /// optimal level.
    /// </summary>
    private readonly int overShootPercentage;

    /// <summary>
    /// The coded sample bit depth.
    /// </summary>
    private readonly Av1BitDepth bitDepth;

    /// <summary>
    /// The configured frame width. A scaled frame codes at a smaller width.
    /// </summary>
    private readonly int configuredWidth;

    /// <summary>
    /// The configured frame height. A scaled frame codes at a smaller height.
    /// </summary>
    private readonly int configuredHeight;

    /// <summary>
    /// The coded width of the current frame.
    /// </summary>
    private int width;

    /// <summary>
    /// The coded height of the current frame.
    /// </summary>
    private int height;

    /// <summary>
    /// The number of 16x16 macroblocks of the current frame.
    /// </summary>
    private int macroblockCount;

    /// <summary>
    /// The size of the primary reference frame of the current frame, or <see langword="null"/> without one.
    /// </summary>
    private Size? primaryReferenceSize;

    /// <summary>
    /// The constant-quality level, which bounds the constrained-quality mode.
    /// </summary>
    private int constantQualityLevel;

    /// <summary>
    /// The rate correction factor of golden refreshes outside the constant-bitrate mode.
    /// </summary>
    private double goldenCorrectionFactor;

    /// <summary>
    /// The quantizer index of the last ordinary inter frame.
    /// </summary>
    private int lastInterFrameQIndex;

    /// <summary>
    /// The bits that the coded frames used.
    /// </summary>
    private long totalActualBits;

    /// <summary>
    /// The sum of the average frame bandwidth over the shown frames.
    /// </summary>
    private long totalTargetBits;

    /// <summary>
    /// The bit allocation of the key frame of the current golden group.
    /// </summary>
    private int groupKeyFrameTarget;

    /// <summary>
    /// The bit allocation of the golden update that starts the current golden group.
    /// </summary>
    private int groupGoldenTarget;

    /// <summary>
    /// The bit allocation of each other frame of the current golden group.
    /// </summary>
    private int groupInterTarget;

    /// <summary>
    /// The target rate of the current frame per 64x64 area.
    /// </summary>
    private int superblockTargetRate;

    /// <summary>
    /// Whether the current frame refreshes GOLDEN. The quantizer pick sets it from the update type of the frame. The post-encode update sets it from
    /// the refresh flags of the coded frame. A golden refresh reads the golden quantizer floor. Outside the constant-bitrate mode, it also reads the
    /// golden rate correction factor. Real-time constant-bitrate coding gives a golden refresh no boost, so there it picks as an ordinary inter frame.
    /// </summary>
    private bool refreshesGolden;

    /// <summary>
    /// The lowest allowed quantizer index.
    /// </summary>
    private int bestQuality;

    /// <summary>
    /// The highest allowed quantizer index.
    /// </summary>
    private int worstQuality;

    /// <summary>
    /// Whether inter frames estimate their bits from the error against the LAST reconstruction.
    /// </summary>
    private readonly bool accurateBitEstimate;

    /// <summary>
    /// The frame rate.
    /// </summary>
    private readonly double framerate;

    /// <summary>
    /// The buffer level before the first frame, in bits.
    /// </summary>
    private readonly long startingBufferLevel;

    /// <summary>
    /// The buffer level that the constant-bitrate targets steer toward, in bits.
    /// </summary>
    private readonly long optimalBufferLevel;

    /// <summary>
    /// The largest buffer level, in bits.
    /// </summary>
    private readonly long maximumBufferSize;

    /// <summary>
    /// The largest number of frames between key frames.
    /// </summary>
    private readonly int keyFrameMaximumDistance;

    /// <summary>
    /// The cyclic refresh of the sequence, or <see langword="null"/> without it.
    /// </summary>
    private readonly Av1CyclicRefresh? cyclicRefresh;

    /// <summary>
    /// The bits per frame at the target rate.
    /// </summary>
    private int averageFrameBandwidth;

    /// <summary>
    /// The largest bit target of a frame.
    /// </summary>
    private int maximumFrameBandwidth;

    /// <summary>
    /// The average frame bandwidth when the previous frame was coded.
    /// </summary>
    private int previousAverageFrameBandwidth;

    /// <summary>
    /// The buffer level after the previous frame, in bits.
    /// </summary>
    private long bufferLevel;

    /// <summary>
    /// The bits saved against the target so far, capped at the buffer size.
    /// </summary>
    private long bitsOffTarget;

    /// <summary>
    /// The running average quantizer index of key frames.
    /// </summary>
    private int averageKeyFrameQIndex;

    /// <summary>
    /// The running average quantizer index of inter frames.
    /// </summary>
    private int averageInterFrameQIndex;

    /// <summary>
    /// The quantizer index of the last key frame.
    /// </summary>
    private int lastKeyFrameQIndex;

    /// <summary>
    /// The quantizer index of the last boosted frame. A forced key frame stays near this index.
    /// </summary>
    private int lastBoostedQIndex;

    /// <summary>
    /// The rate correction factor of key frames.
    /// </summary>
    private double keyFrameCorrectionFactor;

    /// <summary>
    /// The rate correction factor of ordinary inter frames.
    /// </summary>
    private double interFrameCorrectionFactor;

    /// <summary>
    /// The quantizer index of the previous frame.
    /// </summary>
    private int firstFrameQIndex;

    /// <summary>
    /// The quantizer index of the frame before the previous one.
    /// </summary>
    private int secondFrameQIndex;

    /// <summary>
    /// Whether the previous frame overshot (-1), undershot (1) or met (0) its expected size.
    /// </summary>
    private int firstFrameRateSign;

    /// <summary>
    /// Whether the frame before the previous one overshot (-1), undershot (1) or met (0) its expected size.
    /// </summary>
    private int secondFrameRateSign;

    /// <summary>
    /// The number of frames since the last key frame.
    /// </summary>
    private int framesSinceKey;

    /// <summary>
    /// The number of frames left before the next key frame.
    /// </summary>
    private int framesToKey;

    /// <summary>
    /// The key frame boost.
    /// </summary>
    private int keyFrameBoost;

    /// <summary>
    /// Whether the key frame interval placed the current key frame.
    /// </summary>
    private bool thisKeyFrameForced;

    /// <summary>
    /// The bit target of the current frame.
    /// </summary>
    private int thisFrameTarget;

    /// <summary>
    /// The running ratio between the coded inter frame size and the reconstruction error.
    /// </summary>
    private int bitEstimateRatio;

    /// <summary>
    /// The reconstruction error of the current inter frame, or <see cref="ulong.MaxValue"/> when it is not measured.
    /// </summary>
    private ulong reconstructionError;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1RateControl"/> class. It sets the buffer sizes, the starting quantizer averages and the
    /// limits that depend on the frame rate.
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="speed">The encoder speed tier.</param>
    /// <param name="mode">The rate control mode.</param>
    /// <param name="realtime">Whether the encoder runs in real-time usage.</param>
    /// <param name="bestAllowedQIndex">The lowest quantizer index that the frames can use.</param>
    /// <param name="worstAllowedQIndex">The highest quantizer index that the frames can use.</param>
    /// <param name="constantQualityLevel">The constant-quality level as a quantizer index.</param>
    /// <param name="keyFrameMaximumDistance">The largest number of frames between key frames.</param>
    /// <param name="usesAdaptiveQuantization">Whether the sequence uses any adaptive quantization mode.</param>
    /// <param name="cyclicRefresh">The cyclic refresh of the sequence, or <see langword="null"/> without it.</param>
    public Av1RateControl(
        int width,
        int height,
        Av1BitDepth bitDepth,
        HeifEncodingSpeed speed,
        Av1RateControlMode mode,
        bool realtime,
        int bestAllowedQIndex,
        int worstAllowedQIndex,
        int constantQualityLevel,
        int keyFrameMaximumDistance,
        bool usesAdaptiveQuantization,
        Av1CyclicRefresh? cyclicRefresh)
    {
        this.mode = mode;
        this.realtime = realtime;
        this.cyclicRefresh = cyclicRefresh;
        this.keyFrameMaximumDistance = keyFrameMaximumDistance;
        this.configuredWidth = width;
        this.configuredHeight = height;
        this.width = width;
        this.height = height;
        this.bitDepth = bitDepth;
        this.macroblockCount = GetMacroblockCount(width, height);
        this.bestQuality = bestAllowedQIndex;
        this.worstQuality = worstAllowedQIndex;
        this.constantQualityLevel = constantQualityLevel;

        // Real-time speed 7 estimates inter frame bits from the error against the last reconstruction. This applies to 8-bit frames from 360 to 720
        // lines without adaptive quantization. Lossless coding turns it off.
        int shortSide = Math.Min(width, height);
        this.accurateBitEstimate = realtime &&
            speed == HeifEncodingSpeed.Level7 &&
            shortSide >= 360 &&
            shortSide <= 720 &&
            bitDepth == Av1BitDepth.EightBit &&
            worstAllowedQIndex != 0 &&
            !usesAdaptiveQuantization;

        // Every frame has time stamp 0 and duration 1 in a 1/30 time base. The frame rate update thus sees one constant duration of 333333 ticks
        // of 10 MHz, which gives a frame rate a little above 30.
        this.framerate = 10000000.0 / 333333;

        // Real-time usage keeps a 1000 ms buffer, a 600 ms starting and optimal level, and 50% undershoot and overshoot. The good-quality and
        // all-intra usages keep a 6000 ms buffer, a 4000 ms starting level, a 5000 ms optimal level, and 25% undershoot and overshoot.
        this.startingBufferLevel = (realtime ? 600 : 4000) * TargetBandwidth / 1000;
        this.optimalBufferLevel = (realtime ? 600 : 5000) * TargetBandwidth / 1000;
        this.maximumBufferSize = (realtime ? 1000 : 6000) * TargetBandwidth / 1000;
        this.underShootPercentage = realtime ? 50 : 25;
        this.overShootPercentage = realtime ? 50 : 25;
        this.bufferLevel = this.startingBufferLevel;
        this.bitsOffTarget = this.startingBufferLevel;

        // One-pass constant-bitrate coding starts both averages at the worst quantizer. Every other coding starts them halfway between the allowed
        // quantizers.
        int initialAverage = mode == Av1RateControlMode.ConstantBitRate
            ? worstAllowedQIndex
            : (worstAllowedQIndex + bestAllowedQIndex) / 2;

        this.averageKeyFrameQIndex = initialAverage;
        this.averageInterFrameQIndex = initialAverage;
        this.lastKeyFrameQIndex = bestAllowedQIndex;
        this.lastInterFrameQIndex = worstAllowedQIndex;
        this.keyFrameCorrectionFactor = 1.0;
        this.goldenCorrectionFactor = 0.7;
        this.interFrameCorrectionFactor = 0.7;
        this.framesSinceKey = 8;
        this.reconstructionError = ulong.MaxValue;
        this.UpdateFrameRate(new Size(width, height));
    }

    /// <summary>
    /// Gets the running average quantizer index of inter frames.
    /// </summary>
    public int AverageInterFrameQIndex => this.averageInterFrameQIndex;

    /// <summary>
    /// Gets the coded sample bit depth.
    /// </summary>
    public Av1BitDepth BitDepth => this.bitDepth;

    /// <summary>
    /// Gets the lowest allowed quantizer index.
    /// </summary>
    public int BestQuality => this.bestQuality;

    /// <summary>
    /// Gets a value indicating whether every allowed quantizer is lossless.
    /// </summary>
    public bool IsLosslessRequested => this.bestQuality == 0 && this.worstQuality == 0;

    /// <summary>
    /// Gets the bits per frame at the target rate.
    /// </summary>
    public int AverageFrameBandwidth => this.averageFrameBandwidth;

    /// <summary>
    /// Gets the target rate of the current frame per 64x64 area.
    /// </summary>
    public int SuperblockTargetRate => this.superblockTargetRate;

    /// <summary>
    /// Gets a value indicating whether the key frame interval places a key frame on the next frame. Automatic key frames are on, because the
    /// smallest key frame distance (0) differs from the largest.
    /// </summary>
    public bool IsKeyFrameDue => this.framesToKey == 0;

    /// <summary>
    /// Gets the frames left before the next key frame.
    /// </summary>
    public int FramesToKey => this.framesToKey;

    /// <summary>
    /// Gets the linear coefficients of the quantizer floor curves. Each row holds, in order, the low and the high motion key frame curves, the low
    /// and the high motion golden and alternate reference curves, and the inter curve. The first two rows hold the good-quality curves below 608
    /// lines and from 608 lines. The last two rows hold the real-time curves, which are the same for every resolution.
    /// </summary>
    private static ReadOnlySpan<double> CurveCoefficients =>
    [
        0.1771, 0.379, 0.3279, 0.6634, 1.385,
        0.1917, 0.3760, 0.34570, 0.6916, 1.14820,
        0.15, 0.45, 0.30, 0.55, 0.90,
        0.15, 0.45, 0.30, 0.55, 0.90
    ];

    /// <summary>
    /// Gets a value indicating whether the current inter frame reads the golden rate correction factor. This is true for a golden update outside
    /// the constant-bitrate mode. The constant-bitrate mode gives a golden update no boost, so it uses the inter factor.
    /// </summary>
    private bool UsesGoldenCorrectionFactor => this.refreshesGolden && this.mode != Av1RateControlMode.ConstantBitRate;

    /// <summary>
    /// Applies a configuration change. The encoder makes this change before each layer of a layered image whose quality differs from the layer
    /// before it. A lossless range resets the quantizer averages. The frame rate limits of the change do not last, because every frame sets them
    /// again before its target. The rest of the model stays, because the bit rate and frame rate do not change.
    /// </summary>
    /// <param name="bestAllowedQIndex">The lowest quantizer index that the frames can use.</param>
    /// <param name="worstAllowedQIndex">The highest quantizer index that the frames can use.</param>
    /// <param name="constantQualityLevel">The constant-quality level as a quantizer index.</param>
    public void ChangeConfiguration(int bestAllowedQIndex, int worstAllowedQIndex, int constantQualityLevel)
    {
        this.bestQuality = bestAllowedQIndex;
        this.worstQuality = worstAllowedQIndex;
        this.constantQualityLevel = constantQualityLevel;
        if (this.IsLosslessRequested)
        {
            this.averageKeyFrameQIndex = 0;
            this.averageInterFrameQIndex = 0;
        }
    }

    /// <summary>
    /// Sets the coded size of the frame that the quantizer pick and the post-encode update read. A scaled frame counts fewer macroblocks. Its rate
    /// correction factor scales by the area that it no longer codes.
    /// </summary>
    /// <param name="frameSize">The coded frame size.</param>
    /// <param name="primaryReferenceSize">The size of the primary reference frame, or <see langword="null"/> without one.</param>
    public void SetFrameSize(Size frameSize, Size? primaryReferenceSize)
    {
        this.width = frameSize.Width;
        this.height = frameSize.Height;
        this.macroblockCount = GetMacroblockCount(frameSize.Width, frameSize.Height);
        this.primaryReferenceSize = primaryReferenceSize;
    }

    /// <summary>
    /// Sets the limits that depend on the frame rate before the target of a frame. Every frame has time stamp 0, so each frame takes the step
    /// update of the frame rate. That update sets the limits from the frame size at that point. This is the size of the frame before it or, after a
    /// configuration change, the configured size. The largest frame target allows 2000% of the average frame bandwidth, which is the default
    /// of every usage.
    /// </summary>
    /// <param name="frameSize">The frame size that sets the limits.</param>
    public void UpdateFrameRate(Size frameSize)
    {
        int macroblocks = GetMacroblockCount(frameSize.Width, frameSize.Height);
        this.averageFrameBandwidth = (int)Math.Round(TargetBandwidth / this.framerate, MidpointRounding.AwayFromZero);
        long maximumSectionBits = Math.Min((long)this.averageFrameBandwidth * 2000 / 100, int.MaxValue);
        this.maximumFrameBandwidth = Math.Max(Math.Max(macroblocks * MaximumMacroblockRate, MaximumRate1080P), (int)maximumSectionBits);
    }

    /// <summary>
    /// Sets the frame type state and the bit target of a real-time frame. Constant-bitrate frames steer toward the optimal buffer level. The other
    /// modes give the key frame and the golden update that starts a group more bits than the other frames of the group.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <param name="goldenUpdate">Whether the frame starts a golden group.</param>
    /// <param name="goldenInterval">The length of the current golden group.</param>
    /// <param name="targetSize">
    /// The frame size when the target is set. This is the size of the frame before it or, after a configuration change, the configured size.
    /// </param>
    public void BeginFrame(bool keyFrame, uint frameNumber, bool goldenUpdate, int goldenInterval, Size targetSize)
    {
        Debug.Assert(this.realtime, "Only real-time coding sets the target per frame.");

        this.UpdateFrameRate(targetSize);
        if (keyFrame)
        {
            this.thisKeyFrameForced = frameNumber != 0 && this.framesToKey == 0;
            this.framesToKey = this.keyFrameMaximumDistance;
            this.keyFrameBoost = DefaultKeyFrameBoost;
        }

        int target;
        if (this.mode == Av1RateControlMode.ConstantBitRate)
        {
            target = keyFrame ? this.GetIntraFrameTarget(frameNumber) : this.GetInterFrameTarget();
        }
        else
        {
            // Real-time coding without lookahead weighs the boosted first frame of a group like the good-quality usage. The group spends the bits
            // of goldenInterval average frames. The first frame counts as VariableBitrateGoldenRatio frames and each other frame counts as one.
            long groupWeight = goldenInterval + VariableBitrateGoldenRatio - 1;
            target = keyFrame
                ? (int)Math.Min((long)this.averageFrameBandwidth * VariableBitrateKeyFrameRatio, this.maximumFrameBandwidth)
                : goldenUpdate
                    ? this.ClampInterFrameTarget((long)this.averageFrameBandwidth * goldenInterval * VariableBitrateGoldenRatio / groupWeight)
                    : this.ClampInterFrameTarget((long)this.averageFrameBandwidth * goldenInterval / groupWeight);
        }

        this.SetFrameTarget(target, targetSize);
    }

    /// <summary>
    /// Resets the buffer and the inter rate model when a real-time frame codes at a size other than the frame before it. A scaled layer causes
    /// this. The buffer returns to the optimal level. A larger frame raises the inter average toward the worst quantizer. The inter correction
    /// factor moves by the distance between the quantizer that the model expects at the new size and the worst or the last inter quantizer. The
    /// expectation reads the frame type, the correction factors and the cyclic refresh state of the frame before. The frame in setup did not
    /// replace them yet.
    /// </summary>
    /// <param name="resizeSize">The size of the frame in setup.</param>
    /// <param name="previousSize">The size of the frame before.</param>
    /// <param name="previousFrameIntra">Whether the frame before was intra only. The frame type still holds this value.</param>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <param name="screenContent">Whether the content is screen content.</param>
    /// <param name="sourceSad">The scene statistics of this frame.</param>
    public void ResetForResize(
        Size resizeSize,
        Size previousSize,
        bool previousFrameIntra,
        uint frameNumber,
        bool screenContent,
        in SourceSadStatistics sourceSad)
    {
        Debug.Assert(this.realtime, "Only real-time coding resets the rate model for a resize.");

        double scaleChange = (double)((long)resizeSize.Width * resizeSize.Height) / ((long)previousSize.Width * previousSize.Height);
        this.bufferLevel = this.optimalBufferLevel;
        this.bitsOffTarget = this.optimalBufferLevel;
        this.thisFrameTarget = this.GetInterFrameTarget();
        if (scaleChange > 4.0)
        {
            this.averageInterFrameQIndex = this.worstQuality;
        }
        else if (scaleChange > 1.0)
        {
            this.averageInterFrameQIndex = (this.averageInterFrameQIndex + this.worstQuality) >> 1;
        }

        int activeWorstQuality = this.GetActiveWorstQuality(previousFrameIntra, frameNumber);

        // The expectation spreads the target over the macroblocks of the new size and scales the factor by its area. The accurate estimate still
        // reads the macroblocks of the frame before, because the encoder still holds that grid.
        int savedWidth = this.width;
        int savedHeight = this.height;
        this.width = resizeSize.Width;
        this.height = resizeSize.Height;
        int targetBitsPerMacroblock = (int)(((ulong)this.thisFrameTarget << BitsPerMacroblockShift) / (ulong)GetMacroblockCount(resizeSize.Width, resizeSize.Height));
        int qIndex = this.RegulateQuantizer(previousFrameIntra, screenContent, in sourceSad, this.bestQuality, activeWorstQuality, targetBitsPerMacroblock);
        this.width = savedWidth;
        this.height = savedHeight;

        // A smaller frame near the worst quantizer can afford a lower one. A larger frame keeps its quantizer close to the last inter quantizer.
        if (scaleChange < 1.0 && qIndex > 90 * this.worstQuality / 100)
        {
            this.interFrameCorrectionFactor *= 0.85;
        }

        if (scaleChange >= 1.0)
        {
            if (scaleChange < 4.0 && qIndex > 130 * this.lastInterFrameQIndex / 100)
            {
                this.interFrameCorrectionFactor *= 0.8;
            }

            if (qIndex <= 120 * this.lastInterFrameQIndex / 100)
            {
                this.interFrameCorrectionFactor *= 1.5;
            }
        }
    }

    /// <summary>
    /// Allocates the bits of a new golden group of one-pass coding without statistics. The key frame or the golden update that starts the group
    /// gets a boosted target. Every other frame gets the same target. A constant-bitrate group reads the buffer level when the group starts.
    /// </summary>
    /// <param name="groupLength">The number of frames of the group.</param>
    /// <param name="frameNumber">The number of frames coded before the first frame of the group, before a key frame restarts the count.</param>
    /// <param name="keyFrame">Whether a key frame starts the group.</param>
    public void DefineGroup(int groupLength, uint frameNumber, bool keyFrame)
    {
        Debug.Assert(!this.realtime, "Real-time coding sets its targets per frame.");

        // A new key frame group starts to count its frames at the key frame before it sets its targets. Thus a later constant-bitrate key frame
        // gets no boost.
        if (keyFrame)
        {
            this.framesSinceKey = 0;
        }

        if (this.mode == Av1RateControlMode.ConstantBitRate)
        {
            this.groupKeyFrameTarget = this.GetIntraFrameTarget(frameNumber);
            this.groupGoldenTarget = this.GetInterFrameTarget();
            this.groupInterTarget = this.groupGoldenTarget;
            return;
        }

        this.groupKeyFrameTarget = (int)Math.Min((long)this.averageFrameBandwidth * VariableBitrateKeyFrameRatio, this.maximumFrameBandwidth);

        // The group spends the bits of groupLength average frames. The boosted first frame counts as VariableBitrateGoldenRatio frames and each
        // other frame counts as one.
        long groupWeight = groupLength + VariableBitrateGoldenRatio - 1;
        this.groupGoldenTarget = this.ClampInterFrameTarget((long)this.averageFrameBandwidth * groupLength * VariableBitrateGoldenRatio / groupWeight);
        this.groupInterTarget = this.ClampInterFrameTarget((long)this.averageFrameBandwidth * groupLength / groupWeight);
    }

    /// <summary>
    /// Sets the frame type state and the bit target of a frame of one-pass coding without statistics. The target comes from the allocation of the
    /// golden group of the frame.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="keyFrameForced">Whether the key frame interval placed this key frame.</param>
    /// <param name="goldenUpdate">Whether the frame is the golden update that starts its group.</param>
    /// <param name="framesToKey">The frames left before the next key frame, which the sequence encoder counts.</param>
    /// <param name="targetSize">
    /// The frame size when the target is set. This is the size of the frame before it or, after a configuration change, the configured size.
    /// </param>
    public void BeginGroupFrame(bool keyFrame, bool keyFrameForced, bool goldenUpdate, int framesToKey, Size targetSize)
    {
        Debug.Assert(!this.realtime, "Real-time coding sets its targets per frame.");

        this.framesToKey = framesToKey;
        if (keyFrame)
        {
            this.thisKeyFrameForced = keyFrameForced;
            this.keyFrameBoost = DefaultKeyFrameBoost;
        }

        int target = keyFrame ? this.groupKeyFrameTarget : goldenUpdate ? this.groupGoldenTarget : this.groupInterTarget;
        this.SetFrameTarget(target, targetSize);
    }

    /// <summary>
    /// Sets the bit target of the current frame and its rate per 64x64 area. Outside the constant-bitrate mode, a frame coded below the configured
    /// size scales its target by the area that it no longer codes.
    /// </summary>
    /// <param name="target">The target in bits.</param>
    /// <param name="targetSize">The frame size when the target is set.</param>
    private void SetFrameTarget(int target, Size targetSize)
    {
        this.thisFrameTarget = target;
        if (this.mode != Av1RateControlMode.ConstantBitRate && this.IsScaled(targetSize))
        {
            this.thisFrameTarget = SaturateToInt(this.thisFrameTarget * this.GetResizeRateFactor(targetSize));
        }

        this.superblockTargetRate = GetSuperblockTargetRate(this.thisFrameTarget, targetSize.Width, targetSize.Height);
    }

    /// <summary>
    /// Clamps the bit target of a variable-bitrate inter frame between the smallest and the largest frame target. The smallest target is
    /// <see cref="FrameOverheadBits"/> or 1/32 of the average frame bandwidth, whichever is larger.
    /// </summary>
    /// <param name="target">The target in bits.</param>
    /// <returns>The clamped target.</returns>
    private int ClampInterFrameTarget(long target)
    {
        int minimumFrameTarget = Math.Max(FrameOverheadBits, this.averageFrameBandwidth >> 5);
        return (int)Math.Min(Math.Max(target, minimumFrameTarget), this.maximumFrameBandwidth);
    }

    /// <summary>
    /// Returns whether a frame codes below the configured size.
    /// </summary>
    /// <param name="frameSize">The frame size.</param>
    /// <returns><see langword="true"/> when the frame is scaled.</returns>
    private bool IsScaled(Size frameSize)
        => frameSize.Width != this.configuredWidth || frameSize.Height != this.configuredHeight;

    /// <summary>
    /// Returns how many times more samples the configured frame holds than a frame of the given size.
    /// </summary>
    /// <param name="frameSize">The frame size.</param>
    /// <returns>The ratio of the configured and the given area.</returns>
    private double GetResizeRateFactor(Size frameSize)
        => (double)((long)this.configuredWidth * this.configuredHeight) / ((long)frameSize.Width * frameSize.Height);

    /// <summary>
    /// Converts a value to an integer. Values outside the integer range saturate at its limits.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The saturated integer.</returns>
    private static int SaturateToInt(double value)
        => value >= int.MaxValue ? int.MaxValue : value <= int.MinValue ? int.MinValue : (int)value;

    /// <summary>
    /// Returns the bit target of a constant-bitrate key frame. The first frame gets half the starting buffer level. A later key frame gets a boost
    /// over the average frame bandwidth, which is smaller when it comes less than half a second after the last key frame. The largest frame
    /// target limits the result.
    /// </summary>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <returns>The target in bits.</returns>
    private int GetIntraFrameTarget(uint frameNumber)
    {
        long target;
        if (frameNumber == 0)
        {
            target = this.startingBufferLevel / 2 > int.MaxValue ? int.MaxValue : this.startingBufferLevel / 2;
        }
        else
        {
            int boost = Math.Max(32, (int)Math.Round((2 * this.framerate) - 16, MidpointRounding.AwayFromZero));
            if (this.framesSinceKey < this.framerate / 2)
            {
                boost = (int)(boost * this.framesSinceKey / (this.framerate / 2));
            }

            target = ((long)(16 + boost) * this.averageFrameBandwidth) >> 4;
        }

        return (int)Math.Min(target, this.maximumFrameBandwidth);
    }

    /// <summary>
    /// Returns the bit target of a constant-bitrate inter frame, moved toward the optimal buffer level. Each percent of the optimal level by which
    /// the buffer misses it moves the target by half a percent of the average frame bandwidth, up to half of the undershoot or overshoot share.
    /// The target is at least <see cref="FrameOverheadBits"/> or 1/16 of the average frame bandwidth, whichever is larger.
    /// </summary>
    /// <returns>The target in bits.</returns>
    private int GetInterFrameTarget()
    {
        long difference = this.optimalBufferLevel - this.bufferLevel;
        long onePercentBits = 1 + (this.optimalBufferLevel / 100);
        int minimumFrameTarget = Math.Max(this.averageFrameBandwidth >> 4, FrameOverheadBits);
        long target = this.averageFrameBandwidth;
        if (difference > 0)
        {
            int percentLow = (int)Math.Min(difference / onePercentBits, this.underShootPercentage);
            target -= target * percentLow / 200;
        }
        else if (difference < 0)
        {
            int percentHigh = (int)Math.Min(-difference / onePercentBits, this.overShootPercentage);
            target += target * percentHigh / 200;
        }

        return Math.Max(minimumFrameTarget, (int)Math.Min(target, int.MaxValue));
    }

    /// <summary>
    /// Picks the quantizer index of a real-time frame by the rate control mode. A constant-bitrate inter frame with the accurate estimate first
    /// measures its error against the LAST reconstruction.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <typeparam name="TMotion">The error operations.</typeparam>
    /// <typeparam name="TBlock">The block averaging operations.</typeparam>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded since the last key frame, 0 for a key frame.</param>
    /// <param name="goldenUpdate">Whether the frame starts a golden group. Constant-quality coding reads this as the update type of the frame.</param>
    /// <param name="refreshesGolden">Whether the frame refreshes GOLDEN. The variable-bitrate floors and correction factor read this value.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="sourceSad">The scene statistics of this frame.</param>
    /// <param name="source">The bordered source luma plane.</param>
    /// <param name="lastReconstruction">The bordered luma plane of the LAST reference, for an inter frame.</param>
    /// <returns>The quantizer index.</returns>
    public int PickQuantizer<TSample, TMotion, TBlock>(
        bool keyFrame,
        uint frameNumber,
        bool goldenUpdate,
        bool refreshesGolden,
        bool screenContent,
        in SourceSadStatistics sourceSad,
        Av1PlaneRegion<TSample> source,
        Av1PlaneRegion<TSample> lastReconstruction)
        where TSample : unmanaged
        where TMotion : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
        where TBlock : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        Debug.Assert(this.realtime, "Only real-time coding picks through this entry.");

        this.refreshesGolden = refreshesGolden;
        this.reconstructionError = ulong.MaxValue;
        switch (this.mode)
        {
            case Av1RateControlMode.ConstantBitRate:
                if (this.accurateBitEstimate && !keyFrame)
                {
                    this.MeasureReconstructionError<TSample, TMotion, TBlock>(source, lastReconstruction);
                }

                return this.PickConstantBitrateQuantizer(keyFrame, frameNumber, screenContent, in sourceSad);
            case Av1RateControlMode.Quality:
                return this.PickConstantQualityQuantizer(keyFrame, goldenUpdate, screenContent);
            default:
                return this.PickVariableBitrateQuantizer(keyFrame, frameNumber, screenContent);
        }
    }

    /// <summary>
    /// Picks the quantizer index of a real-time constant-quality frame. A key frame uses the key frame floor of the real-time tables. The golden
    /// update that starts a group uses the high motion golden floor. Every other frame uses the constant-quality level. The one-pass golden group
    /// leaves the boost factor of the golden floor at zero.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="goldenUpdate">Whether the frame starts a golden group.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <returns>The quantizer index.</returns>
    private int PickConstantQualityQuantizer(bool keyFrame, bool goldenUpdate, bool screenContent)
    {
        // The active worst quality of constant-quality coding is the quality level.
        int level = this.constantQualityLevel;
        int activeWorstQuality = level;
        int activeBestQuality;
        if (keyFrame)
        {
            if (this.framesToKey <= 1)
            {
                // A key frame followed by another key frame codes at the quality level.
                activeBestQuality = level;
            }
            else if (this.thisKeyFrameForced)
            {
                // A forced key frame keeps the quantizer near the last boosted one to limit popping.
                double lastBoostedQ = ConvertQIndexToQ(this.lastBoostedQIndex, this.bitDepth);
                int deltaQIndex = this.ComputeQDelta(lastBoostedQ, lastBoostedQ * 0.5);
                activeBestQuality = Math.Max(this.lastBoostedQIndex + deltaQIndex, this.bestQuality);
            }
            else
            {
                activeBestQuality = this.GetKeyFrameActiveQuality(activeWorstQuality);
                if (screenContent)
                {
                    activeBestQuality /= 2;
                }

                // Small formats allow a somewhat lower key frame quantizer.
                double adjustmentFactor = this.width * this.height <= 352 * 288 ? 0.75 : 1.0;
                double q = ConvertQIndexToQ(activeBestQuality, this.bitDepth);
                activeBestQuality += this.ComputeQDelta(q, q * adjustmentFactor);
            }
        }
        else if (goldenUpdate)
        {
            // After the frame that follows the key frame, the lower of the active worst quality and the recent inter average sets the golden
            // floor. The boost factor is zero, so the floor stays at the high motion floor.
            int q = this.framesSinceKey > 1 && this.averageInterFrameQIndex < activeWorstQuality
                ? this.averageInterFrameQIndex
                : activeWorstQuality;

            activeBestQuality = this.GetGoldenHighMotionQuality(q);
        }
        else
        {
            activeBestQuality = level;
        }

        if (level > 0)
        {
            activeBestQuality = Math.Max(1, activeBestQuality);
        }

        return Av1Math.Clamp(activeBestQuality, this.bestQuality, this.worstQuality);
    }

    /// <summary>
    /// Picks the quantizer index of a frame of one-pass coding without statistics outside real-time usage, by the rate control mode.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="goldenUpdate">Whether the frame is the golden update that starts its group.</param>
    /// <param name="frameNumber">The number of frames coded since the last key frame, 0 for a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <returns>The quantizer index.</returns>
    public int PickGroupFrameQuantizer(bool keyFrame, bool goldenUpdate, uint frameNumber, bool screenContent)
    {
        Debug.Assert(!this.realtime, "Real-time coding picks through PickQuantizer.");

        this.refreshesGolden = goldenUpdate;
        this.reconstructionError = ulong.MaxValue;
        return this.mode == Av1RateControlMode.ConstantBitRate
            ? this.PickConstantBitrateQuantizer(keyFrame, frameNumber, screenContent, default)
            : this.PickVariableBitrateQuantizer(keyFrame, frameNumber, screenContent);
    }

    /// <summary>
    /// Picks the quantizer index of a variable-bitrate or constrained-quality frame. The index lies between a floor from the recent quantizers and
    /// a ceiling from the last quantizers.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded since the last key frame, 0 for a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <returns>The quantizer index.</returns>
    private int PickVariableBitrateQuantizer(bool keyFrame, uint frameNumber, bool screenContent)
    {
        bool constrainedQuality = this.mode == Av1RateControlMode.ConstrainedQuality;
        int constantQualityLevel = this.GetActiveConstantQualityLevel();
        int activeWorstQuality = this.GetVariableBitrateActiveWorstQuality(keyFrame, frameNumber);
        int activeBestQuality;
        if (keyFrame)
        {
            if (this.thisKeyFrameForced)
            {
                // A forced key frame keeps the quantizer near the last boosted one to limit popping.
                double lastBoostedQ = ConvertQIndexToQ(this.lastBoostedQIndex, this.bitDepth);
                int deltaQIndex = this.ComputeQDelta(lastBoostedQ, lastBoostedQ * 0.75);
                activeBestQuality = Math.Max(this.lastBoostedQIndex + deltaQIndex, this.bestQuality);
            }
            else
            {
                activeBestQuality = this.GetKeyFrameActiveQuality(this.averageKeyFrameQIndex);

                // Small formats allow a somewhat lower key frame quantizer.
                double adjustmentFactor = this.width * this.height <= 352 * 288 ? 0.75 : 1.0;
                double q = ConvertQIndexToQ(activeBestQuality, this.bitDepth);
                activeBestQuality += this.ComputeQDelta(q, q * adjustmentFactor);
            }
        }
        else if (this.refreshesGolden)
        {
            // After the frame that follows the key frame, the recent inter average sets the golden floor when it is below the active worst
            // quality. Otherwise the key frame average sets it. The constrained-quality mode keeps it at or above the quality level.
            int q = this.framesSinceKey > 1 && this.averageInterFrameQIndex < activeWorstQuality
                ? this.averageInterFrameQIndex
                : this.averageKeyFrameQIndex;

            if (constrainedQuality)
            {
                q = Math.Max(q, constantQualityLevel);
            }

            activeBestQuality = this.GetGoldenActiveQuality(q);

            // The constrained-quality mode uses a slightly lower floor.
            if (constrainedQuality)
            {
                activeBestQuality = activeBestQuality * 15 / 16;
            }
        }
        else
        {
            // The recent average of the frame type before it sets the inter floor. The constrained-quality mode keeps it at or above the quality
            // level.
            activeBestQuality = this.GetInterActiveQuality(frameNumber > 1 ? this.averageInterFrameQIndex : this.averageKeyFrameQIndex);
            if (constrainedQuality && activeBestQuality < constantQualityLevel)
            {
                activeBestQuality = constantQualityLevel;
            }
        }

        activeBestQuality = Av1Math.Clamp(activeBestQuality, this.bestQuality, this.worstQuality);
        activeWorstQuality = Av1Math.Clamp(activeWorstQuality, activeBestQuality, this.worstQuality);
        int topIndex = activeWorstQuality;
        int bottomIndex = activeBestQuality;

        // A later key frame that the interval did not force can exceed the active worst quality by the quantizer change for a rate ratio of 2. A
        // golden update can exceed it by the change for a rate ratio of 1.75.
        int qDelta = 0;
        if (keyFrame && !this.thisKeyFrameForced && frameNumber != 0)
        {
            qDelta = this.GetQDeltaByRate(keyFrame, screenContent, activeWorstQuality, 2.0);
        }
        else if (!keyFrame && this.refreshesGolden)
        {
            qDelta = this.GetQDeltaByRate(keyFrame, screenContent, activeWorstQuality, 1.75);
        }

        topIndex = Math.Max(activeWorstQuality + qDelta, bottomIndex);

        // A forced key frame codes at the last boosted quantizer to match the quality around it.
        if (keyFrame && this.thisKeyFrameForced)
        {
            return this.lastBoostedQIndex;
        }

        int correctedQ = this.FindClosestQIndexByRate(
            keyFrame,
            screenContent,
            this.GetTargetBitsPerMacroblock(),
            this.GetRateCorrectionFactor(keyFrame),
            activeBestQuality,
            activeWorstQuality);

        // A frame whose target is the largest frame target keeps the chosen quantizer above the top index.
        return correctedQ > topIndex && this.thisFrameTarget < this.maximumFrameBandwidth ? topIndex : correctedQ;
    }

    /// <summary>
    /// Returns the highest quantizer that a variable-bitrate or constrained-quality frame can use, from the last quantizers. A first key frame can
    /// use any allowed quantizer. The first frame after a key frame follows the key frame quantizer. Later frames follow the last inter quantizer.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded since the last key frame, 0 for a key frame.</param>
    /// <returns>The active worst quality.</returns>
    private int GetVariableBitrateActiveWorstQuality(bool keyFrame, uint frameNumber)
    {
        int activeWorstQuality;
        if (keyFrame)
        {
            activeWorstQuality = frameNumber == 0 ? this.worstQuality : this.lastKeyFrameQIndex * 2;
        }
        else if (this.refreshesGolden)
        {
            activeWorstQuality = frameNumber == 1 ? this.lastKeyFrameQIndex * 5 / 4 : this.lastInterFrameQIndex;
        }
        else
        {
            activeWorstQuality = frameNumber == 1 ? this.lastKeyFrameQIndex * 2 : this.lastInterFrameQIndex * 2;
        }

        return Math.Min(activeWorstQuality, this.worstQuality);
    }

    /// <summary>
    /// Returns the constant-quality level that the constrained-quality mode keeps the frames at or above. When the coding spends less than a tenth
    /// of its budget, the level falls in proportion.
    /// </summary>
    /// <returns>The active constant-quality level.</returns>
    private int GetActiveConstantQualityLevel()
    {
        int level = this.constantQualityLevel;
        if (this.mode == Av1RateControlMode.ConstrainedQuality && this.totalTargetBits > 0)
        {
            double ratio = (double)this.totalActualBits / this.totalTargetBits;
            if (ratio < ConstrainedQualityAdjustThreshold)
            {
                level = (int)(level * ratio / ConstrainedQualityAdjustThreshold);
            }
        }

        return level;
    }

    /// <summary>
    /// Returns the golden quantizer floor between the low and high motion golden curves, by the golden boost. Real-time usage reads the real-time
    /// tables. The other usages read the good-quality tables of the resolution class of the frame. Without first-pass statistics, the average
    /// golden boost stays 0, so the first pair of good-quality boost bounds applies. Both usages use the same default golden boost.
    /// </summary>
    /// <param name="q">The quantizer index that the floor applies to.</param>
    /// <returns>The golden active quality.</returns>
    private int GetGoldenActiveQuality(int q)
    {
        bool large = Math.Min(this.width, this.height) >= 608;
        double maximumQ = ConvertQIndexToQ(q, this.bitDepth);
        int lowMotion = GetMinimumQIndex(maximumQ, 0.0000015, -0.0009, GetCurveCoefficient(this.realtime, large, 2), this.bitDepth);
        int highMotion = this.GetGoldenHighMotionQuality(q);
        return this.realtime
            ? GetActiveQuality(DefaultGoldenBoost, RealtimeGoldenLowBoost, RealtimeGoldenHighBoost, lowMotion, highMotion)
            : GetActiveQuality(DefaultGoldenBoost, GoldenLowBoost, GoldenHighBoost, lowMotion, highMotion);
    }

    /// <summary>
    /// Returns the high motion golden quantizer floor. Real-time usage reads the real-time tables. The other usages read the good-quality tables of
    /// the resolution class of the frame.
    /// </summary>
    /// <param name="q">The quantizer index that the floor applies to.</param>
    /// <returns>The high motion golden floor.</returns>
    private int GetGoldenHighMotionQuality(int q)
    {
        bool large = Math.Min(this.width, this.height) >= 608;
        return GetMinimumQIndex(ConvertQIndexToQ(q, this.bitDepth), 0.0000021, -0.00125, GetCurveCoefficient(this.realtime, large, 3), this.bitDepth);
    }

    /// <summary>
    /// Returns the inter quantizer floor from the inter curve. Real-time usage reads the real-time tables. The other usages read the good-quality
    /// tables of the resolution class of the frame.
    /// </summary>
    /// <param name="q">The quantizer index that the floor applies to.</param>
    /// <returns>The inter active quality.</returns>
    private int GetInterActiveQuality(int q)
    {
        bool large = Math.Min(this.width, this.height) >= 608;
        return GetMinimumQIndex(ConvertQIndexToQ(q, this.bitDepth), 0.00000271, -0.00113, GetCurveCoefficient(this.realtime, large, 4), this.bitDepth);
    }

    /// <summary>
    /// Picks the quantizer index of a constant-bitrate frame from the buffer state.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="sourceSad">The scene statistics of this frame.</param>
    /// <returns>The quantizer index.</returns>
    private int PickConstantBitrateQuantizer(bool keyFrame, uint frameNumber, bool screenContent, in SourceSadStatistics sourceSad)
    {
        int activeWorstQuality = this.GetActiveWorstQuality(keyFrame, frameNumber);
        int activeBestQuality = this.GetActiveBestQuality(keyFrame, frameNumber, activeWorstQuality);
        activeBestQuality = Av1Math.Clamp(activeBestQuality, this.bestQuality, this.worstQuality);
        activeWorstQuality = Av1Math.Clamp(activeWorstQuality, activeBestQuality, this.worstQuality);
        int topIndex = activeWorstQuality;
        int bottomIndex = activeBestQuality;

        // Limit the range of a later key frame that the interval did not force.
        if (keyFrame && !this.thisKeyFrameForced && frameNumber != 0)
        {
            topIndex = activeWorstQuality + this.GetQDeltaByRate(keyFrame, screenContent, activeWorstQuality, 2.0);
            topIndex = Math.Max(topIndex, bottomIndex);
        }

        int q = this.RegulateQuantizer(keyFrame, screenContent, in sourceSad, activeBestQuality, activeWorstQuality, this.GetTargetBitsPerMacroblock());
        if (q > topIndex)
        {
            // A frame whose target is the largest frame target keeps the chosen quantizer.
            if (this.thisFrameTarget >= this.maximumFrameBandwidth)
            {
                topIndex = q;
            }
            else
            {
                q = topIndex;
            }
        }

        return q;
    }

    /// <summary>
    /// Measures the squared error between the 4x4-averaged source and the LAST reconstruction over 64x64 blocks. The sum includes the replicated
    /// border that edge blocks reach. A zero sum is stored as 1.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <typeparam name="TMotion">The error operations.</typeparam>
    /// <typeparam name="TBlock">The block averaging operations.</typeparam>
    /// <param name="source">The bordered source luma plane.</param>
    /// <param name="lastReconstruction">The bordered LAST reconstruction luma plane.</param>
    private void MeasureReconstructionError<TSample, TMotion, TBlock>(Av1PlaneRegion<TSample> source, Av1PlaneRegion<TSample> lastReconstruction)
        where TSample : unmanaged
        where TMotion : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
        where TBlock : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        int modeInfoColumns = Av1Math.AlignPowerOf2(this.width, 3) >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoRows = Av1Math.AlignPowerOf2(this.height, 3) >> Av1Constants.ModeInfoSizeLog2;
        int columns = (modeInfoColumns + 15) / 16;
        int rows = (modeInfoRows + 15) / 16;
        Span<TSample> averaged = stackalloc TSample[64 * 64];

        // Edge blocks reach into the replicated border, so the averages read the complete bordered buffer.
        ReadOnlySpan<TSample> sourceSamples = source.Samples;
        ReadOnlySpan<TSample> lastSamples = lastReconstruction.Samples;
        ulong total = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Point origin = new(column << 6, row << 6);
                for (int y = 0; y < 64; y += 4)
                {
                    for (int x = 0; x < 64; x += 4)
                    {
                        int offset = source.GetOffset(origin.X + x, origin.Y + y);
                        TSample average = TBlock.CreateSample(TBlock.GetAverage4x4(sourceSamples[offset..], source.Stride));
                        for (int m = 0; m < 4; m++)
                        {
                            averaged.Slice(((y + m) * 64) + x, 4).Fill(average);
                        }
                    }
                }

                TMotion.GetMoments(
                    averaged,
                    64,
                    lastSamples[lastReconstruction.GetOffset(origin.X, origin.Y)..],
                    lastReconstruction.Stride,
                    64,
                    64,
                    out _,
                    out long squares);

                total += (ulong)squares;
            }
        }

        this.reconstructionError = total > 0 ? total : 1;
    }

    /// <summary>
    /// Returns the highest quantizer that the frame can use, from the buffer fullness. Spatial variance, cyclic refresh and layers do not change
    /// the result.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <returns>The active worst quality.</returns>
    private int GetActiveWorstQuality(bool keyFrame, uint frameNumber)
    {
        if (keyFrame)
        {
            return this.worstQuality;
        }

        // The first frames after a key frame weigh the key frame quantizer into the ambient quantizer.
        const uint keyWeightFrames = 5;
        long criticalLevel = this.optimalBufferLevel >> 3;
        int ambientQuantizer = frameNumber < keyWeightFrames
            ? Math.Min(this.averageInterFrameQIndex, this.averageKeyFrameQIndex)
            : this.averageInterFrameQIndex;

        ambientQuantizer = Math.Min(this.worstQuality, ambientQuantizer);

        int activeWorstQuality;
        if (this.bufferLevel > this.optimalBufferLevel)
        {
            // A buffer above the optimal level lowers the quantizer with its fullness.
            activeWorstQuality = Math.Min(this.worstQuality, ambientQuantizer * 5 / 4);
            int maximumAdjustmentDown = activeWorstQuality / 3;
            if (maximumAdjustmentDown != 0)
            {
                long bufferStep = (this.maximumBufferSize - this.optimalBufferLevel) / maximumAdjustmentDown;
                if (bufferStep != 0)
                {
                    activeWorstQuality -= (int)((this.bufferLevel - this.optimalBufferLevel) / bufferStep);
                }
            }
        }
        else if (this.bufferLevel > criticalLevel)
        {
            // A buffer between the critical and the optimal level raises the quantizer from the ambient one.
            activeWorstQuality = Math.Min(this.worstQuality, ambientQuantizer);
            if (criticalLevel != 0)
            {
                long bufferStep = this.optimalBufferLevel - criticalLevel;
                if (bufferStep != 0)
                {
                    activeWorstQuality += (int)((this.worstQuality - ambientQuantizer) * (this.optimalBufferLevel - this.bufferLevel) / bufferStep);
                }
            }
        }
        else
        {
            activeWorstQuality = this.worstQuality;
        }

        return activeWorstQuality;
    }

    /// <summary>
    /// Returns the lowest quantizer that a constant-bitrate frame can use. Key frames read the key frame tables of the usage. Inter frames read the
    /// real-time table in every usage. A golden update gets no boost.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <param name="activeWorstQuality">The active worst quality.</param>
    /// <returns>The active best quality.</returns>
    private int GetActiveBestQuality(bool keyFrame, uint frameNumber, int activeWorstQuality)
    {
        int activeBestQuality = this.bestQuality;
        if (keyFrame)
        {
            if (this.thisKeyFrameForced)
            {
                // A forced key frame keeps the quantizer near the last boosted one to limit popping.
                double lastBoostedQ = ConvertQIndexToQ(this.lastBoostedQIndex, this.bitDepth);
                int deltaQIndex = this.ComputeQDelta(lastBoostedQ, lastBoostedQ * 0.75);
                activeBestQuality = Math.Max(this.lastBoostedQIndex + deltaQIndex, this.bestQuality);
            }
            else if (frameNumber > 0)
            {
                double adjustmentFactor = 1.0;
                activeBestQuality = this.GetKeyFrameActiveQuality(this.averageKeyFrameQIndex);

                // Small formats allow a somewhat lower key frame quantizer.
                if (this.width * this.height <= 352 * 288)
                {
                    adjustmentFactor -= 0.25;
                }

                double q = ConvertQIndexToQ(activeBestQuality, this.bitDepth);
                activeBestQuality += this.ComputeQDelta(q, q * adjustmentFactor);
            }
        }
        else
        {
            // The real-time floor of the lower of the active worst quality and the recent average sets the lowest quantizer.
            int average = frameNumber > 1 ? this.averageInterFrameQIndex : this.averageKeyFrameQIndex;
            activeBestQuality = average < activeWorstQuality
                ? GetRealtimeMinimumQuality(average, this.bitDepth)
                : GetRealtimeMinimumQuality(activeWorstQuality, this.bitDepth);
        }

        return activeBestQuality;
    }

    /// <summary>
    /// Returns the quantizer index of a still image that codes against a bit budget. A still image is the first and only frame of its encoder, and
    /// it codes in all-intra usage. Thus the rate model starts from its initial state.
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="speed">The encoder speed tier.</param>
    /// <param name="mode">The rate control mode, any but <see cref="Av1RateControlMode.Quality"/>.</param>
    /// <param name="bestAllowedQIndex">The lowest allowed quantizer index.</param>
    /// <param name="worstAllowedQIndex">The highest allowed quantizer index.</param>
    /// <param name="constantQualityLevel">The constant-quality level as a quantizer index.</param>
    /// <param name="screenContent">Whether the image is screen content.</param>
    /// <returns>The quantizer index.</returns>
    public static int GetStillImageQIndex(
        int width,
        int height,
        Av1BitDepth bitDepth,
        HeifEncodingSpeed speed,
        Av1RateControlMode mode,
        int bestAllowedQIndex,
        int worstAllowedQIndex,
        int constantQualityLevel,
        bool screenContent)
    {
        Debug.Assert(mode != Av1RateControlMode.Quality, "Constant-quality still images code at the requested quantizer.");

        // The all-intra usage sets a key frame distance of 0. The adaptive quantization flag only selects the accurate bit estimate of real-time
        // usage, so it has no effect here.
        Av1RateControl rateControl = new(
            width,
            height,
            bitDepth,
            speed,
            mode,
            realtime: false,
            bestAllowedQIndex,
            worstAllowedQIndex,
            constantQualityLevel,
            keyFrameMaximumDistance: 0,
            usesAdaptiveQuantization: false,
            cyclicRefresh: null);

        // The image is the whole one-frame key frame group of its encoder.
        Size frameSize = new(width, height);
        rateControl.DefineGroup(groupLength: 1, frameNumber: 0, keyFrame: true);
        rateControl.BeginGroupFrame(keyFrame: true, keyFrameForced: false, goldenUpdate: false, framesToKey: 1, frameSize);
        return rateControl.PickGroupFrameQuantizer(keyFrame: true, goldenUpdate: false, frameNumber: 0, screenContent);
    }

    /// <summary>
    /// Returns the quantizer index of a key frame that the interval did not force, in one-pass constant-quality coding without lookahead. The
    /// index is the key frame floor of the good-quality tables at the default boost, lowered for small formats. The active worst quality is the
    /// constant-quality index.
    /// </summary>
    /// <param name="cqLevel">The constant-quality index.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="bestAllowedQIndex">The lowest allowed quantizer index.</param>
    /// <param name="worstAllowedQIndex">The highest allowed quantizer index.</param>
    /// <returns>The key frame quantizer index.</returns>
    public static int GetConstantQualityKeyFrameQIndex(
        int cqLevel,
        int width,
        int height,
        Av1BitDepth bitDepth,
        bool screenContent,
        int bestAllowedQIndex,
        int worstAllowedQIndex)
    {
        const int defaultKeyFrameBoost = 2300;
        const int keyFrameLow = 553;
        const int keyFrameHigh = 8000;

        // A frame of 608 lines or more reads the good-quality tables of the large class. A smaller frame reads the tables of the small class.
        bool large = Math.Min(width, height) >= 608;
        double maximumQ = ConvertQIndexToQ(cqLevel, bitDepth);
        int lowMotion = GetMinimumQIndex(maximumQ, 0.000001, -0.0004, GetCurveCoefficient(false, large, 0), bitDepth);
        int highMotion = GetMinimumQIndex(maximumQ, 0.0000021, -0.00125, GetCurveCoefficient(false, large, 1), bitDepth);
        int activeBestQuality = GetActiveQuality(defaultKeyFrameBoost, keyFrameLow, keyFrameHigh, lowMotion, highMotion);
        if (screenContent)
        {
            activeBestQuality /= 2;
        }

        // Small formats allow a somewhat lower key frame quantizer.
        double adjustmentFactor = width * height <= 352 * 288 ? 0.75 : 1.0;
        double q = ConvertQIndexToQ(activeBestQuality, bitDepth);
        activeBestQuality += FindQIndex(q * adjustmentFactor, bitDepth, bestAllowedQIndex, worstAllowedQIndex) -
            FindQIndex(q, bitDepth, bestAllowedQIndex, worstAllowedQIndex);

        if (cqLevel > 0)
        {
            activeBestQuality = Math.Max(1, activeBestQuality);
        }

        return Av1Math.Clamp(activeBestQuality, bestAllowedQIndex, worstAllowedQIndex);
    }

    /// <summary>
    /// Returns the quantizer index of a key frame that the key frame interval placed, in one-pass constant-quality coding without lookahead. It
    /// stays near the last boosted quantizer to limit a quality jump. The index is the one for half the real quantizer of the last boosted index,
    /// and no lower than the best allowed index.
    /// </summary>
    /// <param name="cqLevel">The constant-quality index.</param>
    /// <param name="lastBoostedQIndex">The last boosted quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="bestAllowedQIndex">The lowest allowed quantizer index.</param>
    /// <param name="worstAllowedQIndex">The highest allowed quantizer index.</param>
    /// <returns>The key frame quantizer index.</returns>
    public static int GetConstantQualityForcedKeyFrameQIndex(
        int cqLevel,
        int lastBoostedQIndex,
        Av1BitDepth bitDepth,
        int bestAllowedQIndex,
        int worstAllowedQIndex)
    {
        double lastBoostedQ = ConvertQIndexToQ(lastBoostedQIndex, bitDepth);
        int deltaQIndex = FindQIndex(lastBoostedQ * 0.5, bitDepth, bestAllowedQIndex, worstAllowedQIndex) -
            FindQIndex(lastBoostedQ, bitDepth, bestAllowedQIndex, worstAllowedQIndex);

        int activeBestQuality = Math.Max(lastBoostedQIndex + deltaQIndex, bestAllowedQIndex);
        if (cqLevel > 0)
        {
            activeBestQuality = Math.Max(1, activeBestQuality);
        }

        return Av1Math.Clamp(activeBestQuality, bestAllowedQIndex, worstAllowedQIndex);
    }

    /// <summary>
    /// Returns the quantizer index of a golden or alternate-reference update in one-pass constant-quality coding without lookahead. The base is
    /// the constant-quality index. After the first inter frame, the running average of the ordinary inter frames replaces it when that average is
    /// lower. The result is the high motion floor of the good-quality golden table, because the one-pass group definition leaves the boost factor
    /// at zero.
    /// </summary>
    /// <param name="cqLevel">The constant-quality index.</param>
    /// <param name="averageInterQIndex">The running average quantizer of the ordinary inter frames.</param>
    /// <param name="framesSinceKey">The number of frames since the last key frame.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="bestAllowedQIndex">The lowest allowed quantizer index.</param>
    /// <param name="worstAllowedQIndex">The highest allowed quantizer index.</param>
    /// <returns>The golden frame quantizer index.</returns>
    public static int GetConstantQualityGoldenFrameQIndex(
        int cqLevel,
        int averageInterQIndex,
        int framesSinceKey,
        int width,
        int height,
        Av1BitDepth bitDepth,
        int bestAllowedQIndex,
        int worstAllowedQIndex)
    {
        // The active worst quality of constant-quality coding is the constant-quality index. When more than one frame follows the key frame, the
        // lower recent average applies.
        int q = framesSinceKey > 1 && averageInterQIndex < cqLevel ? averageInterQIndex : cqLevel;

        // The boost factor is zero, so the floor stays at the high motion floor. A frame of 608 lines or more reads the good-quality tables of the
        // large class. A smaller frame reads the tables of the small class.
        bool large = Math.Min(width, height) >= 608;
        int activeBestQuality = GetMinimumQIndex(
            ConvertQIndexToQ(q, bitDepth), 0.0000021, -0.00125, GetCurveCoefficient(false, large, 3), bitDepth);

        if (cqLevel > 0)
        {
            activeBestQuality = Math.Max(1, activeBestQuality);
        }

        return Av1Math.Clamp(activeBestQuality, bestAllowedQIndex, worstAllowedQIndex);
    }

    /// <summary>
    /// Returns the key frame quantizer floor between the low and high motion curves, by the key frame boost. Real-time usage reads the real-time
    /// tables. The other usages read the good-quality tables of the resolution class of the frame.
    /// </summary>
    /// <param name="q">The quantizer index that the floor applies to.</param>
    /// <returns>The key frame active quality.</returns>
    private int GetKeyFrameActiveQuality(int q)
    {
        bool large = Math.Min(this.width, this.height) >= 608;
        double maximumQ = ConvertQIndexToQ(q, this.bitDepth);
        int lowMotion = GetMinimumQIndex(maximumQ, 0.000001, -0.0004, GetCurveCoefficient(this.realtime, large, 0), this.bitDepth);
        int highMotion = GetMinimumQIndex(maximumQ, 0.0000021, -0.00125, GetCurveCoefficient(this.realtime, large, 1), this.bitDepth);
        return this.realtime
            ? GetActiveQuality(this.keyFrameBoost, RealtimeKeyFrameLowBoost, RealtimeKeyFrameHighBoost, lowMotion, highMotion)
            : GetActiveQuality(this.keyFrameBoost, KeyFrameLowBoost, KeyFrameHighBoost, lowMotion, highMotion);
    }

    /// <summary>
    /// Returns the linear coefficient of a quantizer floor curve for the usage and the resolution class. Real-time usage reads a real-time row.
    /// The other usages read the good-quality row of frames below 608 lines or from 608 lines.
    /// </summary>
    /// <param name="realtime">Whether the encoder runs in real-time usage.</param>
    /// <param name="large">Whether the shorter frame side has 608 lines or more.</param>
    /// <param name="curve">
    /// The curve. 0 and 1 are the low and high motion key frame curves, 2 and 3 are the low and high motion golden curves, and 4 is the inter curve.
    /// </param>
    /// <returns>The linear coefficient.</returns>
    private static double GetCurveCoefficient(bool realtime, bool large, int curve)
    {
        int row = ((realtime ? 1 : 0) * 2) + (large ? 1 : 0);
        return CurveCoefficients[(row * 5) + curve];
    }

    /// <summary>
    /// Interpolates between a low and a high motion quantizer floor by a boost. The interpolation is linear in the boost, with a rounding offset
    /// of half the gap.
    /// </summary>
    /// <param name="boost">The boost.</param>
    /// <param name="low">The boost at and below which the high motion floor applies.</param>
    /// <param name="high">The boost at and above which the low motion floor applies.</param>
    /// <param name="lowMotion">The low motion floor.</param>
    /// <param name="highMotion">The high motion floor.</param>
    /// <returns>The active quality.</returns>
    private static int GetActiveQuality(int boost, int low, int high, int lowMotion, int highMotion)
    {
        if (boost > high)
        {
            return lowMotion;
        }

        if (boost < low)
        {
            return highMotion;
        }

        int gap = high - low;
        int offset = high - boost;
        int difference = highMotion - lowMotion;
        return lowMotion + (((offset * difference) + (gap >> 1)) / gap);
    }

    /// <summary>
    /// Returns the real-time quantizer floor of a quantizer index.
    /// </summary>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The floor.</returns>
    private static int GetRealtimeMinimumQuality(int qIndex, Av1BitDepth bitDepth)
        => GetMinimumQIndex(ConvertQIndexToQ(qIndex, bitDepth), 0.00000271, -0.00113, 0.70, bitDepth);

    /// <summary>
    /// Returns the quantizer index of a third-order polynomial of the real quantizer. The polynomial value is limited to the real quantizer.
    /// </summary>
    /// <param name="maximumQ">The real quantizer.</param>
    /// <param name="x3">The cubic coefficient.</param>
    /// <param name="x2">The quadratic coefficient.</param>
    /// <param name="x1">The linear coefficient.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The quantizer index.</returns>
    private static int GetMinimumQIndex(double maximumQ, double x3, double x2, double x1, Av1BitDepth bitDepth)
    {
        double target = Math.Min(((((x3 * maximumQ) + x2) * maximumQ) + x1) * maximumQ, maximumQ);

        // The step from q 2.0 down to lossless has its own case.
        if (target <= 2.0)
        {
            return 0;
        }

        return FindQIndex(target, bitDepth, 0, Av1Constants.MaxQ);
    }

    /// <summary>
    /// Returns the quantizer index whose expected rate is closest to the frame target. In constant-bitrate coding, it then limits the change from
    /// the preceding frames.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="sourceSad">The scene statistics of this frame.</param>
    /// <param name="activeBestQuality">The lowest quantizer.</param>
    /// <param name="activeWorstQuality">The highest quantizer.</param>
    /// <param name="targetBitsPerMacroblock">The bit target per macroblock of the size being regulated.</param>
    /// <returns>The quantizer index.</returns>
    private int RegulateQuantizer(
        bool keyFrame,
        bool screenContent,
        in SourceSadStatistics sourceSad,
        int activeBestQuality,
        int activeWorstQuality,
        int targetBitsPerMacroblock)
    {
        double correctionFactor = this.GetRateCorrectionFactor(keyFrame);
        int q = this.FindClosestQIndexByRate(keyFrame, screenContent, targetBitsPerMacroblock, correctionFactor, activeBestQuality, activeWorstQuality);
        return this.mode == Av1RateControlMode.ConstantBitRate
            ? this.AdjustConstantBitrateQuantizer(keyFrame, screenContent, in sourceSad, q, activeWorstQuality)
            : q;
    }

    /// <summary>
    /// Returns the bit target of the current frame per macroblock, with <see cref="BitsPerMacroblockShift"/> fraction bits.
    /// </summary>
    /// <returns>The target bits per macroblock.</returns>
    private int GetTargetBitsPerMacroblock()
        => (int)(((ulong)this.thisFrameTarget << BitsPerMacroblockShift) / (ulong)this.macroblockCount);

    /// <summary>
    /// Returns the quantizer index whose expected bits per macroblock lie closest to the desired rate. The expected bits fall as the index rises,
    /// so a binary search finds the first index at or below the rate. The result is that index or the one before it.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="desiredBitsPerMacroblock">The desired bits per macroblock.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <param name="bestQIndex">The lowest quantizer index.</param>
    /// <param name="worstQIndex">The highest quantizer index.</param>
    /// <returns>The quantizer index.</returns>
    private int FindClosestQIndexByRate(
        bool keyFrame,
        bool screenContent,
        int desiredBitsPerMacroblock,
        double correctionFactor,
        int bestQIndex,
        int worstQIndex)
    {
        int low = bestQIndex;
        int high = worstQIndex;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (this.GetSearchBitsPerMacroblock(keyFrame, screenContent, middle, correctionFactor) > desiredBitsPerMacroblock)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int currentQ = low;
        int currentBits = this.GetSearchBitsPerMacroblock(keyFrame, screenContent, currentQ, correctionFactor);
        int currentDifference = currentBits <= desiredBitsPerMacroblock ? desiredBitsPerMacroblock - currentBits : int.MaxValue;
        int previousDifference;
        if (currentDifference == int.MaxValue || currentQ == bestQIndex)
        {
            previousDifference = int.MaxValue;
        }
        else
        {
            previousDifference = this.GetSearchBitsPerMacroblock(keyFrame, screenContent, currentQ - 1, correctionFactor) -
                desiredBitsPerMacroblock;
        }

        return currentDifference <= previousDifference ? currentQ : currentQ - 1;
    }

    /// <summary>
    /// Returns the expected bits per macroblock that the quantizer search compares. When cyclic refresh refreshes the frame, its expected boosted
    /// share codes at the lower quantizer of the first boosted segment.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <returns>The bits per macroblock.</returns>
    private int GetSearchBitsPerMacroblock(bool keyFrame, bool screenContent, int qIndex, double correctionFactor)
    {
        if (this.cyclicRefresh is not { Apply: true } cyclicRefresh)
        {
            return this.GetBitsPerMacroblock(keyFrame, screenContent, qIndex, correctionFactor, this.accurateBitEstimate);
        }

        double weight = cyclicRefresh.GetExpectedSegmentWeight(this.macroblockCount);
        int qIndexDelta = cyclicRefresh.GetExpectedQDelta(this, keyFrame, screenContent, qIndex);
        return (int)Math.Round(
            ((1.0 - weight) * this.GetBitsPerMacroblock(keyFrame, screenContent, qIndex, correctionFactor, this.accurateBitEstimate)) +
            (weight * this.GetBitsPerMacroblock(keyFrame, screenContent, qIndex + qIndexDelta, correctionFactor, this.accurateBitEstimate)),
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Returns the expected bits per macroblock at a quantizer. The estimate is the rate constant times the correction factor, divided by the real
    /// quantizer. An inter frame with the accurate estimate derives the rate constant from the reconstruction error.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <param name="accurateEstimate">Whether the reconstruction error can set the rate constant.</param>
    /// <returns>The bits per macroblock.</returns>
    private int GetBitsPerMacroblock(bool keyFrame, bool screenContent, int qIndex, double correctionFactor, bool accurateEstimate)
    {
        double q = ConvertQIndexToQ(qIndex, this.bitDepth);
        int enumerator = GetBitsPerMacroblockEnumerator(keyFrame, screenContent);
        if (!keyFrame && accurateEstimate && this.reconstructionError != ulong.MaxValue)
        {
            double errorRoot = (double)((int)Math.Sqrt(this.reconstructionError) << BitsPerMacroblockShift) / this.macroblockCount;
            int ratio = this.bitEstimateRatio == 0 ? (int)(300000 / errorRoot) : this.bitEstimateRatio;

            // A clamped rate constant limits quantizer fluctuations.
            enumerator = Av1Math.Clamp((int)(ratio * errorRoot), 20000, 170000);
        }

        return (int)(enumerator * correctionFactor / q);
    }

    /// <summary>
    /// Returns the rate constant of a frame type. Screen content uses half the constant of natural content.
    /// </summary>
    /// <param name="keyFrame">Whether the rate is of a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <returns>The rate constant.</returns>
    private static int GetBitsPerMacroblockEnumerator(bool keyFrame, bool screenContent)
        => screenContent ? keyFrame ? 1000000 : 750000 : keyFrame ? 2000000 : 1500000;

    /// <summary>
    /// Limits the quantizer change against the two preceding frames and the scene statistics. It also moves a frame larger than its primary
    /// reference toward the worst quality. Temporal layers, dynamic resizing and reference biasing do not apply.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="sourceSad">The scene statistics of this frame.</param>
    /// <param name="q">The quantizer index that the rate model chose.</param>
    /// <param name="activeWorstQuality">The highest quantizer of the rate model search.</param>
    /// <returns>The quantizer index.</returns>
    private int AdjustConstantBitrateQuantizer(bool keyFrame, bool screenContent, in SourceSadStatistics sourceSad, int q, int activeWorstQuality)
    {
        // A preceding overshoot with a low buffer relaxes the limits on the next increase.
        bool overshootBufferLow = this.firstFrameRateSign == -1 &&
            sourceSad.FrameSad > 1000 &&
            this.bufferLevel < (this.optimalBufferLevel >> 1) &&
            this.framesSinceKey > 4;

        int maximumDeltaUp = overshootBufferLow ? 120 : 20;
        bool bandwidthChanged = Math.Abs(this.averageFrameBandwidth - this.previousAverageFrameBandwidth) > 0.1 * this.averageFrameBandwidth;
        int maximumDeltaDown;
        if (this.cyclicRefresh is { Apply: true } cyclicRefresh)
        {
            // Static screen content limits the decrease until the next refresh cycle starts. It also links the increase to the decrease and the
            // buffer.
            maximumDeltaDown = screenContent && cyclicRefresh.CycleAdvanced
                ? Av1Math.Clamp(this.firstFrameQIndex / 32, 1, 8)
                : Av1Math.Clamp(this.firstFrameQIndex / 8, 1, 16);

            if (screenContent)
            {
                if (this.bufferLevel > this.optimalBufferLevel)
                {
                    maximumDeltaUp = Math.Max(4, maximumDeltaDown);
                }
                else if (!overshootBufferLow)
                {
                    maximumDeltaUp = Math.Max(8, maximumDeltaDown);
                }
            }
        }
        else
        {
            maximumDeltaDown = screenContent
                ? Av1Math.Clamp(this.firstFrameQIndex / 16, 1, 8)
                : Av1Math.Clamp(this.firstFrameQIndex / 8, 1, 16);
        }

        // A frame whose size differs from its primary reference, or whose bandwidth changed, targets a different rate per macroblock. Thus its
        // quantizer is not clamped to the preceding ones.
        Size? previous = this.primaryReferenceSize;
        bool targetBitsPerMacroblockChanged = previous is Size reference &&
            (this.width != reference.Width || this.height != reference.Height || bandwidthChanged);

        if (!keyFrame && this.framesSinceKey > 1 && this.firstFrameQIndex > 0 && this.secondFrameQIndex > 0 && !targetBitsPerMacroblockChanged)
        {
            // An overshoot and an undershoot in the two preceding frames clamp the quantizer between theirs.
            if (this.firstFrameRateSign * this.secondFrameRateSign == -1 &&
                this.firstFrameQIndex != this.secondFrameQIndex &&
                !overshootBufferLow)
            {
                int clamped = Av1Math.Clamp(
                    q,
                    Math.Min(this.firstFrameQIndex, this.secondFrameQIndex),
                    Math.Max(this.firstFrameQIndex, this.secondFrameQIndex));

                // After an overshoot, a larger increase is reduced less for a faster reaction.
                q = this.firstFrameRateSign == -1 && q > clamped && this.framesSinceKey > 10
                    ? (q + clamped) >> 1
                    : clamped;
            }

            // Falling content change pushes a high quantizer down while the buffer is stable. Rising content change slows a decrease while the
            // buffer is below its maximum. Only real-time usage detects scenes.
            if (this.realtime && sourceSad.PreviousAverageSad > 0 && this.framesSinceKey > 10 && sourceSad.FrameSad > 0)
            {
                double delta = ((double)sourceSad.AverageSad / sourceSad.PreviousAverageSad) - 1.0;
                if (delta < 0.0 && this.bufferLevel > (this.optimalBufferLevel >> 2) && q > (this.worstQuality >> 1))
                {
                    double adjustmentFactor = 1.0 + (0.5 * Math.Tanh(4.0 * delta));
                    double qValue = ConvertQIndexToQ(q, this.bitDepth);
                    q += this.ComputeQDelta(qValue, qValue * adjustmentFactor);
                }
                else if (this.firstFrameQIndex - q > 0 &&
                    delta > 0.1 &&
                    this.bufferLevel < Math.Min(this.maximumBufferSize, this.optimalBufferLevel << 1))
                {
                    q = ((3 * q) + this.firstFrameQIndex) >> 2;
                }
            }

            // Limit the decrease and the increase from the preceding frame.
            if (this.firstFrameQIndex - q > maximumDeltaDown)
            {
                q = this.firstFrameQIndex - maximumDeltaDown;
            }
            else if (q - this.firstFrameQIndex > maximumDeltaUp)
            {
                q = this.firstFrameQIndex + maximumDeltaUp;
            }
        }

        // A frame with more than 1.5 times the area of its primary reference moves halfway to the active worst quality to avoid an overshoot.
        if (previous is Size larger && (long)this.width * this.height > 1.5 * larger.Width * larger.Height)
        {
            q = (q + activeWorstQuality) >> 1;
        }

        return Av1Math.Clamp(q, this.bestQuality, this.worstQuality);
    }

    /// <summary>
    /// Raises the quantizer of a scene change toward the worst quality. It also resets the state that later frames read, so that they do not
    /// settle at a low quantizer and overshoot again. Spatial variance, layers and screen content tuning do not apply.
    /// </summary>
    /// <param name="q">The quantizer index of the frame.</param>
    /// <param name="averageSourceSad">The running average source SAD.</param>
    /// <returns>The raised quantizer index.</returns>
    public int ApplyOvershootQuantizer(int q, ulong averageSourceSad)
    {
        // A scene change restarts the count of frames since the last scene change in the cyclic refresh.
        if (this.cyclicRefresh is not null)
        {
            this.cyclicRefresh.SceneChangeFrameCount = 0;
        }

        // An easy scene change in a large frame with a stable buffer uses a lower quantizer.
        const ulong sadThreshold = 64 * 64 * 32;
        if (this.width * this.height >= 1280 * 720 &&
            this.bufferLevel > (this.optimalBufferLevel >> 1) &&
            averageSourceSad < sadThreshold)
        {
            q = (q + this.worstQuality) >> 1;
        }
        else
        {
            q = ((3 * this.worstQuality) + q) >> 2;
        }

        this.averageInterFrameQIndex = q;
        this.bufferLevel = this.optimalBufferLevel;
        this.bitsOffTarget = this.optimalBufferLevel;
        this.firstFrameRateSign = 0;
        this.secondFrameRateSign = 0;

        // Base the correction factor on the target rate at this quantizer, the inverse of the bits estimate. The estimate uses the key frame rate
        // constant for this inter factor. This is a compatibility choice that keeps the coded output the same as the standard AVIF encoder.
        int targetBitsPerMacroblock = (int)(((ulong)this.averageFrameBandwidth << BitsPerMacroblockShift) / (ulong)this.macroblockCount);
        double qValue = ConvertQIndexToQ(q, this.bitDepth);
        int enumerator = GetBitsPerMacroblockEnumerator(keyFrame: true, screenContent: false);
        double newFactor = targetBitsPerMacroblock * qValue / enumerator;
        if (newFactor > this.interFrameCorrectionFactor)
        {
            this.interFrameCorrectionFactor = Math.Min((newFactor + this.interFrameCorrectionFactor) / 2.0, MaximumBitsPerBlockFactor);
        }

        return q;
    }

    /// <summary>
    /// Updates the rate model, the quantizer averages and the buffer after a frame is coded.
    /// </summary>
    /// <param name="frameBytes">The coded size of the frame, without the temporal delimiter.</param>
    /// <param name="qIndex">The quantizer index of the frame.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="refreshesGolden">Whether the frame refreshes the GOLDEN reference.</param>
    /// <param name="constrainedGoldenGroup">Whether the golden group ends at the next key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="segmentationEnabled">Whether the frame codes segments.</param>
    /// <param name="sceneChange">Whether the frame is a scene change.</param>
    public void UpdateAfterFrame(
        int frameBytes,
        int qIndex,
        bool keyFrame,
        bool refreshesGolden,
        bool constrainedGoldenGroup,
        bool screenContent,
        bool segmentationEnabled,
        bool sceneChange)
    {
        // The post-encode update reads the refresh flags of the coded frame.
        this.refreshesGolden = refreshesGolden;
        int projectedFrameSize = frameBytes << 3;
        this.UpdateRateCorrectionFactors(projectedFrameSize, qIndex, keyFrame, screenContent, segmentationEnabled, sceneChange);

        if (this.mode == Av1RateControlMode.ConstantBitRate && !keyFrame && this.accurateBitEstimate)
        {
            double q = ConvertQIndexToQ(qIndex, this.bitDepth);
            int ratio = (int)(projectedFrameSize * q / Math.Sqrt(this.reconstructionError));
            this.bitEstimateRatio = this.bitEstimateRatio == 0 ? ratio : ((7 * this.bitEstimateRatio) + ratio) / 8;
        }

        if (keyFrame)
        {
            this.lastKeyFrameQIndex = qIndex;
            this.averageKeyFrameQIndex = ((3 * this.averageKeyFrameQIndex) + qIndex + 2) >> 2;
        }
        else if (!refreshesGolden)
        {
            this.lastInterFrameQIndex = qIndex;
            this.averageInterFrameQIndex = ((3 * this.averageInterFrameQIndex) + qIndex + 2) >> 2;
        }

        // Keep the last boosted quantizer, which a forced key frame reads. A golden refresh in a group that ends at the next key frame does not
        // count. A lower quantizer of any frame also replaces it.
        if (qIndex < this.lastBoostedQIndex || keyFrame || (refreshesGolden && !constrainedGoldenGroup))
        {
            this.lastBoostedQIndex = qIndex;
        }

        // A leaky bucket: each shown frame adds the average frame bandwidth and removes its own size.
        this.bitsOffTarget = Math.Min(this.bitsOffTarget + this.averageFrameBandwidth - projectedFrameSize, this.maximumBufferSize);
        this.bufferLevel = this.bitsOffTarget;
        this.previousAverageFrameBandwidth = this.averageFrameBandwidth;

        // The constrained-quality level reads the bits spent against the bits of the shown frames.
        this.totalActualBits += projectedFrameSize;
        this.totalTargetBits += this.averageFrameBandwidth;
        if (keyFrame)
        {
            this.framesSinceKey = 0;
        }
    }

    /// <summary>
    /// Advances the key frame counters after a shown frame. The counters stop when no frames are left before the next key frame.
    /// </summary>
    public void EndFrame()
    {
        if (this.framesToKey != 0)
        {
            this.framesSinceKey++;
            this.framesToKey--;
        }
    }

    /// <summary>
    /// Moves the rate correction factor of the frame type toward the ratio of the coded and the expected size. A damping limit keeps each step
    /// small.
    /// </summary>
    /// <param name="projectedFrameSize">The coded size in bits.</param>
    /// <param name="qIndex">The quantizer index of the frame.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="segmentationEnabled">Whether the frame codes segments.</param>
    /// <param name="sceneChange">Whether the frame is a scene change.</param>
    private void UpdateRateCorrectionFactors(
        int projectedFrameSize,
        int qIndex,
        bool keyFrame,
        bool screenContent,
        bool segmentationEnabled,
        bool sceneChange)
    {
        Av1CyclicRefresh? cyclicRefresh = segmentationEnabled ? this.cyclicRefresh : null;

        // The overshoot of a scene change already reset the factors, so only the quantizer history restarts. The count is zero exactly on that
        // frame. ApplyOvershootQuantizer cleared it, and a scene change refreshes nothing, so the cyclic refresh setup did not count the frame.
        // Only constant-bitrate coding detects the overshoot.
        if (this.mode == Av1RateControlMode.ConstantBitRate &&
            this.cyclicRefresh is { SceneChangeFrameCount: 0 } &&
            sceneChange &&
            !keyFrame)
        {
            this.secondFrameQIndex = qIndex;
            this.firstFrameQIndex = qIndex;
            this.secondFrameRateSign = 0;
            this.firstFrameRateSign = 0;
            return;
        }

        double rateCorrectionFactor = this.GetRateCorrectionFactor(keyFrame);
        double correctionFactor = 1.0;
        int projectedSizeBasedOnQ = cyclicRefresh is null
            ? this.EstimateBitsAtQ(keyFrame, screenContent, qIndex, rateCorrectionFactor)
            : this.EstimateCyclicRefreshBitsAtQ(cyclicRefresh, keyFrame, screenContent, qIndex, rateCorrectionFactor);

        if (projectedSizeBasedOnQ > FrameOverheadBits)
        {
            correctionFactor = (double)projectedFrameSize / projectedSizeBasedOnQ;
        }

        correctionFactor = Math.Max(correctionFactor, 0.25);
        this.secondFrameQIndex = this.firstFrameQIndex;
        this.firstFrameQIndex = qIndex;
        this.secondFrameRateSign = this.firstFrameRateSign;
        this.firstFrameRateSign = correctionFactor > 1.1 ? -1 : correctionFactor < 0.9 ? 1 : 0;

        // Dampen the adjustment. The limit grows with the log of the size ratio, from 0.25 up to 0.5 for screen content and up to 0.625 otherwise.
        double adjustmentLimit = screenContent
            ? 0.25 + (0.5 * Math.Min(0.5, Math.Abs(Math.Log10(correctionFactor))))
            : 0.25 + (0.75 * Math.Min(0.5, Math.Abs(Math.Log10(correctionFactor))));

        // An overshoot or undershoot moves the refresh amount and its quantizer change.
        if (cyclicRefresh is not null && this.thisFrameTarget > 0)
        {
            cyclicRefresh.AdjustForRate(correctionFactor);
        }

        if (correctionFactor > 1.01)
        {
            correctionFactor = 1.0 + ((correctionFactor - 1.0) * adjustmentLimit);
            rateCorrectionFactor = Math.Min(rateCorrectionFactor * correctionFactor, MaximumBitsPerBlockFactor);
        }
        else if (correctionFactor < 0.99)
        {
            correctionFactor = 1.0 / correctionFactor;
            correctionFactor = 1.0 + ((correctionFactor - 1.0) * adjustmentLimit);
            correctionFactor = 1.0 / correctionFactor;
            rateCorrectionFactor = Math.Max(rateCorrectionFactor * correctionFactor, MinimumBitsPerBlockFactor);
        }

        // The stored factor excludes the scale of a frame coded below the configured size.
        rateCorrectionFactor /= this.GetResizeRateFactor(new Size(this.width, this.height));
        rateCorrectionFactor = Math.Clamp(rateCorrectionFactor, MinimumBitsPerBlockFactor, MaximumBitsPerBlockFactor);
        if (keyFrame)
        {
            this.keyFrameCorrectionFactor = rateCorrectionFactor;
        }
        else if (this.UsesGoldenCorrectionFactor)
        {
            this.goldenCorrectionFactor = rateCorrectionFactor;
        }
        else
        {
            this.interFrameCorrectionFactor = rateCorrectionFactor;
        }
    }

    /// <summary>
    /// Returns the expected frame size at a quantizer. The result is at least <see cref="FrameOverheadBits"/>.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <returns>The expected size in bits.</returns>
    private int EstimateBitsAtQ(bool keyFrame, bool screenContent, int qIndex, double correctionFactor)
    {
        int bitsPerMacroblock = this.GetBitsPerMacroblock(keyFrame, screenContent, qIndex, correctionFactor, this.accurateBitEstimate);
        return Math.Max(FrameOverheadBits, (int)((ulong)bitsPerMacroblock * (ulong)this.macroblockCount) >> BitsPerMacroblockShift);
    }

    /// <summary>
    /// Returns the expected frame size at a quantizer with the quantizer changes of the boosted segments. Each segment has a weight equal to its
    /// share of the 4x4 units of the frame, which hold 16 units per macroblock.
    /// </summary>
    /// <param name="cyclicRefresh">The cyclic refresh of the sequence.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <returns>The expected size in bits.</returns>
    private int EstimateCyclicRefreshBitsAtQ(
        Av1CyclicRefresh cyclicRefresh,
        bool keyFrame,
        bool screenContent,
        int qIndex,
        double correctionFactor)
    {
        int unitCount = this.macroblockCount << 4;
        double firstWeight = (double)cyclicRefresh.FirstSegmentBlockCount / unitCount;
        double secondWeight = (double)cyclicRefresh.SecondSegmentBlockCount / unitCount;
        int firstQIndex = qIndex + cyclicRefresh.GetSegmentQDelta(Av1CyclicRefresh.FirstBoostSegment);
        int secondQIndex = qIndex + cyclicRefresh.GetSegmentQDelta(Av1CyclicRefresh.SecondBoostSegment);
        return (int)Math.Round(
            ((1.0 - firstWeight - secondWeight) * this.EstimateBitsAtQ(keyFrame, screenContent, qIndex, correctionFactor)) +
            (firstWeight * this.EstimateBitsAtQ(keyFrame, screenContent, firstQIndex, correctionFactor)) +
            (secondWeight * this.EstimateBitsAtQ(keyFrame, screenContent, secondQIndex, correctionFactor)),
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Returns the rate correction factor of the frame type, scaled up for a frame coded below the configured size.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <returns>The factor.</returns>
    private double GetRateCorrectionFactor(bool keyFrame)
    {
        double factor = keyFrame
            ? this.keyFrameCorrectionFactor
            : this.UsesGoldenCorrectionFactor ? this.goldenCorrectionFactor : this.interFrameCorrectionFactor;

        factor *= this.GetResizeRateFactor(new Size(this.width, this.height));
        return Math.Clamp(factor, MinimumBitsPerBlockFactor, MaximumBitsPerBlockFactor);
    }

    /// <summary>
    /// Returns the quantizer index change that scales the expected rate by a ratio, inside the allowed quantizer range.
    /// </summary>
    /// <param name="keyFrame">Whether the rate is of a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The base quantizer index.</param>
    /// <param name="rateTargetRatio">The rate ratio.</param>
    /// <returns>The quantizer index change.</returns>
    internal int GetQDeltaByRate(bool keyFrame, bool screenContent, int qIndex, double rateTargetRatio)
        => GetQDeltaByRate(keyFrame, screenContent, qIndex, rateTargetRatio, this.bitDepth, this.bestQuality, this.worstQuality);

    /// <summary>
    /// Returns the quantizer index change that scales the expected rate by a ratio. The estimate uses a correction factor of 1 and does not use
    /// the accurate estimate. A binary search finds the first index whose expected rate is at or below the scaled rate.
    /// </summary>
    /// <param name="keyFrame">Whether the rate is of a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The base quantizer index.</param>
    /// <param name="rateTargetRatio">The rate ratio.</param>
    /// <param name="bitDepth">The coded bit depth.</param>
    /// <param name="bestQIndex">The lowest allowed quantizer index.</param>
    /// <param name="worstQIndex">The highest allowed quantizer index.</param>
    /// <returns>The quantizer index change.</returns>
    public static int GetQDeltaByRate(
        bool keyFrame,
        bool screenContent,
        int qIndex,
        double rateTargetRatio,
        Av1BitDepth bitDepth,
        int bestQIndex,
        int worstQIndex)
    {
        int enumerator = GetBitsPerMacroblockEnumerator(keyFrame, screenContent);
        int baseBitsPerMacroblock = (int)(enumerator * 1.0 / ConvertQIndexToQ(qIndex, bitDepth));
        int targetBitsPerMacroblock = (int)(rateTargetRatio * baseBitsPerMacroblock);
        int low = bestQIndex;
        int high = worstQIndex;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if ((int)(enumerator * 1.0 / ConvertQIndexToQ(middle, bitDepth)) > targetBitsPerMacroblock)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low - qIndex;
    }

    /// <summary>
    /// Returns the target rate of a frame per 64x64 area. The rate is the frame target times 4096 samples, divided by the frame area.
    /// </summary>
    /// <param name="frameTarget">The target size of the frame in bits.</param>
    /// <param name="width">The frame width in samples.</param>
    /// <param name="height">The frame height in samples.</param>
    /// <returns>The target rate per 64x64 area.</returns>
    public static int GetSuperblockTargetRate(int frameTarget, int width, int height)
        => (int)Math.Min(((long)frameTarget << 12) / ((long)width * height), int.MaxValue);

    /// <summary>
    /// Returns the quantizer index change between two real quantizers inside the allowed range.
    /// </summary>
    /// <param name="qStart">The starting real quantizer.</param>
    /// <param name="qTarget">The target real quantizer.</param>
    /// <returns>The quantizer index change.</returns>
    private int ComputeQDelta(double qStart, double qTarget)
        => FindQIndex(qTarget, this.bitDepth, this.bestQuality, this.worstQuality) -
            FindQIndex(qStart, this.bitDepth, this.bestQuality, this.worstQuality);

    /// <summary>
    /// Returns the first quantizer index whose real quantizer reaches a value. The real quantizer rises with the index, so a binary search finds
    /// the index. The result is <paramref name="worstQIndex"/> when no index reaches the value.
    /// </summary>
    /// <param name="desiredQ">The real quantizer.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="bestQIndex">The lowest quantizer index.</param>
    /// <param name="worstQIndex">The highest quantizer index.</param>
    /// <returns>The quantizer index.</returns>
    private static int FindQIndex(double desiredQ, Av1BitDepth bitDepth, int bestQIndex, int worstQIndex)
    {
        int low = bestQIndex;
        int high = worstQIndex;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (ConvertQIndexToQ(middle, bitDepth) < desiredQ)
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
    /// Converts a quantizer index to the real quantizer on the 8-bit scale. The real quantizer is the AC quantizer step divided by 4 at 8
    /// bits, by 16 at 10 bits and by 64 at 12 bits.
    /// </summary>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The real quantizer.</returns>
    internal static double ConvertQIndexToQ(int qIndex, Av1BitDepth bitDepth)
    {
        int acQuantizer = Av1InverseTransformMath.GetAcQuantization(qIndex, 0, bitDepth);
        return bitDepth switch
        {
            Av1BitDepth.EightBit => acQuantizer / 4.0,
            Av1BitDepth.TenBit => acQuantizer / 16.0,
            _ => acQuantizer / 64.0
        };
    }

    /// <summary>
    /// Returns the number of 16x16 macroblocks of a frame. Each dimension rounds up to whole macroblocks.
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <returns>The macroblock count.</returns>
    private static int GetMacroblockCount(int width, int height)
    {
        int modeInfoColumns = Av1Math.AlignPowerOf2(width, 3) >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoRows = Av1Math.AlignPowerOf2(height, 3) >> Av1Constants.ModeInfoSizeLog2;
        return ((modeInfoRows + 2) >> 2) * ((modeInfoColumns + 2) >> 2);
    }

    /// <summary>
    /// The source change statistics of a frame that the quantizer limits read.
    /// </summary>
    internal readonly struct SourceSadStatistics
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SourceSadStatistics"/> struct.
        /// </summary>
        /// <param name="frameSad">The average 64x64 SAD of the frame.</param>
        /// <param name="averageSad">The running average after this frame.</param>
        /// <param name="previousAverageSad">The running average before this frame.</param>
        public SourceSadStatistics(ulong frameSad, ulong averageSad, ulong previousAverageSad)
        {
            this.FrameSad = frameSad;
            this.AverageSad = averageSad;
            this.PreviousAverageSad = previousAverageSad;
        }

        /// <summary>
        /// Gets the average 64x64 SAD of the frame.
        /// </summary>
        public ulong FrameSad { get; }

        /// <summary>
        /// Gets the running average after this frame.
        /// </summary>
        public ulong AverageSad { get; }

        /// <summary>
        /// Gets the running average before this frame.
        /// </summary>
        public ulong PreviousAverageSad { get; }
    }
}
