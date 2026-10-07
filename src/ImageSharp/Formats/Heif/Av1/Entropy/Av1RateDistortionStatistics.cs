// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Holds the rate, distortion, and rounded cost of an encoder candidate.
/// </summary>
internal struct Av1RateDistortionStatistics
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1RateDistortionStatistics"/> struct.
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier for the current block.</param>
    /// <param name="rate">The estimated syntax rate in 1/512-bit units.</param>
    /// <param name="distortion">The candidate distortion.</param>
    public Av1RateDistortionStatistics(int rateMultiplier, int rate, long distortion)
    {
        this.Rate = rate;
        this.Distortion = distortion;
        this.Cost = Av1RateDistortion.GetCost(rateMultiplier, rate, distortion);
        this.LumaCost = long.MaxValue;
    }

    /// <summary>
    /// Gets the sentinel for a candidate that cannot win a cost comparison.
    /// </summary>
    public static Av1RateDistortionStatistics Invalid => new()
    {
        Rate = int.MaxValue,
        Distortion = long.MaxValue,
        Cost = long.MaxValue,
        LumaCost = long.MaxValue,
        TransformCost = long.MaxValue
    };

    /// <summary>
    /// Gets the estimated syntax rate in 1/512-bit units.
    /// </summary>
    public int Rate { get; private set; }

    /// <summary>
    /// Gets the candidate distortion.
    /// </summary>
    public long Distortion { get; private set; }

    /// <summary>
    /// Gets or sets the residual syntax rate, including skip syntax for inter candidates.
    /// </summary>
    public int ResidualRate { get; set; }

    /// <summary>
    /// Gets or sets the distortion before residual coding.
    /// </summary>
    public long PredictionDistortion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any transform retains nonzero coefficients.
    /// </summary>
    public bool HasCoefficients { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the searched residual is skippable before the block's skip-cost
    /// comparison: every transform is empty, or a recursive luma transform search found the skip cost no higher.
    /// </summary>
    /// <remarks>This is the <c>skip_txfm</c> of the search's <c>RD_STATS</c>.</remarks>
    public bool AllTransformsEmpty { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the luma residual was predicted to quantize to nothing before any
    /// transform search. Reference: the skip_txfm result that set_skip_txfm() leaves.
    /// </summary>
    public bool SkipPredicted { get; set; }

    /// <summary>
    /// Gets or sets the luma cost used to compare prediction families, including prediction and skip syntax.
    /// </summary>
    public long LumaCost { get; set; }

    /// <summary>
    /// Gets or sets the cost of the transform choice alone: its coefficients, the non-skip flag and the
    /// transform-size syntax, without the prediction syntax. A later transform depth is bounded by it.
    /// Reference: the rd that uniform_txfm_yrd() returns.
    /// </summary>
    public long TransformCost { get; set; }

    /// <summary>
    /// Gets or sets the candidate comparison cost, including any mode-selection adjustment.
    /// </summary>
    public long Cost { get; set; }

    /// <summary>
    /// Adds a valid candidate's rate and distortion and updates the combined cost.
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier for the combined candidate.</param>
    /// <param name="other">The valid candidate to add.</param>
    public void Add(int rateMultiplier, Av1RateDistortionStatistics other)
    {
        // Round the combined rate only once. Adding the already rounded child costs can change
        // partition and inter/intra decisions even when both children have the same reconstruction.
        this.Rate += other.Rate;
        this.ResidualRate += other.ResidualRate;
        this.Distortion += other.Distortion;
        this.PredictionDistortion += other.PredictionDistortion;
        this.HasCoefficients |= other.HasCoefficients;
        this.Cost = Av1RateDistortion.GetCost(rateMultiplier, this.Rate, this.Distortion);
    }

    /// <summary>
    /// Adds an eighth to the distortion and the luma cost of a valid inter prediction, and prices it again. The image
    /// tune favors intra prediction in this way. Reference: the AOM_TUNE_IQ branches of adjust_cost() and
    /// adjust_rdcost() in motion_mode_rd(), whose caller prices the adjusted distortion with RDCOST.
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier for the current block.</param>
    public void AddInterPredictionBias(int rateMultiplier)
    {
        this.Distortion += this.Distortion >> 3;
        this.Cost = Av1RateDistortion.GetCost(rateMultiplier, this.Rate, this.Distortion);
        if (this.LumaCost != long.MaxValue)
        {
            this.LumaCost += this.LumaCost >> 3;
        }
    }

    /// <summary>
    /// Adds an eighth to the distortion and to the cost as it stands, which need not be the price of the rate and
    /// distortion. Reference: the AOM_TUNE_IQ branch of adjust_rdcost().
    /// </summary>
    public void AddInterCostBias()
    {
        this.Distortion += this.Distortion >> 3;
        this.Cost += this.Cost >> 3;
    }

    /// <summary>
    /// Adds the sharpness 3 offset of a prediction smoother than its source to the distortion, prices the cost
    /// again, and adds the priced offset to a valid luma cost. Reference: the sharpness branches of adjust_cost()
    /// and adjust_rdcost() in motion_mode_rd().
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier of the block.</param>
    /// <param name="offset">The amount by which the source variance measure exceeds the prediction's.</param>
    public void AddPredictionSmoothingOffset(int rateMultiplier, long offset)
    {
        this.AddSmoothingOffset(rateMultiplier, offset);
        if (this.LumaCost != long.MaxValue)
        {
            this.LumaCost += Av1RateDistortion.GetCost(rateMultiplier, 0, offset);
        }
    }

    /// <summary>
    /// Adds the sharpness 3 offset to the distortion and prices the cost again. Reference: the sharpness branch of
    /// adjust_rdcost().
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier of the block.</param>
    /// <param name="offset">The amount by which the source variance measure exceeds the block samples'.</param>
    public void AddSmoothingOffset(int rateMultiplier, long offset)
    {
        this.Distortion += offset;
        this.Cost = Av1RateDistortion.GetCost(rateMultiplier, this.Rate, this.Distortion);
    }

    /// <summary>
    /// Adds the distortion that an extra cost amounts to, rounded to the nearest distortion unit, and prices the cost
    /// again. Reference: the extra_rd to extra_dist conversion of the sub-block energy adjustment in adjust_rdcost().
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier of the block.</param>
    /// <param name="extraCost">The extra cost, which must be positive.</param>
    public void AddEnergyCost(int rateMultiplier, long extraCost)
    {
        // RDCOST scales the distortion up by RDDIV_BITS, so the extra cost comes back to distortion with that shift.
        const int DistortionShift = 7;
        this.Distortion += (extraCost + (1L << (DistortionShift - 1))) >> DistortionShift;
        this.Cost = Av1RateDistortion.GetCost(rateMultiplier, this.Rate, this.Distortion);
    }

    /// <summary>
    /// Recomputes the cost at another rate multiplier, keeping an invalid candidate invalid.
    /// Reference: av1_rd_cost_update().
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier.</param>
    public void UpdateCost(int rateMultiplier)
    {
        if (this.Rate < int.MaxValue && this.Distortion < long.MaxValue && this.Cost < long.MaxValue)
        {
            this.Cost = Av1RateDistortion.GetSignedCost(rateMultiplier, this.Rate, this.Distortion);
        }
    }

    /// <summary>
    /// Computes the rate and distortion remaining after a partial candidate.
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier for the remaining candidate.</param>
    /// <param name="other">The rate and distortion already consumed.</param>
    /// <returns>The remaining bound, or an unbounded sentinel when either input is invalid.</returns>
    public readonly Av1RateDistortionStatistics Subtract(int rateMultiplier, Av1RateDistortionStatistics other)
    {
        // Search starts without a winning candidate. Preserve that unbounded state instead of subtracting
        // from sentinel integers; finite bounds subtract raw components before the single rate rounding, which
        // rounds the magnitude of a negative rate. Reference: av1_rd_stats_subtraction().
        if (this.Cost == long.MaxValue || other.Cost == long.MaxValue)
        {
            return Invalid;
        }

        Av1RateDistortionStatistics remaining = new(rateMultiplier, this.Rate - other.Rate, this.Distortion - other.Distortion);
        remaining.Cost = Av1RateDistortion.GetSignedCost(rateMultiplier, remaining.Rate, remaining.Distortion);
        return remaining;
    }
}
