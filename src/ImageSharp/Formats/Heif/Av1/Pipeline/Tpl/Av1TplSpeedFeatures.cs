// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchSettings;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The speed features that the temporal dependency model reads in good-quality usage. The model runs before the frame
/// level speed features are set, so it sees the features the previous coded frame left: the size-independent and
/// size-dependent speed choices, and the quantizer-dependent overrides at the previous frame's quantizer.
/// Reference: TPL_SPEED_FEATURES with the selective_ref_frame field of INTER_MODE_SPEED_FEATURES.
/// </summary>
internal readonly struct Av1TplSpeedFeatures
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplSpeedFeatures"/> struct. Reference: init_tpl_sf(),
    /// set_good_speed_features_framesize_independent(), set_good_speed_features_framesize_dependent() and
    /// av1_set_speed_features_qindex_dependent().
    /// </summary>
    /// <param name="speed">The good-quality speed, zero to six.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="lastQIndex">
    /// The base quantizer index of the most recent av1_set_speed_features_qindex_dependent() call, that is of the
    /// previous coded frame, or -1 when no frame has been coded yet.
    /// </param>
    public Av1TplSpeedFeatures(HeifEncodingSpeed speed, int width, int height, int lastQIndex)
    {
        int minimumDimension = Math.Min(width, height);
        bool is480pOrLarger = minimumDimension >= 480;
        bool is720pOrLarger = minimumDimension >= 720;
        bool is1080pOrLarger = minimumDimension >= 1080;

        // init_tpl_sf() defaults.
        int gopLengthDecisionMethod = 1;
        bool pruneIntraModes = false;
        int pruneStartingMotionVector = 0;
        int reduceFirstStepSize = 0;
        int skipAlikeStartingMotionVector = 0;
        SearchPrecision subpelForceStop = SearchPrecision.EighthSample;
        FullPixelSearchMethod searchMethod = FullPixelSearchMethod.NStep;
        bool pruneReferenceFrames = false;
        bool allowCompoundPrediction = true;
        bool lumaOnlyRateDistortion = false;
        int useSadForModeDecision = 0;
        bool reduceNumberOfFrames = false;
        int selectiveReferenceFrame;

        // set_good_speed_features_framesize_independent().
        searchMethod = FullPixelSearchMethod.EightPointNStep;
        selectiveReferenceFrame = 1;
        if (speed >= HeifEncodingSpeed.Level1)
        {
            skipAlikeStartingMotionVector = 1;
            selectiveReferenceFrame = 2;
        }

        if (speed >= HeifEncodingSpeed.Level2)
        {
            selectiveReferenceFrame = 3;
            pruneStartingMotionVector = 1;
            searchMethod = FullPixelSearchMethod.Diamond;
            allowCompoundPrediction = false;
            pruneReferenceFrames = true;
        }

        if (speed >= HeifEncodingSpeed.Level3)
        {
            selectiveReferenceFrame = 5;
            pruneStartingMotionVector = 2;
            skipAlikeStartingMotionVector = 2;
            pruneIntraModes = true;
            reduceFirstStepSize = 6;
            subpelForceStop = SearchPrecision.QuarterSample;
        }

        if (speed >= HeifEncodingSpeed.Level4)
        {
            subpelForceStop = SearchPrecision.HalfSample;
            searchMethod = FullPixelSearchMethod.FastBigDiamond;
            useSadForModeDecision = 1;
        }

        if (speed >= HeifEncodingSpeed.Level5)
        {
            pruneStartingMotionVector = 3;
            lumaOnlyRateDistortion = true;
            subpelForceStop = SearchPrecision.Integer;
            gopLengthDecisionMethod = 2;
            useSadForModeDecision = 2;
        }

        if (speed >= HeifEncodingSpeed.Level6)
        {
            selectiveReferenceFrame = 6;
            gopLengthDecisionMethod = 3;
        }

        // set_good_speed_features_framesize_dependent().
        if (speed >= HeifEncodingSpeed.Level5 && is480pOrLarger)
        {
            reduceNumberOfFrames = true;
        }

        // av1_set_speed_features_qindex_dependent(): coarse quantizers select a cheaper full-pixel pattern at the
        // slower speeds. It has not run before the first frame is coded.
        if (lastQIndex >= 0 && speed <= HeifEncodingSpeed.Level2)
        {
            int aggressiveness = (int)speed;
            if (!is720pOrLarger)
            {
                ReadOnlySpan<int> coarseThresholds = [200, 170, 170];
                if (lastQIndex > coarseThresholds[aggressiveness])
                {
                    searchMethod = FullPixelSearchMethod.ClampedDiamond;
                }
            }
            else
            {
                ReadOnlySpan<int> coarseThresholds = [Av1Constants.MaxQ, Av1Constants.MaxQ, 200];
                ReadOnlySpan<int> intermediateThresholds = [200, -1, -1];
                if (lastQIndex > coarseThresholds[aggressiveness])
                {
                    searchMethod = FullPixelSearchMethod.Diamond;
                }
                else if (lastQIndex > intermediateThresholds[aggressiveness])
                {
                    searchMethod = aggressiveness == 0 ? FullPixelSearchMethod.EightPointNStep : FullPixelSearchMethod.Diamond;
                }
            }

            if (speed == HeifEncodingSpeed.Level0 && is1080pOrLarger && lastQIndex <= 108)
            {
                selectiveReferenceFrame = 2;
            }
        }

        this.GopLengthDecisionMethod = gopLengthDecisionMethod;
        this.PruneIntraModes = pruneIntraModes;
        this.PruneStartingMotionVector = pruneStartingMotionVector;
        this.ReduceFirstStepSize = reduceFirstStepSize;
        this.SkipAlikeStartingMotionVector = skipAlikeStartingMotionVector;
        this.SubpelForceStop = subpelForceStop;
        this.SearchMethod = searchMethod;
        this.PruneReferenceFrames = pruneReferenceFrames;
        this.AllowCompoundPrediction = allowCompoundPrediction;
        this.LumaOnlyRateDistortion = lumaOnlyRateDistortion;
        this.UseSadForModeDecision = useSadForModeDecision;
        this.ReduceNumberOfFrames = reduceNumberOfFrames;
        this.SelectiveReferenceFrame = selectiveReferenceFrame;
    }

    /// <summary>
    /// Gets the method that decides the golden group length from the model. Reference: gop_length_decision_method.
    /// </summary>
    public int GopLengthDecisionMethod { get; }

    /// <summary>
    /// Gets a value indicating whether the intra search stops before D45_PRED. Reference: prune_intra_modes.
    /// </summary>
    public bool PruneIntraModes { get; }

    /// <summary>
    /// Gets the pruning level of the starting motion vectors. Reference: prune_starting_mv.
    /// </summary>
    public int PruneStartingMotionVector { get; }

    /// <summary>
    /// Gets the number of excluded initial search stages. Reference: reduce_first_step_size.
    /// </summary>
    public int ReduceFirstStepSize { get; }

    /// <summary>
    /// Gets the level that skips starting vectors close to earlier ones. Reference: skip_alike_starting_mv.
    /// </summary>
    public int SkipAlikeStartingMotionVector { get; }

    /// <summary>
    /// Gets the finest motion precision of the model search. Reference: subpel_force_stop.
    /// </summary>
    public SearchPrecision SubpelForceStop { get; }

    /// <summary>
    /// Gets the full-pixel search method. Reference: search_method.
    /// </summary>
    public FullPixelSearchMethod SearchMethod { get; }

    /// <summary>
    /// Gets a value indicating whether the selective reference pruning removes references of non-eligible frames.
    /// Reference: prune_ref_frames_in_tpl.
    /// </summary>
    public bool PruneReferenceFrames { get; }

    /// <summary>
    /// Gets a value indicating whether compound prediction is searched. Reference: allow_compound_pred.
    /// </summary>
    public bool AllowCompoundPrediction { get; }

    /// <summary>
    /// Gets a value indicating whether only luma contributes rate and distortion. Reference: use_y_only_rate_distortion.
    /// </summary>
    public bool LumaOnlyRateDistortion { get; }

    /// <summary>
    /// Gets the level at which mode decisions compare absolute differences. Reference: use_sad_for_mode_decision.
    /// </summary>
    public int UseSadForModeDecision { get; }

    /// <summary>
    /// Gets a value indicating whether leaf frames are skipped. Reference: reduce_num_frames.
    /// </summary>
    public bool ReduceNumberOfFrames { get; }

    /// <summary>
    /// Gets the selective reference frame level. Reference: inter_sf.selective_ref_frame.
    /// </summary>
    public int SelectiveReferenceFrame { get; }
}
