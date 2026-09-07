// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Resolves frame-dependent motion-search policies before block traversal.
/// </summary>
internal readonly struct Av1MotionSearchSettings
{
    private readonly HeifEncodingSpeed speed;
    private readonly FullPixelSearchMethod fullPixelMethod;
    private readonly int fasterSearchMinimumDimension;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1MotionSearchSettings"/> struct.
    /// </summary>
    /// <param name="speed">The encoding speed.</param>
    /// <param name="intraOnly">Whether every frame is coded independently.</param>
    /// <param name="frameSize">The visible frame dimensions.</param>
    /// <param name="qIndex">The base quantizer index.</param>
    /// <param name="boostedFrame">Whether this is a key, golden, or alternate-reference frame with boosted quality.</param>
    /// <param name="screenContent">Whether the content classification identifies graphics or screen content.</param>
    public Av1MotionSearchSettings(
        HeifEncodingSpeed speed,
        bool intraOnly,
        Size frameSize,
        int qIndex,
        bool boostedFrame,
        bool screenContent)
    {
        this.speed = speed;
        this.fullPixelMethod = FullPixelSearchMethod.NStep;
        this.FractionalMethod = FractionalSearchMethod.TwoLevelTree;
        this.FractionalIterationsPerStep = 2;
        this.FractionalInterpolationTaps = 8;
        this.SimpleMotionPrecision = SearchPrecision.EighthSample;
        this.SecondCandidateSelection = CandidateSelection.RateDistortion;
        this.AllowIntraBlockCopy = true;
        this.MeshErrorThreshold = 1 << (screenContent ? 20 : 25);

        // Apply coding-mode choices before resolution and quantizer overrides. Reversing that order can
        // incorrectly suppress a second motion candidate or replace a quantizer-selected search pattern.
        if (speed >= HeifEncodingSpeed.Level1)
        {
            this.MeshErrorThreshold <<= 1;
        }

        if (intraOnly)
        {
            this.PruneIntraBlockCopyHashCandidates = speed >= HeifEncodingSpeed.Level1;
            this.AutomaticStepSizeLevel = speed >= HeifEncodingSpeed.Level2 ? 1 : 0;
            this.LimitFullPixelStartingCandidates = speed >= HeifEncodingSpeed.Level3;
            if (speed >= HeifEncodingSpeed.Level3)
            {
                this.fullPixelMethod = FullPixelSearchMethod.Diamond;
            }

            if (speed >= HeifEncodingSpeed.Level4)
            {
                this.FractionalMethod = FractionalSearchMethod.MorePrunedTree;
                this.SimpleMotionPrecision = SearchPrecision.HalfSample;
                this.ReduceSearchRange = true;
                this.LimitIntraBlockCopyHashBlockSize = true;
            }

            this.MeshPruningLevel = speed >= HeifEncodingSpeed.Level5 ? 2 : 0;
            if (speed >= HeifEncodingSpeed.Level6)
            {
                this.fasterSearchMinimumDimension = 32;
                this.UseFastIntraBlockCopySearch = true;
            }
        }
        else
        {
            this.DisableExtensiveJointSearch = true;
            if (speed >= HeifEncodingSpeed.Level1)
            {
                this.UseRefiningObmcSearch = true;
                this.FractionalInterpolationTaps = 4;
            }

            if (speed >= HeifEncodingSpeed.Level2)
            {
                this.SimpleMotionPrecision = SearchPrecision.QuarterSample;
                this.FractionalIterationsPerStep = 1;
                this.ReduceSearchRange = true;
            }

            if (speed >= HeifEncodingSpeed.Level3)
            {
                this.FractionalMethod = FractionalSearchMethod.PrunedTree;
                this.fullPixelMethod = FullPixelSearchMethod.Diamond;
                this.SecondCandidateSelection = CandidateSelection.FirstOnly;
                this.MeshPruningLevel = 1;
                this.AllowIntraBlockCopy = false;
            }

            if (speed >= HeifEncodingSpeed.Level4)
            {
                this.FractionalMethod = FractionalSearchMethod.MorePrunedTree;
                this.SimpleMotionPrecision = SearchPrecision.HalfSample;
                this.MeshPruningLevel = 2;
            }

            this.UseDiamondWarpSearch = speed >= HeifEncodingSpeed.Level5;
            if (speed >= HeifEncodingSpeed.Level6)
            {
                this.SimpleMotionPrecision = SearchPrecision.Integer;
            }
        }

        // Resolution classes use the shorter dimension, so rotating a frame does not change its class.
        int minimumDimension = Math.Min(frameSize.Width, frameSize.Height);
        bool is720pOrLarger = minimumDimension >= 720;
        this.DownsampledSadLevel = is720pOrLarger ? 2 : 0;
        if (!intraOnly)
        {
            this.ReferenceCandidatePruningLevel = speed >= HeifEncodingSpeed.Level5 ? 4
                : speed >= HeifEncodingSpeed.Level4 && minimumDimension <= 480 ? 3
                : speed >= HeifEncodingSpeed.Level3 ? 2
                : speed >= HeifEncodingSpeed.Level1 ? 1 : 0;

            if (speed >= HeifEncodingSpeed.Level2)
            {
                this.AutomaticStepSizeLevel = is720pOrLarger ? 1 : 2;
                this.SecondCandidateSelection = !is720pOrLarger
                    ? CandidateSelection.Variance
                    : boostedFrame ? CandidateSelection.RateDistortion : CandidateSelection.FirstOnly;
            }

            if (speed >= HeifEncodingSpeed.Level4 && minimumDimension < 480)
            {
                this.StartCandidatePruningLevel = boostedFrame ? 0 : 1;
            }

            if (speed >= HeifEncodingSpeed.Level5)
            {
                this.StartCandidatePruningLevel = boostedFrame ? 0 : 1;
                if (!is720pOrLarger)
                {
                    this.DownsampledSadLevel = 1;
                }
            }

            if (speed >= HeifEncodingSpeed.Level6)
            {
                this.StartCandidatePruningLevel = boostedFrame ? 0 : 2;
                this.fasterSearchMinimumDimension = is720pOrLarger ? 128 : 64;
            }
        }

        // Coarse quantization selects a less expensive full-pixel pattern even at the slower speed levels.
        // These thresholds apply to the coding pass; first-pass statistics use a separate configuration.
        if (speed <= HeifEncodingSpeed.Level2)
        {
            int coarseThreshold;
            int intermediateThreshold;
            if (is720pOrLarger)
            {
                coarseThreshold = speed == HeifEncodingSpeed.Level2 ? 200 : 255;
                intermediateThreshold = speed == HeifEncodingSpeed.Level0 ? 200 : -1;
            }
            else
            {
                coarseThreshold = speed == HeifEncodingSpeed.Level0 ? 200 : 170;
                intermediateThreshold = speed switch
                {
                    HeifEncodingSpeed.Level0 => 70,
                    HeifEncodingSpeed.Level1 => 50,
                    _ => 40
                };
            }

            if (qIndex > coarseThreshold)
            {
                this.fullPixelMethod = is720pOrLarger
                    ? FullPixelSearchMethod.Diamond
                    : FullPixelSearchMethod.ClampedDiamond;
            }
            else if (qIndex > intermediateThreshold)
            {
                this.fullPixelMethod = FullPixelSearchMethod.EightPointNStep;
            }
        }
    }

    /// <summary>
    /// The full-pixel search pattern.
    /// </summary>
    public enum FullPixelSearchMethod
    {
        /// <summary>
        /// Repeated shrinking diamond searches.
        /// </summary>
        Diamond,

        /// <summary>
        /// Searches with eight or twelve sites at progressively smaller radii.
        /// </summary>
        NStep,

        /// <summary>
        /// Searches with eight sites at every radius.
        /// </summary>
        EightPointNStep,

        /// <summary>
        /// Diamond search with repeated, bounded initial radii.
        /// </summary>
        ClampedDiamond,

        /// <summary>
        /// Hexagonal search followed by local refinement.
        /// </summary>
        Hexagon,

        /// <summary>
        /// Large diamond search followed by local refinement.
        /// </summary>
        BigDiamond,

        /// <summary>
        /// Diamond search beginning at a reduced scale.
        /// </summary>
        FastDiamond,

        /// <summary>
        /// Large diamond search beginning at a reduced scale.
        /// </summary>
        FastBigDiamond,

        /// <summary>
        /// Diamond search with only the smallest scales.
        /// </summary>
        VeryFastDiamond
    }

    /// <summary>
    /// The fractional-pixel search traversal.
    /// </summary>
    public enum FractionalSearchMethod
    {
        /// <summary>
        /// Cardinal and selected diagonal searches with a second refinement level.
        /// </summary>
        TwoLevelTree,

        /// <summary>
        /// Pruned tree search using the integer cost neighborhood when available.
        /// </summary>
        PrunedTree,

        /// <summary>
        /// Pruned tree search with additional quadratic cost-surface prediction.
        /// </summary>
        MorePrunedTree
    }

    /// <summary>
    /// The finest displacement examined by a fractional search.
    /// </summary>
    public enum SearchPrecision
    {
        /// <summary>
        /// One eighth of a luma sample.
        /// </summary>
        EighthSample,

        /// <summary>
        /// One quarter of a luma sample.
        /// </summary>
        QuarterSample,

        /// <summary>
        /// One half of a luma sample.
        /// </summary>
        HalfSample,

        /// <summary>
        /// Whole luma samples.
        /// </summary>
        Integer
    }

    /// <summary>
    /// The comparison used after refining a second motion candidate.
    /// </summary>
    public enum CandidateSelection
    {
        /// <summary>
        /// Compare estimated transform rate and distortion.
        /// </summary>
        RateDistortion,

        /// <summary>
        /// Compare prediction variance and motion-vector rate.
        /// </summary>
        Variance,

        /// <summary>
        /// Refine only the first full-pixel winner.
        /// </summary>
        FirstOnly
    }

    /// <summary>
    /// Gets the adaptation level for the initial full-pixel step.
    /// </summary>
    public int AutomaticStepSizeLevel { get; }

    /// <summary>
    /// Gets the fractional search traversal.
    /// </summary>
    public FractionalSearchMethod FractionalMethod { get; }

    /// <summary>
    /// Gets the refinement iterations at each fractional precision.
    /// </summary>
    public int FractionalIterationsPerStep { get; }

    /// <summary>
    /// Gets the interpolation tap count used during fractional search.
    /// </summary>
    public int FractionalInterpolationTaps { get; }

    /// <summary>
    /// Gets the finest precision used by preliminary simple-motion analysis.
    /// </summary>
    public SearchPrecision SimpleMotionPrecision { get; }

    /// <summary>
    /// Gets the variance threshold for following a stepped search with a mesh search.
    /// </summary>
    public int MeshErrorThreshold { get; }

    /// <summary>
    /// Gets a value indicating whether earlier reference-index results restrict subsequent search ranges.
    /// </summary>
    public bool ReduceSearchRange { get; }

    /// <summary>
    /// Gets the level used to prune mesh search based on motion displacement.
    /// </summary>
    public int MeshPruningLevel { get; }

    /// <summary>
    /// Gets a value indicating whether overlapped prediction uses local full-pixel refinement.
    /// </summary>
    public bool UseRefiningObmcSearch { get; }

    /// <summary>
    /// Gets a value indicating whether full-pixel search omits additional temporal-analysis starting candidates.
    /// </summary>
    public bool LimitFullPixelStartingCandidates { get; }

    /// <summary>
    /// Gets a value indicating whether intra-block-copy motion search is enabled.
    /// </summary>
    public bool AllowIntraBlockCopy { get; }

    /// <summary>
    /// Gets a value indicating whether block-copy hash search stops after the first 64 candidates.
    /// </summary>
    public bool PruneIntraBlockCopyHashCandidates { get; }

    /// <summary>
    /// Gets a value indicating whether block copy restricts geometry and uses pixel search only after hash search fails.
    /// </summary>
    public bool UseFastIntraBlockCopySearch { get; }

    /// <summary>
    /// Gets a value indicating whether block-copy hashing is restricted to 4x4 and 8x8 blocks.
    /// </summary>
    public bool LimitIntraBlockCopyHashBlockSize { get; }

    /// <summary>
    /// Gets the row-subsampling policy: zero disables it, one checks the starting SAD, and two checks only the final SAD.
    /// </summary>
    public int DownsampledSadLevel { get; }

    /// <summary>
    /// Gets a value indicating whether compound motion omits the extensive joint refinement search.
    /// </summary>
    public bool DisableExtensiveJointSearch { get; }

    /// <summary>
    /// Gets how two fractional motion candidates are compared.
    /// </summary>
    public CandidateSelection SecondCandidateSelection { get; }

    /// <summary>
    /// Gets a value indicating whether zero, four, or eight neighboring start/reference positions can reuse an earlier search.
    /// </summary>
    public int StartCandidatePruningLevel { get; }

    /// <summary>
    /// Gets the pruning level applied across dynamic reference-vector choices after motion search.
    /// </summary>
    public int ReferenceCandidatePruningLevel { get; }

    /// <summary>
    /// Gets a value indicating whether warped-motion refinement uses a diamond instead of a square.
    /// </summary>
    public bool UseDiamondWarpSearch { get; }

    /// <summary>
    /// Gets the full-pixel method for the current block geometry.
    /// </summary>
    /// <param name="blockSize">The prediction block size.</param>
    /// <returns>The frame-selected method after its block-size override.</returns>
    public FullPixelSearchMethod GetFullPixelMethod(Av1BlockSize blockSize)
    {
        int minimumDimension = Math.Min(blockSize.GetWidth(), blockSize.GetHeight());
        if (this.fasterSearchMinimumDimension == 0 || minimumDimension < this.fasterSearchMinimumDimension)
        {
            return this.fullPixelMethod;
        }

        return this.fullPixelMethod switch
        {
            FullPixelSearchMethod.NStep or FullPixelSearchMethod.EightPointNStep => FullPixelSearchMethod.Diamond,
            FullPixelSearchMethod.Diamond or FullPixelSearchMethod.ClampedDiamond => FullPixelSearchMethod.BigDiamond,
            FullPixelSearchMethod.BigDiamond => FullPixelSearchMethod.Hexagon,
            FullPixelSearchMethod.Hexagon => FullPixelSearchMethod.FastDiamond,
            FullPixelSearchMethod.FastDiamond => FullPixelSearchMethod.VeryFastDiamond,
            _ => this.fullPixelMethod
        };
    }

    /// <summary>
    /// Gets the successive mesh ranges and sampling intervals in full luma samples.
    /// </summary>
    /// <param name="intraBlockCopy">Whether the search references the current reconstruction.</param>
    /// <returns>Four range/interval pairs. Traversal ends after the first interval of one.</returns>
    public ReadOnlySpan<int> GetMeshPattern(bool intraBlockCopy)
    {
        // The alternating range/interval layout is immutable static storage. A frame or candidate does not
        // allocate a pattern, and an interval of one terminates refinement before unused trailing entries.
        if (intraBlockCopy)
        {
            return this.speed switch
            {
                <= HeifEncodingSpeed.Level1 => [256, 1, 256, 1, 0, 0, 0, 0],
                <= HeifEncodingSpeed.Level3 => [64, 1, 64, 1, 0, 0, 0, 0],
                _ => [64, 4, 16, 1, 0, 0, 0, 0]
            };
        }

        return this.speed switch
        {
            <= HeifEncodingSpeed.Level1 => [64, 8, 28, 4, 15, 1, 7, 1],
            HeifEncodingSpeed.Level2 => [64, 8, 14, 2, 7, 1, 7, 1],
            _ => [64, 16, 24, 8, 12, 4, 7, 1]
        };
    }
}
