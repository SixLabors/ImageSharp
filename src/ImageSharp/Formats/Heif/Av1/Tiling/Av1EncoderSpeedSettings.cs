// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Resolves the coding-tool search settings used by one encoded picture.
/// </summary>
internal readonly struct Av1EncoderSpeedSettings
{
    private readonly int chromaModeRestriction;
    private readonly int qIndex;
    private readonly int minimumDimension;
    private readonly bool realtime;
    private readonly bool allIntra;
    private readonly bool screenContent;
    private readonly bool intraFrame;
    private readonly InlineArray5<float> partitionBreakoutThresholds;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderSpeedSettings"/> struct.
    /// </summary>
    /// <param name="speed">The encoding speed tier.</param>
    /// <param name="allIntra">Whether the sequence uses the all-intra profile.</param>
    /// <param name="intraFrame">Whether the current picture is intra-only.</param>
    /// <param name="updateType">
    /// The role of the picture in its golden-frame group.
    /// </param>
    /// <param name="qIndex">The current base quantizer index.</param>
    /// <param name="frameSize">The visible frame dimensions.</param>
    /// <param name="screenContent">Whether the frame allows the screen content tools.</param>
    /// <param name="frameSizeScreenContent">
    /// Whether the frame allowed the screen content tools when the frame-size speed features were set, or
    /// <see langword="null"/> when it is <paramref name="screenContent"/>. A key frame whose screen content trial turns
    /// the tools on keeps its detected value here.
    /// </param>
    /// <param name="sharpness">The encoder sharpness, 0 to 7.</param>
    /// <param name="tuning">The tune metric.</param>
    public Av1EncoderSpeedSettings(
        HeifEncodingSpeed speed,
        bool allIntra,
        bool intraFrame,
        Av1FrameUpdateType updateType,
        int qIndex,
        Size frameSize,
        bool screenContent = false,
        bool? frameSizeScreenContent = null,
        int sharpness = 0,
        Av1Tuning tuning = Av1Tuning.Psnr)
    {
        // The image and SSIMULACRA 2 tunes search intra modes more thoroughly in inter frames, because a layered image
        // can code its key frame at a lower quality than its inter frames.
        bool imageTuning = tuning.IsImageTuning();

        this.Speed = speed;
        this.qIndex = qIndex;
        this.allIntra = allIntra;
        this.screenContent = screenContent;
        this.FrameSizeScreenContentTools = frameSizeScreenContent ?? screenContent;
        this.intraFrame = intraFrame;
        this.minimumDimension = Math.Min(frameSize.Width, frameSize.Height);

        // Good-quality usage derives many settings from the role of the frame in its group. A boosted frame is an intra-only, golden or
        // alternate-reference update. The encoder codes it at a higher quality than the frames around it. The second class adds the
        // internal alternate-reference updates. Only the ordinary displayed frames of a group are last-frame updates.
        bool boosted = intraFrame || updateType is Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        bool boostedOrInternalAlternate = boosted || updateType == Av1FrameUpdateType.IntermediateAlternate;
        bool lastFrameUpdate = updateType == Av1FrameUpdateType.Last;
        this.IsBoosted = boosted;

        // Good-quality usage disables dual interpolation filters at every speed. All-intra pictures do not write inter filters. The
        // sequence flag stays off, so the header does not signal an unused coding tool.
        this.EnableDualFilter = false;

        // Real-time usage never enables loop restoration. All-intra usage enables it below speed 5.
        this.EnableRestoration = (allIntra || speed < HeifEncodingSpeed.Level7) &&
            (!allIntra || speed < HeifEncodingSpeed.Level5);

        // Good-quality usage keeps distance-weighted compound at speed 0 only. Real-time usage disables it.
        this.UseDistanceWeightedCompound = !allIntra && speed == HeifEncodingSpeed.Level0;
        this.AllowHighPrecisionMotionVector = !intraFrame && qIndex < 128;
        this.UseVarianceBasedPartition = speed >= HeifEncodingSpeed.Level7;
        int minimumDimension = this.minimumDimension;
        this.MinimumPartitionSize = minimumDimension >= 2160 ||
            (speed >= HeifEncodingSpeed.Level6 && minimumDimension >= 1080) ||
            speed >= HeifEncodingSpeed.Level7
                ? Av1BlockSize.Block8x8
                : Av1BlockSize.Block4x4;

        // Fast still-image search caps its leaves at 32 samples. Larger roots must split before mode evaluation. Slower still-image
        // searches and sequence searches keep the full size range.
        this.MaximumPartitionSize = allIntra && speed >= HeifEncodingSpeed.Level6
            ? Av1BlockSize.Block32x32
            : Av1BlockSize.Block128x128;

        this.PaletteSearchLevel = speed == HeifEncodingSpeed.Level0 ? 0 : speed < HeifEncodingSpeed.Level3 ? 1 : 2;
        this.LumaPaletteHeaderPruneLevel = allIntra ? speed == HeifEncodingSpeed.Level0 ? 1 : 2 : 0;
        this.EarlyTerminateChromaPaletteSearch = allIntra || speed >= HeifEncodingSpeed.Level6;

        // Search depth counts splits below the largest transform. Square blocks usually test one split. At speed 0, rectangular blocks
        // test two splits, except in a frame of 720p or larger with a quantizer index of 128 or less.
        this.IntraSquareTransformSearchDepth = !allIntra && speed >= HeifEncodingSpeed.Level7 ? 0 : 1;
        this.IntraRectangularTransformSearchDepth = speed == HeifEncodingSpeed.Level0 &&
            !(minimumDimension >= 720 && qIndex <= 128) ? 2 : 1;

        this.UseIntraTransformRdBreakout = allIntra && speed >= HeifEncodingSpeed.Level3;

        bool realtime = !allIntra && speed >= HeifEncodingSpeed.Level7;
        this.realtime = realtime;
        this.AdaptiveModeThresholdLevel = allIntra ? 0 : realtime ? 4 : speed >= HeifEncodingSpeed.Level2 ? 1 : 0;
        this.PruneSkippableInterModes = !allIntra && (realtime || speed >= HeifEncodingSpeed.Level5);

        // Intra pruning in inter frames: good quality uses level 2 in an inter frame from speed 2. From speed 3, a boosted frame keeps
        // level 1 and the other frames use level 2 or 3 by resolution. The image tune turns the pruning off.
        this.IntraInInterPruningLevel = allIntra || imageTuning ? 0 : realtime ? 5 : speed >= HeifEncodingSpeed.Level4 ? 4 :
            speed >= HeifEncodingSpeed.Level3 ? boosted ? 1 : minimumDimension >= 720 ? 2 : 3 :
            speed >= HeifEncodingSpeed.Level2 && !intraFrame ? 2 : 1;

        this.MaximumIntraBlockSize = realtime || (!allIntra && speed >= HeifEncodingSpeed.Level3 && minimumDimension < 720)
            ? Av1BlockSize.Block32x32 : Av1BlockSize.Block128x128;

        this.IntraModeMotionRangePruneLevel = !realtime ? 0 : minimumDimension < 360 ? 1 : 2;
        this.UseHighResolutionPartitionBreakout = minimumDimension >= 720 &&
            speed is >= HeifEncodingSpeed.Level1 and < HeifEncodingSpeed.Level3;

        this.PartitionBreakoutHighBitDepthLevel = speed >= HeifEncodingSpeed.Level4 ? 3 : speed >= HeifEncodingSpeed.Level1 ? 2 : 1;
        this.EnablePartitionBreakoutModel = !allIntra && !realtime;
        this.FourStripPartitionMinimumScale = realtime ? 0 : speed >= HeifEncodingSpeed.Level6 ? 3 : 2;
        this.ExtendedPartitionPruningLevel = realtime ? 2 : 1;
        this.ChildPartitionPruningLevel = realtime ? 0 : speed >= HeifEncodingSpeed.Level4 ? 2 : speed >= HeifEncodingSpeed.Level3 ? 1 : 0;
        this.FourStripPartitionModelLevel = realtime ? 0 : Math.Min((int)speed, 3);
        this.SimpleMotionStepReduction = !realtime && speed >= HeifEncodingSpeed.Level4 ? 4 : 0;
        this.AfterSplitTerminationLevel = allIntra || realtime || speed >= HeifEncodingSpeed.Level3
            ? 0 : speed >= HeifEncodingSpeed.Level1 ? 2 : minimumDimension < 720 ? 1 : 0;

        this.FourStripPartitionResolutionIndex = minimumDimension >= 720 ? 2 : minimumDimension >= 480 ? 1 : 0;
        this.MaximumPartitionPredictionMode = realtime || (speed >= HeifEncodingSpeed.Level5 && minimumDimension >= 720)
            ? MaximumPartitionPrediction.Disabled
            : minimumDimension < 480 || (speed >= HeifEncodingSpeed.Level6 && minimumDimension < 720)
                ? MaximumPartitionPrediction.Direct
                : minimumDimension >= 720 ? MaximumPartitionPrediction.Adaptive : MaximumPartitionPrediction.Relaxed;

        this.EnableRectanglePartitionModel = !allIntra &&
            (realtime || speed >= HeifEncodingSpeed.Level3 || (speed == HeifEncodingSpeed.Level0 && minimumDimension >= 720));

        this.SquareOnlyPartitionThreshold = realtime ? Av1BlockSize.Block128x128 : speed >= HeifEncodingSpeed.Level6
            ? !allIntra && minimumDimension >= 720 ? Av1BlockSize.Block32x32 : Av1BlockSize.Block16x16
            : speed >= HeifEncodingSpeed.Level2
                ? minimumDimension >= 720 ? Av1BlockSize.Block64x64 : Av1BlockSize.Block32x32
                : speed >= HeifEncodingSpeed.Level1
                    ? minimumDimension >= 720 ? Av1BlockSize.Block128x128 :
                        minimumDimension >= 480 ? Av1BlockSize.Block64x64 : Av1BlockSize.Block32x32
                    : minimumDimension >= 480 ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;

        // Entries run from 128x128 down to 8x8. A negative threshold disables the model at that size. Frames of 720p or larger use the
        // model only at speeds 1 and 2.
        this.partitionBreakoutThresholds[0] = -1;
        this.partitionBreakoutThresholds[1] = -1;
        this.partitionBreakoutThresholds[2] = -1;
        this.partitionBreakoutThresholds[3] = -1;
        this.partitionBreakoutThresholds[4] = -1;
        if (minimumDimension < 720)
        {
            this.partitionBreakoutThresholds[1] = speed == HeifEncodingSpeed.Level0 ? 0.993307F : 0.952574F;
            this.partitionBreakoutThresholds[2] = 0.952574F;
            this.partitionBreakoutThresholds[3] = 0.924142F;
            this.partitionBreakoutThresholds[4] = 0.880797F;
        }
        else if (this.UseHighResolutionPartitionBreakout)
        {
            this.partitionBreakoutThresholds[0] = 0.5F;
            this.partitionBreakoutThresholds[1] = 0.5042595622791082F;
            this.partitionBreakoutThresholds[2] = 0.5F;
            this.partitionBreakoutThresholds[3] = 0.8378425823517456F;
            this.partitionBreakoutThresholds[4] = 0.8047585616503903F;
        }

        this.TransformTypeProbabilityPruning = realtime || minimumDimension < 480 || speed < HeifEncodingSpeed.Level2
            ? 0 : speed >= HeifEncodingSpeed.Level4 ? 2 : 1;

        // The inter transform type threshold: good quality from speed 6 below 720p keeps the higher threshold in boosted and internal
        // alternate-reference frames.
        this.InterTransformTypeProbabilityThreshold = realtime ? 0 :
            !allIntra && speed >= HeifEncodingSpeed.Level6 && minimumDimension < 720
                ? boostedOrInternalAlternate ? 450 : 150
                : int.MaxValue;

        this.InterWinnerPruningLevel = realtime && !intraFrame ? minimumDimension < 360 ? 2 : 3 : 0;
        this.InterpolationReuseLevel = allIntra ? 0 : realtime ? 1 : speed >= HeifEncodingSpeed.Level4 ? 2 :
            speed >= HeifEncodingSpeed.Level1 ? 1 : 0;
        this.InterpolationPruningLevel = allIntra ? 0 : realtime ? 1 : speed >= HeifEncodingSpeed.Level3 ? 2 :
            speed >= HeifEncodingSpeed.Level2 ? 1 : 0;
        this.ModelBasedInterpolationBreakout = !allIntra;
        this.PruneNearMotionByTranslation = !allIntra && !realtime;

        // Inter reference reduction: good quality uses level 1 in a boosted frame. The other frames use level 2 at speed 0 and level 3
        // from speed 1.
        this.ReduceInterReferenceIndices = allIntra ? 0 : realtime ? intraFrame ? 1 : 3 :
            boosted ? 1 : speed >= HeifEncodingSpeed.Level1 ? 3 : 2;

        // Good quality prunes spatial motion vectors by weight in a frame that is not boosted, from speed 5 below 720p and from speed 6
        // at every size. The code that reads the flag applies the screen-content condition.
        this.PruneSpatialMotionByWeight = !allIntra && !realtime && !boosted &&
            (speed >= HeifEncodingSpeed.Level6 || (speed >= HeifEncodingSpeed.Level5 && minimumDimension < 720));

        this.NearNeighborPruningLevel = allIntra ? 0 : realtime || speed >= HeifEncodingSpeed.Level6 ? 3 :
            speed >= HeifEncodingSpeed.Level5 ? minimumDimension <= 480 ? 1 : 2 : 0;

        // Good quality skips the single-reference interpolation search from speed 5 in a frame that is not boosted.
        this.SkipSingleInterpolationSearch = !allIntra && !realtime && !boosted && speed >= HeifEncodingSpeed.Level5;
        this.UseWinnerInterpolation = realtime;
        this.UseSimpleInterpolationModel = realtime;
        this.WinnerInterpolationUsesSharp = minimumDimension <= 240;

        // Compound pruning by single-reference results: good quality prunes less in a boosted frame.
        this.CompoundSingleResultPruningLevel = allIntra ? 0 : realtime ? 2 :
            speed >= HeifEncodingSpeed.Level3 ? boosted ? 4 : 2 :
            speed >= HeifEncodingSpeed.Level2 ? boosted ? 4 : 1 :
            speed >= HeifEncodingSpeed.Level1 ? boosted ? 2 : 1 : 0;

        this.SkipMixedNearCompound = !allIntra && !realtime &&
            (speed >= HeifEncodingSpeed.Level3 || (speed >= HeifEncodingSpeed.Level2 && minimumDimension <= 480));

        this.CompoundNeighborPruningLevel = allIntra || realtime ? 0 : speed >= HeifEncodingSpeed.Level6 ? 3 :
            speed >= HeifEncodingSpeed.Level4 ? 2 : speed >= HeifEncodingSpeed.Level2 ? 1 : 0;
        this.PruneMixedCompoundBySingleWinner = !allIntra && !realtime && speed >= HeifEncodingSpeed.Level1;
        this.PruneCompoundUsingSingleReference = !allIntra && !realtime;
        this.CompoundReferenceIndexPruningLevel = !allIntra && !realtime && speed >= HeifEncodingSpeed.Level6
            ? minimumDimension >= 720 ? 2 : 1 : 0;

        this.SkipInterpolationChromaModel = !allIntra && !realtime && speed >= HeifEncodingSpeed.Level1;
        this.SkipSharpInterpolationAfterSmooth = !allIntra && !realtime && speed >= HeifEncodingSpeed.Level4;

        // Good quality prefers the sharp interpolation filter below speed 4 in a frame that is not boosted.
        this.PreferSharpInterpolation = !allIntra && !realtime && !boosted && speed < HeifEncodingSpeed.Level4;
        this.UseNeighborInterpolation = !allIntra && (!realtime ? speed >= HeifEncodingSpeed.Level4 :
            speed >= HeifEncodingSpeed.Level9 && minimumDimension is >= 360 and < 1080);
        this.CompoundMotionSearchLevel = realtime ? 0 : speed >= HeifEncodingSpeed.Level5 ? 2 : 1;
        this.UseLocalJointMotionSearch = !realtime;
        this.ReuseCompoundTypeDecision = !realtime && speed >= HeifEncodingSpeed.Level3;

        // Good quality reuses compound mask results from speed 2. At speed 1, it reuses them up to a base quantizer index of 200, in a
        // frame that is not boosted and not an internal alternate reference.
        this.ReuseCompoundMaskResults = !realtime && (speed >= HeifEncodingSpeed.Level2 ||
            (speed == HeifEncodingSpeed.Level1 && qIndex <= 200 && !boostedOrInternalAlternate));

        this.UseCompoundWedgeModel = !realtime && speed >= HeifEncodingSpeed.Level1;
        this.FastWedgeSignEstimation = realtime;

        // Good quality prunes compound types by the model cost from speed 1 in a frame that is not boosted.
        this.PruneCompoundTypeByModel = !realtime && speed >= HeifEncodingSpeed.Level1 && !boosted;
        this.CompoundTypePruningLevel = realtime || speed >= HeifEncodingSpeed.Level2 ? 2 :
            speed >= HeifEncodingSpeed.Level1 ? 1 : 0;

        this.InterInterWedgeVarianceThreshold = realtime ? 100 : speed >= HeifEncodingSpeed.Level4 ? int.MaxValue :
            speed >= HeifEncodingSpeed.Level3 ? minimumDimension >= 720 ? 100 : int.MaxValue :
            speed >= HeifEncodingSpeed.Level2 ? 100 : 0;

        // Wedge motion refinement: good quality at speed 2 below 720p refines only in boosted and internal alternate-reference frames.
        // From speed 3, it refines only in boosted frames.
        this.RefineWedgeMotion = realtime || speed < HeifEncodingSpeed.Level2 ||
            (speed == HeifEncodingSpeed.Level2 ? minimumDimension >= 720 || boostedOrInternalAlternate : boosted);

        this.FastWedgeMaskSearch = !realtime &&
            (speed >= HeifEncodingSpeed.Level3 || (speed == HeifEncodingSpeed.Level2 && minimumDimension < 720));

        this.SkipWedgeForSimilarPredictors = !realtime && speed >= HeifEncodingSpeed.Level3 && minimumDimension >= 720;
        this.CompoundAverageCandidateCount = realtime || speed == HeifEncodingSpeed.Level0 ? 0 :
            speed >= HeifEncodingSpeed.Level2 ? 2 : minimumDimension <= 480 ? 5 : 4;

        this.ReuseInterIntraMode = speed >= HeifEncodingSpeed.Level1;
        this.FastInterIntraWedgeSearch = speed >= HeifEncodingSpeed.Level2;
        this.InterIntraWedgeVarianceThreshold = realtime || speed >= HeifEncodingSpeed.Level3 ? int.MaxValue :
            speed == HeifEncodingSpeed.Level2 ? minimumDimension >= 480 ? 100 : int.MaxValue : 0;

        // Good quality estimates inter residuals with the curve-fitted model, unless sharpness or the image tune is set.
        this.InterModeEstimation = allIntra || intraFrame ? 0 : realtime ? 2 : sharpness != 0 || imageTuning ? 0 : 1;

        // Good quality limits the inter mode candidates only in last-frame updates. The repeat threshold and the transform breakout
        // apply only to frames that are not boosted.
        this.InterModeCandidateLimit = realtime ? 2 : !lastFrameUpdate ? int.MaxValue :
            speed >= HeifEncodingSpeed.Level3 ? 6 :
            speed >= HeifEncodingSpeed.Level2 ? minimumDimension >= 720 ? 10 : 9 : int.MaxValue;

        this.InterModeRepeatThreshold = realtime ? 0 : boosted ? int.MaxValue :
            speed >= HeifEncodingSpeed.Level4 ? 3 :
            speed >= HeifEncodingSpeed.Level3 ? minimumDimension >= 720 ? 4 : 3 : int.MaxValue;

        this.InterModeTransformBreakout = !realtime && !boosted && speed >= HeifEncodingSpeed.Level2
            ? speed >= HeifEncodingSpeed.Level3 ? 2 : 1
            : 0;

        this.MaximumInterTransformCandidates = realtime ? 5 : int.MaxValue;
        this.InterTransformGateLevel = allIntra || intraFrame ? 0 :
            realtime ? 4 : speed >= HeifEncodingSpeed.Level3 ? 2 : speed >= HeifEncodingSpeed.Level2 ? 1 : 0;
        this.PruneRectangularPartitionsUsingIntraMode = allIntra && speed >= HeifEncodingSpeed.Level6;

        // Good quality skips rectangular partitions after a skippable NONE partition from speed 3, in a last-frame update only.
        this.SkippablePartitionPruningLevel = !allIntra && !realtime && lastFrameUpdate && speed >= HeifEncodingSpeed.Level3
            ? minimumDimension >= 720 ? 2 : 1
            : 0;

        this.TerminatePartitionSearchAfterInvalidNoneAndSplit = realtime || speed >= HeifEncodingSpeed.Level4 ||
            (!allIntra && speed >= HeifEncodingSpeed.Level3 && minimumDimension >= 480);

        // The quantizer rule replaces the speed ladder in every usage except real-time. Speed 3 relaxes the check from a base quantizer
        // index of 170, and speeds up to 2 keep the first level.
        this.RectangularPartitionPruningLevel = realtime || speed >= HeifEncodingSpeed.Level4
            ? 2
            : speed == HeifEncodingSpeed.Level3 ? qIndex >= 170 ? 1 : 2 : 1;

        int partitionDistortionThreshold = 0;
        int partitionRateThreshold = 0;
        if (realtime)
        {
            partitionDistortionThreshold = 1 << 25;
            partitionRateThreshold = 500;
        }
        else if (!allIntra && speed >= HeifEncodingSpeed.Level2)
        {
            bool largeFrame = minimumDimension >= 720;
            partitionDistortionThreshold = 1 << (largeFrame ? 24 : 22);
            partitionRateThreshold = largeFrame ? 120 : 100;
            if (speed >= HeifEncodingSpeed.Level3)
            {
                partitionDistortionThreshold <<= 1;
                partitionRateThreshold = largeFrame ? 200 : 120;
            }

            if (speed >= HeifEncodingSpeed.Level4)
            {
                partitionDistortionThreshold <<= 1;
            }

            if (speed >= HeifEncodingSpeed.Level5 && minimumDimension < 480)
            {
                partitionDistortionThreshold = 1 << 26;
            }

            if (speed >= HeifEncodingSpeed.Level6)
            {
                partitionDistortionThreshold = 1 << (largeFrame ? 28 : 26);
            }
        }

        this.PartitionBreakoutDistortionThreshold = partitionDistortionThreshold;
        this.PartitionBreakoutRateThreshold = partitionRateThreshold;

        // Good quality with the image tune caps both gradient pruning levels at 3.
        bool goodQualityImageTuning = imageTuning && !allIntra && !realtime;
        this.IntraHogPruningLevel = realtime
            ? speed >= HeifEncodingSpeed.Level8 ? 1 : 0
            : speed >= HeifEncodingSpeed.Level6 && !goodQualityImageTuning ? 4 : speed >= HeifEncodingSpeed.Level3 ? 3 : speed >= HeifEncodingSpeed.Level2 ? 2 : 1;

        this.IntraModelCandidateCount = realtime ? 4 : allIntra
            ? speed >= HeifEncodingSpeed.Level6 ? 2 : speed >= HeifEncodingSpeed.Level1 ? 3 : 4
            : speed >= HeifEncodingSpeed.Level3 ? 2 : 4;

        this.PruneChromaModesUsingLumaWinner = allIntra && speed >= HeifEncodingSpeed.Level4;
        this.ChromaHogPruningLevel = realtime || this.PruneChromaModesUsingLumaWinner || speed < HeifEncodingSpeed.Level3
            ? 0
            : speed >= HeifEncodingSpeed.Level6 && !goodQualityImageTuning ? 4 : speed >= HeifEncodingSpeed.Level5 ? 3 : 2;

        this.PruneChromaSmoothByVariance = allIntra && speed >= HeifEncodingSpeed.Level6;
        this.ChromaFromLumaSearchRange = this.PruneChromaSmoothByVariance ? 1 : 3;
        this.chromaModeRestriction = allIntra ? 0 : realtime ? 3 : speed >= HeifEncodingSpeed.Level6 ? 2 : speed >= HeifEncodingSpeed.Level4 ? 1 : 0;
        this.DisableSmoothIntra = realtime || speed >= (allIntra ? HeifEncodingSpeed.Level2 : HeifEncodingSpeed.Level5);
        this.RestrictLargeIntraBlocksToDc = !allIntra && !realtime && speed >= HeifEncodingSpeed.Level6;
        this.AdaptIntraModelCountToNeighbors = allIntra && speed >= HeifEncodingSpeed.Level6;
        this.PruneOddIntraAngleDeltas = allIntra && speed >= HeifEncodingSpeed.Level6;

        // Adaptive transform search level: all-intra keeps level 2 from speed 1. Good quality raises frames that are not boosted to
        // level 3 from speed 3, and turns the level off for sharpness 3.
        bool goodQualitySharpness3 = !allIntra && !realtime && sharpness == 3;
        this.InterAdaptiveTransformSearchLevel = realtime ? 2
            : goodQualitySharpness3 ? 0
            : speed >= HeifEncodingSpeed.Level3 && !boosted ? 3
            : speed >= HeifEncodingSpeed.Level1 ? 2 : 1;

        this.UseLumaCostForChromaBound = !allIntra && !realtime && speed >= HeifEncodingSpeed.Level3;

        // Good quality uses the block residual hash from speed 2.
        this.UseMacroblockRateDistortionHash = !allIntra && !realtime && speed >= HeifEncodingSpeed.Level2;
        this.InterTransformNoSplitCandidateCount = allIntra || realtime || speed == HeifEncodingSpeed.Level0
            ? 0
            : speed == HeifEncodingSpeed.Level1 ? 4 : 3;

        this.InterTransformSearchInitialDepth = speed == HeifEncodingSpeed.Level0 && !(minimumDimension >= 720 && qIndex <= 128) ? 0 : 1;
        this.InterTransformSplitThreshold = speed == HeifEncodingSpeed.Level0 && !(minimumDimension >= 1080 && qIndex <= 108) ? 8500 : 4000;
        int defaultInterTypePruning = realtime || speed >= HeifEncodingSpeed.Level3
            ? 3
            : speed >= HeifEncodingSpeed.Level1 || (minimumDimension >= 1080 && qIndex <= 108) ? 2 : 1;

        // Winner transform type pruning: good quality at speed 2 below 480p prunes in a frame that is not boosted.
        int winnerTypePruning = realtime || speed >= HeifEncodingSpeed.Level6 ? 4
            : speed >= HeifEncodingSpeed.Level4 ? 2
            : speed >= HeifEncodingSpeed.Level3 || (speed >= HeifEncodingSpeed.Level2 && !boosted && minimumDimension < 480) ? 1 : 0;

        // Good quality at speed 5 prunes more strongly in an inter frame without screen content. This applies at any quantizer below
        // 480p, and at a quantizer index below 128 from 480p.
        if (!realtime && !allIntra && speed == HeifEncodingSpeed.Level5 && !intraFrame && !screenContent &&
            qIndex < (minimumDimension >= 480 ? 128 : 256))
        {
            winnerTypePruning = 3;
        }

        this.DefaultInterTransformTypePruning = defaultInterTypePruning;
        this.CandidateInterTransformTypePruning = winnerTypePruning switch
        {
            1 => 3,
            2 => 4,
            3 or 4 => 5,
            _ => defaultInterTypePruning
        };

        this.WinnerInterTransformTypePruning = winnerTypePruning switch
        {
            1 or 2 => 0,
            3 => 2,
            4 => 3,
            _ => defaultInterTypePruning
        };

        // Good quality with sharpness 3 never predicts an empty residual.
        this.SkipFlagPredictionLevel = goodQualitySharpness3 ? 0 : realtime || speed >= HeifEncodingSpeed.Level3 ? 2 : 1;
        this.InterTransformSizePruningLevel = realtime ? 0 : speed >= HeifEncodingSpeed.Level3
            ? 3
            : speed >= HeifEncodingSpeed.Level2 ? minimumDimension >= 480 ? 2 : 3 : minimumDimension < 480 ? 1 : 0;

        this.EnableCoefficientOptimization = !realtime;

        // The loop filter pick method: the full-image search is the default. All-intra and good quality drop the separate direction
        // searches from speed 4. All-intra derives the levels from the quantizer from speed 6, and real-time usage always does.
        this.LoopFilterPickMethod = realtime || (allIntra && speed >= HeifEncodingSpeed.Level6)
            ? Av1LoopFilterPickMethod.FromQuantizer
            : speed >= HeifEncodingSpeed.Level4 ? Av1LoopFilterPickMethod.FullImageNonDual : Av1LoopFilterPickMethod.FullImage;

        // Good quality from speed 3 stops the level search of an inter frame at a step of two.
        this.UseCoarseFilterLevelSearch = !realtime && !allIntra && !intraFrame && speed >= HeifEncodingSpeed.Level3;

        // Real-time usage, and good quality from speed 3, drop compound prediction from two references on the same
        // side of the frame.
        this.DisableOneSidedCompound = realtime || (!allIntra && speed >= HeifEncodingSpeed.Level3);

        // Good quality from speed 1 searches no compound mode in an alternate reference frame of the base layer.
        this.SkipAlternateReferenceCompound = !realtime && !allIntra && speed >= HeifEncodingSpeed.Level1;

        // Selective reference frames: good quality starts at level 1 and raises the level with speed. Speed 0 uses level 2 for a 1080p
        // frame at a low quantizer. Real-time usage uses level 4.
        // Compound reference pruning: good quality prunes compound pairs from speed 3 above 480p. At speed 4, it prunes from 720p, and
        // from 480p outside the boosted and internal alternate-reference frames. At speed 5, it prunes everywhere. At speed 6, it
        // prunes every pair that is not kept.
        int compoundPruning = 0;
        if (!realtime && !allIntra && speed >= HeifEncodingSpeed.Level3)
        {
            compoundPruning = this.minimumDimension <= 480 ? 0 : 1;
            if (speed >= HeifEncodingSpeed.Level4)
            {
                if (this.minimumDimension >= 720)
                {
                    compoundPruning = 2;
                }
                else if (this.minimumDimension >= 480)
                {
                    compoundPruning = boostedOrInternalAlternate ? 0 : 2;
                }
            }

            if (speed >= HeifEncodingSpeed.Level5)
            {
                compoundPruning = speed >= HeifEncodingSpeed.Level6 ? 3 : 2;
            }
        }

        this.CompoundReferencePruningLevel = compoundPruning;

        // Alternate reference search: good quality uses level 1 from speed 2 and level 2 from speed 4. Real-time usage uses level 2.
        this.AlternateReferenceSearchLevel = realtime ? 2 : allIntra ? 0 : speed switch
        {
            >= HeifEncodingSpeed.Level4 => 2,
            >= HeifEncodingSpeed.Level2 => 1,
            _ => 0
        };

        this.SelectiveReferenceFrameLevel = realtime ? 4 : allIntra ? 0 : speed switch
        {
            >= HeifEncodingSpeed.Level6 => 6,
            >= HeifEncodingSpeed.Level3 => 5,
            HeifEncodingSpeed.Level2 => 3,
            HeifEncodingSpeed.Level1 => 2,
            _ => minimumDimension >= 1080 && qIndex <= 108 ? 2 : 1
        };

        // Inter mode pruning by the temporal dependency model: good quality prunes LAST2 modes at every speed. From speed 3, a frame
        // that is not boosted prunes more with speed. A boosted frame keeps level 1. Real-time usage leaves the pruning off.
        this.TplInterModePruningLevel = realtime || allIntra ? 0 : boosted ? 1 : speed switch
        {
            >= HeifEncodingSpeed.Level6 => 4,
            >= HeifEncodingSpeed.Level4 => 3,
            HeifEncodingSpeed.Level3 => 2,
            _ => 1
        };

        this.EnableWinnerCoefficientOptimization = !realtime &&
            speed >= (allIntra ? HeifEncodingSpeed.Level4 : HeifEncodingSpeed.Level3);

        this.EstimateTransformTypeRateDistortion = allIntra && speed is HeifEncodingSpeed.Level4 or HeifEncodingSpeed.Level5;
        this.IntraTransformTypeSearchLevel = speed < HeifEncodingSpeed.Level4 ? 0 : allIntra || realtime ? 2 : 1;

        // Deferred transform size search: good quality at speed 2 below 480p defers in a frame that is not boosted. Speed 3 defers in
        // an inter frame, and speed 4 and above defer in every frame.
        this.DeferTransformSizeSearch = allIntra
            ? speed >= HeifEncodingSpeed.Level4
            : speed >= HeifEncodingSpeed.Level4 || (speed == HeifEncodingSpeed.Level3 && !intraFrame) ||
                (speed == HeifEncodingSpeed.Level2 && !boosted && minimumDimension < 480);

        this.IntraWinnerCount = intraFrame && speed is HeifEncodingSpeed.Level4 or HeifEncodingSpeed.Level5
            ? speed == HeifEncodingSpeed.Level4 ? 3 : 2
            : 1;

        this.PruneIntraTransformDepth = allIntra && speed >= HeifEncodingSpeed.Level6;
        this.PruneIntraWinnerByVariance = allIntra && speed >= HeifEncodingSpeed.Level6;
        this.SkipFilterIntraAfterInvalidDc = !allIntra && speed >= HeifEncodingSpeed.Level2;
        this.FilterIntraPruneLevel = allIntra ? speed >= HeifEncodingSpeed.Level6 ? 2 : speed >= HeifEncodingSpeed.Level2 ? 1 : 0 : 0;

        // Coefficient optimization level: good quality at speed 1 keeps the lower level in a boosted frame. The faster speeds keep the
        // lower level in boosted and internal alternate-reference frames.
        int coefficientLevel = speed switch
        {
            HeifEncodingSpeed.Level0 => 1,
            HeifEncodingSpeed.Level1 => allIntra || boosted ? 2 : 3,
            HeifEncodingSpeed.Level2 or HeifEncodingSpeed.Level3 => allIntra || boostedOrInternalAlternate ? 3 : 4,
            HeifEncodingSpeed.Level4 or HeifEncodingSpeed.Level5 => allIntra || boostedOrInternalAlternate ? 5 : 7,
            _ => allIntra || boostedOrInternalAlternate ? 6 : 8
        };

        if (speed == HeifEncodingSpeed.Level0 && minimumDimension >= 720 && qIndex <= 128)
        {
            coefficientLevel = minimumDimension >= 1080 ? 3 : 2;
        }

        coefficientLevel = realtime ? 0 : coefficientLevel;
        this.CoefficientOptimizationLevel = coefficientLevel;
        this.DefaultCoefficientOptimizationThresholds = coefficientLevel switch
        {
            1 => (3200U, uint.MaxValue),
            2 => (1728U, uint.MaxValue),
            3 => (864U, uint.MaxValue),
            4 => (432U, uint.MaxValue),
            5 => (864U, 97U),
            6 => (432U, 97U),
            7 or 8 => (216U, 25U),
            _ => (uint.MaxValue, uint.MaxValue)
        };

        this.ModeCoefficientOptimizationThresholds = this.EnableWinnerCoefficientOptimization
            ? coefficientLevel switch
            {
                1 => (250U, uint.MaxValue),
                2 or 3 => (142U, uint.MaxValue),
                4 => (86U, uint.MaxValue),
                5 => (142U, 16U),
                6 => (86U, 16U),
                7 => (86U, 10U),
                8 => (0U, 10U),
                _ => (uint.MaxValue, uint.MaxValue)
            }
            : this.DefaultCoefficientOptimizationThresholds;

        this.WinnerCoefficientOptimizationThresholds = this.EnableWinnerCoefficientOptimization
            ? (uint.MaxValue, uint.MaxValue)
            : this.ModeCoefficientOptimizationThresholds;

        // Transform-domain distortion has a level, a threshold level and a winner-stage switch. Sequence speed 0 applies the rule for
        // 1080p frames at a low quantizer. Good quality uses level 1 in a boosted frame.
        bool largeLowQuantizer = speed == HeifEncodingSpeed.Level0 && minimumDimension >= 1080 && qIndex <= 108;
        int distortionLevel = realtime ? 2 : allIntra
            ? speed >= HeifEncodingSpeed.Level6 ? 3 : speed >= HeifEncodingSpeed.Level1 ? 1 : 0
            : speed >= HeifEncodingSpeed.Level1 || largeLowQuantizer ? boosted ? 1 : 2 : 0;

        // Real-time usage starts at speed 7 and uses threshold level 3. Good-quality usage keeps level 1 from speed 1.
        int distortionThresholdLevel = realtime ? 3 : allIntra
            ? speed >= HeifEncodingSpeed.Level4 ? 3 : speed >= HeifEncodingSpeed.Level1 ? 1 : 0
            : speed >= HeifEncodingSpeed.Level1 || largeLowQuantizer ? 1 : 0;

        bool winnerDistortionStages = !realtime && speed >= (allIntra ? HeifEncodingSpeed.Level4 : HeifEncodingSpeed.Level3);
        uint distortionThreshold = distortionThresholdLevel switch
        {
            1 => 22026U,
            2 => 1377U,
            3 => 0U,
            _ => uint.MaxValue
        };

        // The distortion types for the default, mode and winner evaluation stages, in that order.
        (int Default, int Mode, int Winner) distortionTypes = distortionLevel switch
        {
            1 => (1, 2, 0),
            2 => (2, 2, 0),
            3 => (2, 2, 2),
            _ => (0, 2, 0)
        };

        this.DefaultTransformDomainDistortion = (distortionTypes.Default, distortionThreshold);
        this.ModeTransformDomainDistortion = winnerDistortionStages
            ? (distortionTypes.Mode, distortionThreshold)
            : this.DefaultTransformDomainDistortion;

        this.WinnerTransformDomainDistortion = winnerDistortionStages
            ? (distortionTypes.Winner, distortionThreshold)
            : this.DefaultTransformDomainDistortion;

        this.SkipTransformSearchAfterEmptyBlock = realtime || speed >= HeifEncodingSpeed.Level1 || (!allIntra && largeLowQuantizer);

        // Skip and DC-only block prediction has one level, which selects a row of stage levels. Good quality at speed 4 keeps level 0
        // in a boosted frame.
        int dcPredictionLevel = realtime ? intraFrame ? 0 : 3 : allIntra
            ? speed >= HeifEncodingSpeed.Level6 ? 1 : 0
            : speed >= HeifEncodingSpeed.Level6 ? 3 : speed >= HeifEncodingSpeed.Level5 ? 2
                : speed >= HeifEncodingSpeed.Level4 ? boosted ? 0 : 2 : 0;

        // The prediction levels for the default, mode and winner evaluation stages, in that order.
        (int Default, int Mode, int Winner) dcPredictionLevels = dcPredictionLevel switch
        {
            1 => (1, 1, 0),
            2 => (2, 2, 0),
            3 => (2, 2, 2),
            _ => (0, 0, 0)
        };

        this.DefaultPredictDcLevel = dcPredictionLevels.Default;
        this.ModePredictDcLevel = dcPredictionLevels.Mode;
        this.WinnerPredictDcLevel = dcPredictionLevels.Winner;
    }

    /// <summary>
    /// Selects how the maximum partition model's class scores become a block-size limit.
    /// </summary>
    public enum MaximumPartitionPrediction
    {
        /// <summary>Uses the configured size limit without motion-based prediction.</summary>
        Disabled,

        /// <summary>Selects the class with the largest score.</summary>
        Direct,

        /// <summary>Retains the largest size covered by the cumulative probability threshold.</summary>
        Relaxed,

        /// <summary>Adjusts the cumulative probability threshold using source variance.</summary>
        Adaptive
    }

    /// <summary>
    /// Gets a value indicating whether successive vectors of one reference reuse the selected intra blend mode.
    /// </summary>
    public bool ReuseInterIntraMode { get; }

    /// <summary>
    /// Gets a value indicating whether wedge selection reuses the smooth blend's intra mode.
    /// </summary>
    public bool FastInterIntraWedgeSearch { get; }

    /// <summary>
    /// Gets the source variance above which inter-intra wedge search is enabled.
    /// </summary>
    public int InterIntraWedgeVarianceThreshold { get; }

    /// <summary>
    /// Gets the prediction-error threshold policy used before inter transform search.
    /// </summary>
    public int InterTransformGateLevel { get; }

    /// <summary>
    /// Gets the inter residual estimator: disabled, learned, or curve fitted.
    /// </summary>
    public int InterModeEstimation { get; }

    /// <summary>
    /// Gets the transform-search count after which a searched motion candidate can end the search.
    /// </summary>
    public int InterModeCandidateLimit { get; }

    /// <summary>
    /// Gets the number of transform searches before repeated prediction modes are restricted.
    /// </summary>
    public int InterModeRepeatThreshold { get; }

    /// <summary>
    /// Gets the first-winner transform-search termination policy.
    /// </summary>
    public int InterModeTransformBreakout { get; }

    /// <summary>
    /// Gets the maximum ranked inter candidates passed to transform search.
    /// </summary>
    public int MaximumInterTransformCandidates { get; }

    /// <summary>
    /// Gets a value indicating whether the selected intra mode prunes rectangular partitions.
    /// </summary>
    public bool PruneRectangularPartitionsUsingIntraMode { get; }

    /// <summary>
    /// Gets a value indicating whether invalid square candidates end a child partition search.
    /// </summary>
    public bool TerminatePartitionSearchAfterInvalidNoneAndSplit { get; }

    /// <summary>
    /// Gets whether a losing split suppresses rectangles after its first two children or at any child.
    /// </summary>
    public int RectangularPartitionPruningLevel { get; }

    /// <summary>
    /// Gets whether an empty unsplit residual suppresses extended or all rectangular searches in an ordinary inter frame.
    /// </summary>
    public int SkippablePartitionPruningLevel { get; }

    /// <summary>
    /// Gets the distortion threshold for terminating a skippable partition branch.
    /// </summary>
    public int PartitionBreakoutDistortionThreshold { get; }

    /// <summary>
    /// Gets the rate threshold per logarithmic sample count for terminating a skippable partition branch.
    /// </summary>
    public int PartitionBreakoutRateThreshold { get; }

    /// <summary>
    /// Gets the encoding speed tier.
    /// </summary>
    public HeifEncodingSpeed Speed { get; }

    /// <summary>
    /// Gets a value indicating whether the picture is an intra-only, golden, or alternate-reference update. The encoder codes such a
    /// picture at a higher quality than the frames around it.
    /// </summary>
    public bool IsBoosted { get; }

    /// <summary>
    /// Gets a value indicating whether independent horizontal and vertical interpolation filters are enabled.
    /// </summary>
    public bool EnableDualFilter { get; }

    /// <summary>
    /// Gets a value indicating whether the sequence enables inter-intra compound prediction. The flag is off when the inter-intra wedge
    /// search is off: in real-time usage, and in good-quality usage from speed 3 or at speed 2 below 480p.
    /// </summary>
    public bool EnableInterIntraCompound => this.allIntra ||
        (!this.realtime && (this.Speed < HeifEncodingSpeed.Level2 ||
        (this.Speed == HeifEncodingSpeed.Level2 && this.minimumDimension >= 480)));

    /// <summary>
    /// Gets a value indicating whether loop restoration remains enabled for the sequence.
    /// </summary>
    public bool EnableRestoration { get; }

    /// <summary>
    /// Gets a value indicating whether the sequence enables distance-weighted compound prediction.
    /// </summary>
    public bool UseDistanceWeightedCompound { get; }

    /// <summary>
    /// Gets a value indicating whether eighth-sample motion-vector syntax is enabled for the picture.
    /// </summary>
    public bool AllowHighPrecisionMotionVector { get; }

    /// <summary>
    /// Gets a value indicating whether all-intra variance-based partitioning replaces rate-distortion partition search.
    /// </summary>
    public bool UseVarianceBasedPartition { get; }

    /// <summary>
    /// Gets a value indicating whether the frame allowed the screen content tools when the encoder set the frame-size speed settings.
    /// The encoder sets them before the screen content trial of a key frame.
    /// </summary>
    public bool FrameSizeScreenContentTools { get; }

    /// <summary>
    /// Gets the smallest square partition permitted by the speed and resolution policy.
    /// </summary>
    public Av1BlockSize MinimumPartitionSize { get; }

    /// <summary>
    /// Gets the largest square partition permitted by the speed policy.
    /// </summary>
    public Av1BlockSize MaximumPartitionSize { get; }

    /// <summary>
    /// Gets the dominant-color and k-means palette search pruning level.
    /// </summary>
    public int PaletteSearchLevel { get; }

    /// <summary>
    /// Gets the quantizer-based transform-size and early-skip level for prediction-only inter search.
    /// </summary>
    public int EstimatedTransformQuantizerLevel
        => this.realtime && this.Speed >= HeifEncodingSpeed.Level8 ? this.minimumDimension < 360 ? 1 : 2 : 0;

    /// <summary>
    /// Gets a value indicating whether inter blocks use prediction-based mode decisions.
    /// </summary>
    public bool UseEstimatedInterModeDecision => this.realtime;

    /// <summary>
    /// Gets a value indicating whether blocks pick their modes with the estimated, prediction-based search instead
    /// of the full rate-distortion search. All-intra usage turns it on from speed 8 and real-time usage at every
    /// speed it covers. Good-quality usage never uses it.
    /// </summary>
    public bool UseEstimatedModeDecision => this.allIntra ? this.Speed >= HeifEncodingSpeed.Level8 : this.realtime;

    /// <summary>
    /// Gets the level that lets small intra blocks of the estimated search use the full intra search. Zero keeps the
    /// estimated search for every block. A nonzero level sends blocks below 16x16 to the full search when their source
    /// variance reaches the threshold of the level: 0 for level 1, 101 for level 2 and 201 for level 3. All-intra
    /// usage uses level 2 at speed 8 and level 0 above it. Real-time usage uses level 1.
    /// </summary>
    public int HybridIntraSearchLevel
        => this.allIntra ? this.Speed == HeifEncodingSpeed.Level8 ? 2 : 0 : this.realtime ? 1 : 0;

    /// <summary>
    /// Gets a value indicating whether the chroma transform search scales its rate multiplier by the chroma
    /// quantizer weights. All-intra and real-time usage turn it on. Good-quality usage keeps the luma multiplier.
    /// </summary>
    public bool UseChromaTrellisRateMultiplier => this.allIntra || this.realtime;

    /// <summary>
    /// Gets a value indicating whether the sequence uses the real-time settings.
    /// </summary>
    public bool IsRealtime => this.realtime;

    /// <summary>
    /// Gets a value indicating whether each 64x64 unit can leave CDEF off through a second, empty strength. Real-time usage sets it
    /// at speed 9, for frames of 360p and larger.
    /// </summary>
    public bool SkipCdefSuperblock => this.realtime && this.Speed >= HeifEncodingSpeed.Level9 && this.minimumDimension >= 360;

    /// <summary>
    /// Gets the warped-motion usage probability, out of 128, below which a frame disallows warped motion, or 0 to
    /// keep it allowed. Real-time usage uses 8 from speed 6. Good-quality usage from speed 5 uses 8 at 480p and 16 at 720p.
    /// </summary>
    public int WarpedProbabilityThreshold
        => this.realtime
            ? this.Speed >= HeifEncodingSpeed.Level6 ? 8 : 0
            : this.Speed >= HeifEncodingSpeed.Level5 && this.minimumDimension >= 720 ? 16
            : this.Speed >= HeifEncodingSpeed.Level5 && this.minimumDimension >= 480 ? 8 : 0;

    /// <summary>
    /// Gets the frame probability below which a block size does not search OBMC: none before good-quality speed 2,
    /// a small threshold at speeds 2 and 3, and every block from speed 4.
    /// </summary>
    public int ObmcProbabilityThreshold
        => this.realtime || this.allIntra ? 0
            : this.Speed >= HeifEncodingSpeed.Level4 ? int.MaxValue
            : this.Speed >= HeifEncodingSpeed.Level2 ? this.minimumDimension >= 720 ? 16 : 8 : 0;

    /// <summary>
    /// Gets the level that adapts the ALTREF lag to the average source SAD, or 0 for the fixed lag of 4 frames. Real-time usage sets
    /// it at speed 9 for frames of 360p and larger, with a separate level from 720p.
    /// </summary>
    public int AlternateReferenceLagLevel
        => this.realtime && this.Speed >= HeifEncodingSpeed.Level9 && this.minimumDimension >= 360
            ? this.minimumDimension >= 720 ? 1 : 2
            : 0;

    /// <summary>
    /// Gets a value indicating whether warped motion is pruned further, which also restores the frame probability
    /// tables at every golden refresh. Real-time usage sets it from speed 6.
    /// </summary>
    public bool ExtraPruneWarped => this.realtime && this.Speed >= HeifEncodingSpeed.Level6;

    /// <summary>
    /// Gets a value indicating whether a coded constant-bitrate frame can force or cancel the golden refresh. Real-time usage sets
    /// it from speed 6.
    /// </summary>
    public bool UsesQuantizerGoldenRefresh => this.realtime && this.Speed >= HeifEncodingSpeed.Level6;

    /// <summary>
    /// Gets a value indicating whether a compound candidate of the large-block model is dropped when its luma
    /// variance exceeds that of either of its single-reference modes. Real-time usage sets it from speed 7.
    /// </summary>
    public bool PrunesCompoundBySingleVariance => this.realtime && this.Speed >= HeifEncodingSpeed.Level7;

    /// <summary>
    /// Gets a value indicating whether estimated inter search admits compound prediction.
    /// </summary>
    public bool UseEstimatedCompound => this.realtime && (this.Speed < HeifEncodingSpeed.Level9 || this.minimumDimension >= 360);

    /// <summary>
    /// Gets a value indicating whether low-resolution mode rejection uses its stronger threshold.
    /// </summary>
    public bool AggressiveEstimatedModeSkip => this.Speed >= HeifEncodingSpeed.Level9 && this.minimumDimension < 360;

    /// <summary>
    /// Gets a value indicating whether alternate-reference candidates participate in estimated mode search.
    /// </summary>
    public bool UseEstimatedAlternateReference => this.Speed < HeifEncodingSpeed.Level8
        || (this.Speed == HeifEncodingSpeed.Level8 && this.minimumDimension >= 360);

    /// <summary>
    /// Gets the level for omitting intra prediction after an initially skippable inter winner.
    /// </summary>
    public int EstimatedIntraSkipLevel => this.Speed >= HeifEncodingSpeed.Level9 && this.minimumDimension >= 360 ? 2 : 1;

    /// <summary>
    /// Gets a value indicating whether temporal partition variance can prune secondary-reference search.
    /// </summary>
    public bool UseEstimatedLowTemporalVariance => this.Speed >= HeifEncodingSpeed.Level9 ||
        (this.Speed == HeifEncodingSpeed.Level8 && this.minimumDimension < 360);

    /// <summary>
    /// Gets a value indicating whether interpolation search uses neighboring filters and alternating blocks.
    /// </summary>
    public bool UseEstimatedFilterChessboard => this.Speed >= HeifEncodingSpeed.Level9 && this.minimumDimension >= 360 &&
        this.minimumDimension < 1080;

    /// <summary>
    /// Gets the luma palette header-cost pruning level.
    /// </summary>
    public int LumaPaletteHeaderPruneLevel { get; }

    /// <summary>
    /// Gets a value indicating whether chroma palette-size search terminates when its header alone exceeds the best cost.
    /// </summary>
    public bool EarlyTerminateChromaPaletteSearch { get; }

    /// <summary>
    /// Gets the maximum number of transform-size splits searched in a square intra block.
    /// </summary>
    public int IntraSquareTransformSearchDepth { get; }

    /// <summary>
    /// Gets the maximum number of transform-size splits searched in a rectangular intra block.
    /// </summary>
    public int IntraRectangularTransformSearchDepth { get; }

    /// <summary>
    /// Gets a value indicating whether a better transform-size result tightens the remaining search bound.
    /// </summary>
    public bool UseIntraTransformRdBreakout { get; }

    /// <summary>
    /// Gets a value indicating whether chroma candidates are derived from the selected luma mode.
    /// </summary>
    public bool PruneChromaModesUsingLumaWinner { get; }

    /// <summary>
    /// Gets the chroma directional histogram pruning level, or zero when disabled.
    /// </summary>
    public int ChromaHogPruningLevel { get; }

    /// <summary>
    /// Gets a value indicating whether low source variance omits smooth chroma prediction.
    /// </summary>
    public bool PruneChromaSmoothByVariance { get; }

    /// <summary>
    /// Gets the alpha refinement range on each side of the estimated chroma-from-luma parameter, including its center.
    /// </summary>
    public int ChromaFromLumaSearchRange { get; }

    /// <summary>
    /// Gets a value indicating whether smooth intra predictors are restricted by the search policy.
    /// </summary>
    public bool DisableSmoothIntra { get; }

    /// <summary>
    /// Gets a value indicating whether blocks with a square transform of at least 32 samples use only DC intra prediction.
    /// </summary>
    public bool RestrictLargeIntraBlocksToDc { get; }

    /// <summary>
    /// Gets the directional histogram pruning level, or zero when disabled.
    /// </summary>
    public int IntraHogPruningLevel { get; }

    /// <summary>
    /// Gets the number of model costs retained when pruning intra candidates.
    /// </summary>
    public int IntraModelCandidateCount { get; }

    /// <summary>
    /// Gets a value indicating whether neighboring modes narrow the retained model cost range.
    /// </summary>
    public bool AdaptIntraModelCountToNeighbors { get; }

    /// <summary>
    /// Gets a value indicating whether adjacent even angles can prune odd angle candidates.
    /// </summary>
    public bool PruneOddIntraAngleDeltas { get; }

    /// <summary>
    /// Gets the adaptive transform-search threshold level.
    /// </summary>
    public int InterAdaptiveTransformSearchLevel { get; }

    /// <summary>
    /// Gets the predicted-skip level. For each evaluation stage, it selects how the search decides that an inter luma residual
    /// quantizes to nothing.
    /// </summary>
    public int SkipFlagPredictionLevel { get; }

    /// <summary>
    /// Gets the residual-statistics pruning level for high-bit-depth inter transform trees.
    /// </summary>
    public int InterTransformSizePruningLevel { get; }

    /// <summary>
    /// Gets the model score threshold below which an eight-bit transform is not subdivided.
    /// </summary>
    public int InterTransformSplitThreshold { get; }

    /// <summary>
    /// Gets the starting search depth used to limit inter transform subdivisions.
    /// </summary>
    public int InterTransformSearchInitialDepth { get; }

    /// <summary>
    /// Gets the number of best unsplit inter-transform costs retained for subdivision pruning.
    /// </summary>
    public int InterTransformNoSplitCandidateCount { get; }

    /// <summary>
    /// Gets a value indicating whether wedge masks are selected using prediction-error estimates.
    /// </summary>
    public bool UseCompoundWedgeModel { get; }

    /// <summary>
    /// Gets a value indicating whether opposite predictor quadrants select a shared wedge sign.
    /// </summary>
    public bool FastWedgeSignEstimation { get; }

    /// <summary>
    /// Gets a value indicating whether a masked blend must improve the best prediction-error estimate.
    /// </summary>
    public bool PruneCompoundTypeByModel { get; }

    /// <summary>
    /// Gets the distance-weighted error threshold used to prune difference-weighted trials.
    /// </summary>
    public int CompoundTypePruningLevel { get; }

    /// <summary>
    /// Gets the maximum source variance that bypasses inter-inter wedge search.
    /// </summary>
    public int InterInterWedgeVarianceThreshold { get; }

    /// <summary>
    /// Gets a value indicating whether wedge trials refine their searched motion vectors.
    /// </summary>
    public bool RefineWedgeMotion { get; }

    /// <summary>
    /// Gets a value indicating whether oblique asymmetric masks depend on the best symmetric mask.
    /// </summary>
    public bool FastWedgeMaskSearch { get; }

    /// <summary>
    /// Gets a value indicating whether low predictor disagreement bypasses transform-based wedge search.
    /// </summary>
    public bool SkipWedgeForSimilarPredictors { get; }

    /// <summary>
    /// Gets the number of best average-compound estimates retained for pruning.
    /// </summary>
    public int CompoundAverageCandidateCount { get; }

    /// <summary>
    /// Gets a value indicating whether matching predictors reuse their selected compound type.
    /// </summary>
    public bool ReuseCompoundTypeDecision { get; }

    /// <summary>
    /// Gets a value indicating whether mask choices carry across the reference-list candidates.
    /// </summary>
    public bool ReuseCompoundMaskResults { get; }

    /// <summary>
    /// Gets the compound motion policy: zero for unrestricted trials, one for full joint refinement, or two for reduced joint refinement.
    /// </summary>
    public int CompoundMotionSearchLevel { get; }

    /// <summary>
    /// Gets a value indicating whether joint integer motion uses adjacent-site refinement.
    /// </summary>
    public bool UseLocalJointMotionSearch { get; }

    /// <summary>
    /// Gets a value indicating whether chroma search subtracts the established luma cost from its bound.
    /// </summary>
    public bool UseLumaCostForChromaBound { get; }

    /// <summary>
    /// Gets the inter transform-type pruning level outside staged mode evaluation.
    /// </summary>
    public int DefaultInterTransformTypePruning { get; }

    /// <summary>
    /// Gets the transform-type probability-pruning level.
    /// </summary>
    public int TransformTypeProbabilityPruning { get; }

    /// <summary>
    /// Gets the probability threshold for fixing an inter candidate's transform type.
    /// </summary>
    public int InterTransformTypeProbabilityThreshold { get; }

    /// <summary>
    /// Gets the selected-inter refinement pruning level.
    /// </summary>
    public int InterWinnerPruningLevel { get; }

    /// <summary>
    /// Gets the trellis optimization level.
    /// </summary>
    public int CoefficientOptimizationLevel { get; }

    /// <summary>Gets the interpolation-result reuse level.</summary>
    public int InterpolationReuseLevel { get; }

    /// <summary>Gets the frame-history interpolation-pruning level.</summary>
    public int InterpolationPruningLevel { get; }

    /// <summary>
    /// Gets the ranking and candidate-count policy for compound references.
    /// </summary>
    public int CompoundSingleResultPruningLevel { get; }

    /// <summary>
    /// Gets a value indicating whether mixed near/new compound modes are omitted.
    /// </summary>
    public bool SkipMixedNearCompound { get; }

    /// <summary>
    /// Gets the number of compound references that must occur in adjacent blocks.
    /// </summary>
    public int CompoundNeighborPruningLevel { get; }

    /// <summary>
    /// Gets a value indicating whether mixed compound modes require a searched single-reference winner.
    /// </summary>
    public bool PruneMixedCompoundBySingleWinner { get; }

    /// <summary>
    /// Gets a value indicating whether a compound pair is searched only when one of its references has a single
    /// reference result within ten percent of the best.
    /// </summary>
    public bool PruneCompoundUsingSingleReference { get; }

    /// <summary>
    /// Gets the motion-distance threshold tier for rejecting unsuccessful repeated compound candidates.
    /// </summary>
    public int CompoundReferenceIndexPruningLevel { get; }

    /// <summary>
    /// Gets a value indicating whether single-reference candidates retain their initial interpolation filters.
    /// </summary>
    public bool SkipSingleInterpolationSearch { get; }

    /// <summary>
    /// Gets a value indicating whether the luma palette search leaves the color map rate out of the palette cost.
    /// Real-time speed 7 and above set it.
    /// </summary>
    public bool DiscountPaletteColorCost => this.realtime && this.Speed >= HeifEncodingSpeed.Level7;

    /// <summary>
    /// Gets a value indicating whether an inter frame chooses its motion vector precision from the statistics of the
    /// last coded frame. Good-quality speeds 0 to 2 set it.
    /// </summary>
    public bool UsesLastMotionVectorData => !this.realtime && !this.allIntra && this.Speed <= HeifEncodingSpeed.Level2;

    /// <summary>
    /// Gets a value indicating whether the selected single-reference prediction searches filters during refinement.
    /// </summary>
    public bool UseWinnerInterpolation { get; }

    /// <summary>
    /// Gets a value indicating whether interpolation uses the linear quantizer/error model.
    /// </summary>
    public bool UseSimpleInterpolationModel { get; }

    /// <summary>
    /// Gets a value indicating whether winner interpolation compares regular with sharp instead of smooth.
    /// </summary>
    public bool WinnerInterpolationUsesSharp { get; }

    /// <summary>
    /// Gets the neighbor-reference threshold tier for near-motion modes.
    /// </summary>
    public int NearNeighborPruningLevel { get; }

    /// <summary>
    /// Gets a value indicating whether weak spatial-reference support can reject nearest and near modes.
    /// </summary>
    public bool PruneSpatialMotionByWeight { get; }

    /// <summary>
    /// Gets the quantizer and temporal-distance policy for dynamic reference indices.
    /// </summary>
    public int ReduceInterReferenceIndices { get; }

    /// <summary>
    /// Gets a value indicating whether near-vector entries are ranked by a luma translation estimate.
    /// </summary>
    public bool PruneNearMotionByTranslation { get; }

    /// <summary>
    /// Gets the cap multiplier for history-based mode rejection, or zero when history is fixed.
    /// </summary>
    public int AdaptiveModeThresholdLevel { get; }

    /// <summary>
    /// Gets a value indicating whether empty-residual winners increase the mode-rejection threshold.
    /// </summary>
    public bool PruneSkippableInterModes { get; }

    /// <summary>
    /// Gets the strength of intra rejection after inter prediction has selected a winner.
    /// </summary>
    public int IntraInInterPruningLevel { get; }

    /// <summary>
    /// Gets the largest intra block considered during inter-picture mode search.
    /// </summary>
    public Av1BlockSize MaximumIntraBlockSize { get; }

    /// <summary>
    /// Gets the motion range level that skips intra search for a block above <see cref="MaximumIntraBlockSize"/>
    /// whose best single-reference motion is small, or zero to never skip it. Only real-time usage sets it.
    /// </summary>
    public int IntraModeMotionRangePruneLevel { get; }

    /// <summary>
    /// Gets a value indicating whether an unsplit block can terminate partition search using its trained model.
    /// </summary>
    public bool EnablePartitionBreakoutModel { get; }

    /// <summary>
    /// Gets a value indicating whether square-candidate costs and source variance classify rectangular searches.
    /// </summary>
    public bool EnableRectanglePartitionModel { get; }

    /// <summary>
    /// Gets the power-of-two width multiplier over the minimum leaf size required for four-strip search.
    /// </summary>
    public int FourStripPartitionMinimumScale { get; }

    /// <summary>
    /// Gets the winner-direction and estimated-cost pruning tier for asymmetric partitions.
    /// </summary>
    public int ExtendedPartitionPruningLevel { get; }

    /// <summary>
    /// Gets whether child partition outcomes prune four-strip searches alone or asymmetric searches too.
    /// </summary>
    public int ChildPartitionPruningLevel { get; }

    /// <summary>
    /// Gets the four-strip model and threshold policy tier.
    /// </summary>
    public int FourStripPartitionModelLevel { get; }

    /// <summary>
    /// Gets the additional coarse search stages omitted by preliminary partition motion searches.
    /// </summary>
    public int SimpleMotionStepReduction { get; }

    /// <summary>
    /// Gets the confidence level used to stop partition search after evaluating square children.
    /// </summary>
    public int AfterSplitTerminationLevel { get; }

    /// <summary>
    /// Gets how motion features restrict the maximum square partition size.
    /// </summary>
    public MaximumPartitionPrediction MaximumPartitionPredictionMode { get; }

    /// <summary>
    /// Gets the low, middle, or high-resolution threshold row for four-strip classification.
    /// </summary>
    public int FourStripPartitionResolutionIndex { get; }

    /// <summary>
    /// Gets a value indicating whether breakout features use the normalized high-resolution model.
    /// </summary>
    public bool UseHighResolutionPartitionBreakout { get; }

    /// <summary>
    /// Gets the breakout threshold scaling tier for high-bit-depth samples.
    /// </summary>
    public int PartitionBreakoutHighBitDepthLevel { get; }

    /// <summary>
    /// Gets the largest block size eligible for non-square search and modeled breakout.
    /// </summary>
    public Av1BlockSize SquareOnlyPartitionThreshold { get; }

    /// <summary>
    /// Gets a value indicating whether modeled prediction cost bounds residual search.
    /// </summary>
    public bool ModelBasedInterpolationBreakout { get; }

    /// <summary>Gets a value indicating whether filter ranking models only luma.</summary>
    public bool SkipInterpolationChromaModel { get; }

    /// <summary>Gets a value indicating whether a smooth winner excludes the sharp filter.</summary>
    public bool SkipSharpInterpolationAfterSmooth { get; }

    /// <summary>Gets a value indicating whether sharp-filter trials receive a reduced comparison cost.</summary>
    public bool PreferSharpInterpolation { get; }

    /// <summary>Gets a value indicating whether agreeing neighbors restrict alternating filter searches.</summary>
    public bool UseNeighborInterpolation { get; }

    /// <summary>
    /// Gets a value indicating whether completed transforms update frame probabilities.
    /// </summary>
    public bool TrackTransformTypeProbabilities => this.TransformTypeProbabilityPruning != 0 ||
        this.InterTransformTypeProbabilityThreshold is > 0 and < int.MaxValue;

    /// <summary>
    /// Gets the inter transform-type pruning level during candidate evaluation.
    /// </summary>
    public int CandidateInterTransformTypePruning { get; }

    /// <summary>
    /// Gets the inter transform-type pruning level during winner evaluation.
    /// </summary>
    public int WinnerInterTransformTypePruning { get; }

    /// <summary>
    /// Gets a value indicating whether coefficient trellis optimization is enabled.
    /// </summary>
    public bool EnableCoefficientOptimization { get; }

    /// <summary>
    /// Gets the method that chooses the deblocking filter levels of each frame.
    /// </summary>
    public Av1LoopFilterPickMethod LoopFilterPickMethod { get; }

    /// <summary>
    /// Gets a value indicating whether the loop filter level search ends at a step of two instead of one.
    /// </summary>
    public bool UseCoarseFilterLevelSearch { get; }

    /// <summary>
    /// Gets the level that limits the references an inter frame searches.
    /// </summary>
    public int SelectiveReferenceFrameLevel { get; }

    /// <summary>
    /// Gets the level that prunes compound reference pairs that are neither kept nor the closest pair. Levels one and two keep a pair
    /// with the best predicted-vector SAD on each side. Level three prunes that pair too.
    /// </summary>
    public int CompoundReferencePruningLevel { get; }

    /// <summary>
    /// Gets the level that limits the references of a frame coded from the source of an alternate reference, and of
    /// an unshown frame whose alternate references lie in the past.
    /// </summary>
    public int AlternateReferenceSearchLevel { get; }

    /// <summary>
    /// Gets the level that skips inter modes whose references predicted much worse than the best reference in the temporal dependency
    /// model, or zero to never skip them.
    /// </summary>
    public int TplInterModePruningLevel { get; }

    /// <summary>
    /// Gets the pyramid level on which the global motion search finds its corners.
    /// </summary>
    public int GlobalMotionDownsampleLevel =>
        this.Speed >= HeifEncodingSpeed.Level6 ? 2 : this.Speed >= HeifEncodingSpeed.Level4 ? 1 : 0;

    /// <summary>
    /// Gets the number of refinement steps of each global motion model.
    /// </summary>
    public int GlobalMotionRefinementSteps =>
        this.Speed >= HeifEncodingSpeed.Level3 ? 0 : this.Speed >= HeifEncodingSpeed.Level2 ? 2 : 5;

    /// <summary>
    /// Gets the level that drops a zero-vector global mode when its prediction error exceeds that of the best new vector of its
    /// reference. Zero is off, one uses a margin of a quarter, and two uses no margin. Real-time usage does not use the full mode
    /// search, so the level is zero there.
    /// </summary>
    public int ZeroVectorSsePruningLevel =>
        this.realtime ? 0 : this.Speed >= HeifEncodingSpeed.Level4 ? 2 : this.Speed >= HeifEncodingSpeed.Level3 ? 1 : 0;

    /// <summary>
    /// Gets a value indicating whether compound prediction from two references on the same side of the frame is
    /// disabled.
    /// </summary>
    public bool DisableOneSidedCompound { get; }

    /// <summary>
    /// Gets a value indicating whether an alternate reference frame of the base layer searches no compound mode.
    /// </summary>
    public bool SkipAlternateReferenceCompound { get; }

    /// <summary>
    /// Gets a value indicating whether candidate and winner coefficient thresholds differ.
    /// </summary>
    public bool EnableWinnerCoefficientOptimization { get; }

    /// <summary>
    /// Gets the preliminary intra transform restriction: zero for the full set, one for derived types, or two for the default type.
    /// </summary>
    public int IntraTransformTypeSearchLevel { get; }

    /// <summary>
    /// Gets a value indicating whether estimated coefficient rates prune transform types before full search.
    /// </summary>
    public bool EstimateTransformTypeRateDistortion { get; }

    /// <summary>
    /// Gets a value indicating whether candidate evaluation defers transform-size selection to winner evaluation.
    /// </summary>
    public bool DeferTransformSizeSearch { get; }

    /// <summary>
    /// Gets a value indicating whether an inter luma transform search reuses the result of an earlier search of the
    /// same residual in the superblock.
    /// </summary>
    public bool UseMacroblockRateDistortionHash { get; }

    /// <summary>
    /// Gets the number of intra candidates retained for winner evaluation.
    /// </summary>
    public int IntraWinnerCount { get; }

    /// <summary>
    /// Gets a value indicating whether low source variance bypasses intra winner evaluation.
    /// </summary>
    public bool PruneIntraWinnerByVariance { get; }

    /// <summary>
    /// Gets a value indicating whether the eight-bit intra depth model is enabled.
    /// </summary>
    public bool PruneIntraTransformDepth { get; }

    /// <summary>
    /// Gets the filter-intra restriction: zero for all modes, one for derived modes, or two to omit the search.
    /// </summary>
    public int FilterIntraPruneLevel { get; }

    /// <summary>
    /// Gets a value indicating whether an invalid inter-frame DC search also rejects filter-intra search.
    /// </summary>
    public bool SkipFilterIntraAfterInvalidDc { get; }

    /// <summary>
    /// Gets coefficient thresholds for evaluation without a deferred winner stage.
    /// </summary>
    public (uint Distortion, uint Satd) DefaultCoefficientOptimizationThresholds { get; }

    /// <summary>
    /// Gets the candidate coefficient thresholds: normalized distortion in Q8 and transform-scaled SATD.
    /// </summary>
    public (uint Distortion, uint Satd) ModeCoefficientOptimizationThresholds { get; }

    /// <summary>
    /// Gets the winner coefficient thresholds: normalized distortion in Q8 and transform-scaled SATD.
    /// </summary>
    public (uint Distortion, uint Satd) WinnerCoefficientOptimizationThresholds { get; }

    /// <summary>
    /// Gets the skip prediction level for evaluation without a deferred winner stage.
    /// </summary>
    /// <remarks>
    /// Level zero measures every transform block, level one predicts blocks that code no coefficients,
    /// and level two additionally predicts DC-only blocks.
    /// </remarks>
    public int DefaultPredictDcLevel { get; }

    /// <summary>
    /// Gets the skip prediction level of candidate evaluation.
    /// </summary>
    /// <remarks><inheritdoc cref="DefaultPredictDcLevel" path="/remarks"/></remarks>
    public int ModePredictDcLevel { get; }

    /// <summary>
    /// Gets the skip prediction level of winner evaluation.
    /// </summary>
    /// <remarks><inheritdoc cref="DefaultPredictDcLevel" path="/remarks"/></remarks>
    public int WinnerPredictDcLevel { get; }

    /// <summary>
    /// Gets the transform-domain distortion policy for evaluation without a deferred winner stage.
    /// </summary>
    /// <remarks>
    /// Type 0 measures every candidate in the pixel domain. Type 1 measures candidates in the transform domain
    /// and the winner again in the pixel domain. Type 2 keeps the transform-domain measurement. The threshold
    /// is the minimum normalized block error in Q8 for transform-domain measurement.
    /// </remarks>
    public (int Type, uint Threshold) DefaultTransformDomainDistortion { get; }

    /// <summary>
    /// Gets the transform-domain distortion policy for candidate evaluation.
    /// </summary>
    public (int Type, uint Threshold) ModeTransformDomainDistortion { get; }

    /// <summary>
    /// Gets the transform-domain distortion policy for winner evaluation.
    /// </summary>
    public (int Type, uint Threshold) WinnerTransformDomainDistortion { get; }

    /// <summary>
    /// Gets a value indicating whether transform-type search stops once a type quantizes the block to zero.
    /// </summary>
    public bool SkipTransformSearchAfterEmptyBlock { get; }

    /// <summary>
    /// Gets the comparison policy for merging four variance-selected leaves.
    /// </summary>
    /// <returns>The merge comparison level, or zero when merging is disabled.</returns>
    public int GetEstimatedPartitionMergeLevel()
    {
        // All-intra usage enables the merge at speed 8 and turns it off at speed 9. A frame whose shorter side is below
        // 480 samples uses level 2 at both speeds, because the frame-size setting is applied after the speed setting.
        if (this.allIntra)
        {
            return this.Speed < HeifEncodingSpeed.Level8 ? 0
                : this.minimumDimension < 480 ? 2
                : this.Speed == HeifEncodingSpeed.Level8 ? 1 : 0;
        }

        // Real-time usage merges at speed 7 only. Good-quality usage never merges.
        return this.realtime && this.Speed == HeifEncodingSpeed.Level7 ? this.minimumDimension < 480 ? 2 : 3 : 0;
    }

    /// <summary>
    /// Gets the large-partition preference for temporal variance partitioning.
    /// </summary>
    /// <returns>The threshold adjustment level.</returns>
    public int GetVariancePartitionPreference()
        => this.Speed >= HeifEncodingSpeed.Level9 ? 3
            : this.Speed == HeifEncodingSpeed.Level8 ? this.minimumDimension < 360 ? 1 : 0
            : this.minimumDimension < 360 ? 2 : this.minimumDimension < 720 ? 1 : 0;

    /// <summary>
    /// Gets the intra prediction modes admitted during estimated inter search.
    /// </summary>
    /// <param name="blockSize">The coding-block size.</param>
    /// <returns>The bit mask of prediction modes.</returns>
    public int GetEstimatedIntraModeMask(Av1BlockSize blockSize)
    {
        bool dcOnly = this.Speed >= HeifEncodingSpeed.Level9 || blockSize >= Av1BlockSize.Block32x32;
        return dcOnly ? 1 << (int)Av1PredictionMode.DC
            : (1 << (int)Av1PredictionMode.DC) | (1 << (int)Av1PredictionMode.Vertical) | (1 << (int)Av1PredictionMode.Horizontal);
    }

    /// <summary>
    /// Gets the reference pruning level for estimated mode search.
    /// </summary>
    /// <returns>The reference pruning level.</returns>
    public int GetEstimatedReferencePruningLevel()
        => this.Speed >= HeifEncodingSpeed.Level8 ? 2 : 1;

    /// <summary>
    /// Gets whether intra convolution pruning retains unsplit screen-content candidates. The level depends on the screen content state
    /// at the time of the frame-size settings.
    /// </summary>
    /// <returns>The pruning level, or zero when the pruning is off.</returns>
    public int GetIntraPartitionPruningLevel()
        => this.realtime || this.Speed == HeifEncodingSpeed.Level0 ? 0
            : this.FrameSizeScreenContentTools ? this.allIntra && this.Speed >= HeifEncodingSpeed.Level5 ? 1 : 0 : 2;

    /// <summary>
    /// Gets the gate level that compares a prediction-only cost with the best one before an inter transform search.
    /// Boosted frames search every transform.
    /// </summary>
    /// <param name="updateType">The frame update type.</param>
    /// <param name="searchCase">The search that applies the gate.</param>
    /// <returns>The gate level, or zero for no gate.</returns>
    public int GetInterTransformGateLevel(Av1FrameUpdateType updateType, Av1TransformSearchCase searchCase)
    {
        if (this.InterTransformGateLevel == 0 || this.realtime)
        {
            return this.InterTransformGateLevel;
        }

        bool boosted = updateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        if (boosted)
        {
            return 0;
        }

        if (this.Speed >= HeifEncodingSpeed.Level4)
        {
            return searchCase switch
            {
                Av1TransformSearchCase.MotionMode => 5,
                Av1TransformSearchCase.CompoundType => this.Speed >= HeifEncodingSpeed.Level5 ? 5 : 3,
                _ => this.Speed >= HeifEncodingSpeed.Level5 ? 4 : 3
            };
        }

        return this.Speed >= HeifEncodingSpeed.Level3 && updateType != Av1FrameUpdateType.IntermediateAlternate ? 2 : 1;
    }

    /// <summary>
    /// Gets how strongly the mode loop drops single-reference modes of a reference whose predicted-vector SAD is
    /// far above the best of its direction.
    /// </summary>
    /// <param name="updateType">The frame update type.</param>
    /// <returns>The pruning level, from zero for none to four.</returns>
    public int GetPruneSingleReferenceLevel(Av1FrameUpdateType updateType)
    {
        if (this.realtime || this.allIntra || this.Speed < HeifEncodingSpeed.Level2)
        {
            return 0;
        }

        bool boosted = updateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        bool boostedOrIntermediate = boosted || updateType == Av1FrameUpdateType.IntermediateAlternate;
        if (this.Speed >= HeifEncodingSpeed.Level6)
        {
            return boostedOrIntermediate ? 0 : 4;
        }

        if (this.Speed >= HeifEncodingSpeed.Level5)
        {
            return boostedOrIntermediate ? 0 : 3;
        }

        return this.Speed >= HeifEncodingSpeed.Level3 || !boosted ? 2 : 1;
    }

    /// <summary>
    /// Gets which winners of the inter mode search skip the winner refinement. Real-time usage has its own levels. Good quality from
    /// speed 3 uses level four in a frame that is not boosted.
    /// </summary>
    /// <param name="updateType">The frame update type.</param>
    /// <returns>The pruning level, from zero for none to four.</returns>
    public int GetInterWinnerPruningLevel(Av1FrameUpdateType updateType)
    {
        if (this.realtime || this.allIntra || this.intraFrame)
        {
            return this.InterWinnerPruningLevel;
        }

        bool boosted = updateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        return this.Speed >= HeifEncodingSpeed.Level3 && !boosted ? 4 : 0;
    }

    /// <summary>
    /// Gets which partitions search only the references that the square blocks of the superblock picked: one for
    /// the extended partitions, two for the horizontal and vertical partitions as well.
    /// </summary>
    /// <param name="updateType">The frame update type.</param>
    /// <returns>The pruning level, from zero for none to two.</returns>
    public int GetRectangularPartitionReferencePruning(Av1FrameUpdateType updateType)
    {
        bool boosted = updateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        bool boostedOrIntermediate = boosted || updateType == Av1FrameUpdateType.IntermediateAlternate;
        if (this.allIntra)
        {
            return 0;
        }

        if (this.realtime)
        {
            return boosted ? 0 : 1;
        }

        if (this.screenContent || (this.Speed == HeifEncodingSpeed.Level0 ? boosted : this.intraFrame))
        {
            return 0;
        }

        return (this.Speed == HeifEncodingSpeed.Level0 ? boostedOrIntermediate : boosted) ? 1 : 2;
    }

    /// <summary>
    /// Gets how many simple-translation winners of the mode loop search the other motion modes after it. Zero searches every motion
    /// mode inside the loop.
    /// </summary>
    /// <param name="updateType">The frame update type.</param>
    /// <returns>The number of retained winners.</returns>
    public int GetMotionModeWinnerCount(Av1FrameUpdateType updateType)
    {
        if (this.realtime || this.allIntra || this.Speed < HeifEncodingSpeed.Level3)
        {
            return 0;
        }

        bool boosted = updateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        return boosted ? 0 : updateType == Av1FrameUpdateType.IntermediateAlternate ? 10 : 3;
    }

    /// <summary>
    /// Gets the level of the model-based early exit of the recursive transform search. Speed 0 enables it, except at low quantizers.
    /// The quantizer limit depends on the frame size and on the update type.
    /// </summary>
    /// <param name="updateType">The frame update type.</param>
    /// <returns>The level. Zero disables the exit.</returns>
    public int GetModelBasedTransformPruneLevel(Av1FrameUpdateType updateType)
    {
        if (this.realtime || this.Speed != HeifEncodingSpeed.Level0)
        {
            return 0;
        }

        bool boosted = updateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        int threshold = boosted ? 70 : updateType == Av1FrameUpdateType.IntermediateAlternate ? 110 : 140;
        bool disabled = this.minimumDimension < 720 ? this.qIndex <= threshold : this.qIndex <= 128;
        return disabled ? 0 : 1;
    }

    /// <summary>
    /// Gets split suppression, pruning aggressiveness, and unsplit termination for motion-based partition decisions.
    /// </summary>
    /// <param name="screenContent">Whether the frame permits screen-content tools.</param>
    /// <param name="updateType">The frame update type.</param>
    /// <returns>
    /// The split suppression level, the pruning aggressiveness (-1 when off), and whether an unsplit block can end the search.
    /// </returns>
    public (int SplitLevel, int Aggressiveness, bool TerminateNone) GetSimpleMotionPartitionSettings(
        bool screenContent,
        Av1FrameUpdateType updateType)
    {
        if (this.realtime || this.allIntra)
        {
            return (0, -1, false);
        }

        bool boosted = updateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        int splitLevel = screenContent ? 1 : 2;
        bool terminateNone = true;
        int aggressiveness = screenContent ? -1 : Math.Min((int)this.Speed, 5);
        if (this.Speed == HeifEncodingSpeed.Level0)
        {
            int threshold = boosted ? 70 : updateType == Av1FrameUpdateType.IntermediateAlternate ? 110 : 140;
            bool lowResolution = this.minimumDimension < 720 && this.qIndex <= threshold;
            bool highResolution = this.minimumDimension >= 720 && this.qIndex <= 128;
            splitLevel = lowResolution || highResolution ? splitLevel : 0;
            terminateNone = lowResolution || (this.minimumDimension >= 1080 && this.qIndex <= 108);
        }
        else if (this.Speed >= HeifEncodingSpeed.Level3)
        {
            // Level six selects separate quantizer bands for rectangular and square decisions.
            aggressiveness = screenContent ? 0 : this.Speed == HeifEncodingSpeed.Level3 && !boosted
                ? 6 : Math.Min((int)this.Speed, 5);
        }

        return (splitLevel, aggressiveness, terminateNone);
    }

    /// <summary>
    /// Gets the breakout probability threshold for a square block, with a negative value disabling the decision.
    /// </summary>
    /// <param name="blockSize">The square coding-block size, from 8x8 through 128x128.</param>
    /// <returns>The configured probability threshold.</returns>
    public float GetPartitionBreakoutThreshold(Av1BlockSize blockSize)
        => this.partitionBreakoutThresholds[((int)Av1BlockSize.Block128x128 - (int)blockSize) / 3];

    /// <summary>
    /// Resolves the exclusive lower size limit for asymmetric and four-strip partitions.
    /// </summary>
    /// <param name="screenContent">Whether the frame permits screen-content tools.</param>
    /// <param name="intraFrame">Whether the frame has no inter prediction.</param>
    /// <param name="updateType">The frame's reference-update role.</param>
    /// <returns>The block size above which extended partitions can be searched.</returns>
    public Av1BlockSize GetExtendedPartitionThreshold(bool screenContent, bool intraFrame, Av1FrameUpdateType updateType)
    {
        Av1BlockSize threshold = !this.realtime && this.Speed >= HeifEncodingSpeed.Level5 && !this.FrameSizeScreenContentTools
            ? Av1BlockSize.Block16x16 : Av1BlockSize.Block8x8;

        if (this.realtime || this.Speed < HeifEncodingSpeed.Level2)
        {
            return threshold;
        }

        // Quantizer overrides follow the base speed policy. Key, golden, and alternate updates
        // retain more shape choices than ordinary displayed frames at the same quantizer.
        bool boosted = intraFrame || updateType is Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        int level = Math.Min(4, (int)this.Speed - 2);
        bool disable;
        if (level <= 1)
        {
            int quantizerThreshold = screenContent ? 50 : level == 0 ? this.minimumDimension < 480 ? 70 : 80 : 100;
            disable = this.qIndex <= quantizerThreshold && !boosted;
        }
        else if (level == 2)
        {
            disable = this.qIndex <= (boosted ? 80 : 120) && !intraFrame;
        }
        else if (level == 3)
        {
            disable = this.minimumDimension < 480 ||
                (this.minimumDimension < 720 && !intraFrame && !screenContent) ||
                (this.qIndex <= (boosted ? 100 : 160) && !intraFrame);
        }
        else
        {
            disable = true;
        }

        return disable ? Av1BlockSize.Block128x128 : threshold;
    }

    /// <summary>
    /// Gets whether extended search follows the current rectangular winner for the frame's content.
    /// </summary>
    /// <param name="screenContent">Whether the frame permits screen-content tools.</param>
    /// <param name="intraFrame">Whether the frame has no inter prediction.</param>
    /// <returns>Whether the winning partition limits extended search.</returns>
    public bool RestrictExtendedPartitionsToWinner(bool screenContent, bool intraFrame)
        => !this.realtime && this.Speed >= HeifEncodingSpeed.Level5 && this.minimumDimension >= 720 && !screenContent && !intraFrame;

    /// <summary>
    /// Gets whether an asymmetric sub-block keeps the luma mode of the candidate that covered
    /// the same samples, instead of searching every mode again.
    /// </summary>
    /// <param name="intraFrame">Whether the frame has no inter prediction.</param>
    /// <returns>Whether the asymmetric sub-block searches one luma mode only.</returns>
    public bool ReuseBestPredictionForAsymmetricPartitions(bool intraFrame)
    {
        if (this.realtime)
        {
            return false;
        }

        return this.allIntra
            ? this.Speed >= HeifEncodingSpeed.Level1
            : this.Speed >= HeifEncodingSpeed.Level2 && !intraFrame;
    }

    /// <summary>
    /// Resolves whether 8x8 splits follow neighboring block sizes or are omitted unconditionally.
    /// </summary>
    /// <param name="screenContent">Whether the frame permits screen-content tools.</param>
    /// <returns>Zero for full search, one for neighbor-based pruning, or two for omitting splits.</returns>
    public int GetSub8PartitionPruningLevel(bool screenContent)
    {
        if (this.realtime || (screenContent && this.qIndex < 128 && this.minimumDimension <= 480))
        {
            return 0;
        }

        return this.allIntra
            ? this.Speed >= HeifEncodingSpeed.Level6 && !this.FrameSizeScreenContentTools ? 1 : 0
            : this.Speed >= HeifEncodingSpeed.Level5 ? this.FrameSizeScreenContentTools ? 1 : 2 : 0;
    }

    /// <summary>
    /// Gets the references that the global motion search of an inter frame visits. Real-time usage does not search.
    /// </summary>
    /// <param name="boosted">Whether the frame is a key, golden or alternate reference frame.</param>
    /// <returns>The search type.</returns>
    public Av1GlobalMotionSearchType GetGlobalMotionSearchType(bool boosted)
    {
        if (this.realtime)
        {
            return Av1GlobalMotionSearchType.Disabled;
        }

        return boosted ? Av1GlobalMotionSearchType.SkipLast2Last3Alternate2 : Av1GlobalMotionSearchType.ClosestReferencesOnly;
    }

    /// <summary>
    /// Returns whether the global motion search stops at the first reference without a usable model.
    /// </summary>
    /// <param name="boosted">Whether the frame is a key, golden or alternate reference frame.</param>
    /// <returns><see langword="true"/> when the search stops early.</returns>
    public bool PrunesGlobalMotionReferences(bool boosted) => this.Speed >= HeifEncodingSpeed.Level3 || !boosted;

    /// <summary>
    /// Resolves the inclusive size range for rectangular partition searches.
    /// </summary>
    /// <param name="screenContent">Whether the frame permits screen-content tools.</param>
    /// <param name="intraFrame">Whether the frame has no inter prediction.</param>
    /// <param name="updateType">The frame's reference-update role.</param>
    /// <param name="blockQIndex">The quantizer index of the block, which differs from the frame's under delta q.</param>
    /// <returns>The inclusive minimum and maximum square parent sizes.</returns>
    public (Av1BlockSize Minimum, Av1BlockSize Maximum) GetRectangularPartitionRange(
        bool screenContent,
        bool intraFrame,
        Av1FrameUpdateType updateType,
        int blockQIndex)
    {
        Av1BlockSize minimum = Av1BlockSize.Block4x4;
        Av1BlockSize maximum = Av1BlockSize.Block128x128;
        if (this.realtime)
        {
            return (minimum, maximum);
        }

        bool boosted = intraFrame || updateType is Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        if (this.Speed >= HeifEncodingSpeed.Level3 && !boosted && this.minimumDimension >= 480 &&
            this.qIndex <= (this.Speed <= HeifEncodingSpeed.Level4 ? 65 : 80))
        {
            maximum = Av1BlockSize.Block8x8;
        }

        if (!screenContent)
        {
            if (this.Speed >= HeifEncodingSpeed.Level6 && (this.allIntra || !boosted))
            {
                // Each quantizer band reduces the excluded square sizes by one doubling.
                // Square sizes are three enum entries apart because rectangular sizes lie between them.
                minimum = (Av1BlockSize)Math.Max(
                    (int)Av1BlockSize.Block4x4,
                    (int)Av1BlockSize.Block32x32 - ((blockQIndex * 3 / 256) * 3));
            }
            else if (!this.allIntra && !intraFrame && this.Speed is >= HeifEncodingSpeed.Level4 and < HeifEncodingSpeed.Level6 &&
                blockQIndex < 35)
            {
                // At speeds 4 and 5, a low block quantizer in an inter frame removes rectangular search below 16x16. At speed 6, a
                // boosted frame keeps the full range.
                minimum = Av1BlockSize.Block16x16;
            }
        }

        return (minimum, maximum);
    }

    /// <summary>
    /// Gets the permitted chroma prediction modes for the maximum chroma transform.
    /// </summary>
    /// <param name="transformSize">The maximum chroma transform size of the block.</param>
    /// <returns>A bit mask with one bit for each <see cref="Av1ChromaPredictionMode"/> value.</returns>
    public ushort GetChromaModeMask(Av1TransformSize transformSize)
    {
        // Bit 0 is DC, bits 1 and 2 are vertical and horizontal, and bit 13 is chroma-from-luma. 0x3FFF allows all fourteen modes.
        Av1TransformSize squareSize = transformSize.GetSquareUpSize();
        if (this.chromaModeRestriction == 3)
        {
            return 0x2001;
        }

        if (this.chromaModeRestriction == 2 && squareSize >= Av1TransformSize.Size32x32)
        {
            return 0x0001;
        }

        return this.chromaModeRestriction != 0 && squareSize >= Av1TransformSize.Size16x16 ? (ushort)0x2007 : (ushort)0x3FFF;
    }
}
