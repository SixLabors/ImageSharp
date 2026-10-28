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
    /// Gets or sets a value indicating whether the searched residual can be skipped before the skip-cost comparison of the block. This is true
    /// when every transform is empty, or when a recursive luma transform search found that the skip cost is not higher.
    /// </summary>
    public bool AllTransformsEmpty { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the encoder predicted, before any transform search, that the luma residual quantizes to nothing.
    /// </summary>
    public bool SkipPredicted { get; set; }

    /// <summary>
    /// Gets or sets the luma cost used to compare prediction families, including prediction and skip syntax.
    /// </summary>
    public long LumaCost { get; set; }

    /// <summary>
    /// Gets or sets the cost of the transform choice alone. It includes the coefficients, the non-skip flag and the transform-size syntax, but not
    /// the prediction syntax. This cost is the bound for the search at a later transform depth.
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
        // Round the combined rate only once. A sum of the rounded child costs can change partition and inter-intra decisions, even when both
        // children have the same reconstruction.
        this.Rate += other.Rate;
        this.ResidualRate += other.ResidualRate;
        this.Distortion += other.Distortion;
        this.PredictionDistortion += other.PredictionDistortion;
        this.HasCoefficients |= other.HasCoefficients;
        this.Cost = Av1RateDistortion.GetCost(rateMultiplier, this.Rate, this.Distortion);
    }

    /// <summary>
    /// Adds one eighth to the distortion and to the luma cost of a valid inter prediction. Then it calculates the cost again from the adjusted
    /// distortion. The image tune uses this bias to prefer intra prediction.
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
    /// Adds one eighth to the distortion and one eighth to the current cost. The image tune uses this bias to prefer intra prediction. The current
    /// cost can differ from the price of the rate and the distortion, so this method does not calculate the cost again.
    /// </summary>
    public void AddInterCostBias()
    {
        this.Distortion += this.Distortion >> 3;
        this.Cost += this.Cost >> 3;
    }

    /// <summary>
    /// Adds the sharpness 3 offset to the distortion when the prediction is smoother than its source. Then it calculates the cost again and adds
    /// the cost of the offset to a valid luma cost.
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
    /// Adds the sharpness 3 offset to the distortion and calculates the cost again.
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier of the block.</param>
    /// <param name="offset">The amount by which the source variance measure exceeds the block samples'.</param>
    public void AddSmoothingOffset(int rateMultiplier, long offset)
    {
        this.Distortion += offset;
        this.Cost = Av1RateDistortion.GetCost(rateMultiplier, this.Rate, this.Distortion);
    }

    /// <summary>
    /// Changes an extra cost into distortion, rounded to the nearest unit, and adds it. Then it calculates the cost again. The sub-block energy
    /// adjustment supplies the extra cost.
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier of the block.</param>
    /// <param name="extraCost">The extra cost, which must be positive.</param>
    public void AddEnergyCost(int rateMultiplier, long extraCost)
    {
        // The cost function shifts the distortion left by 7 bits. So the extra cost shifts right by the same amount to become distortion.
        const int DistortionShift = 7;
        this.Distortion += (extraCost + (1L << (DistortionShift - 1))) >> DistortionShift;
        this.Cost = Av1RateDistortion.GetCost(rateMultiplier, this.Rate, this.Distortion);
    }

    /// <summary>
    /// Calculates the cost again at another rate multiplier. An invalid candidate stays invalid. A negative rate is valid.
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
        // The search starts without a winning candidate. Keep that unbounded state instead of a subtraction from the sentinel values.
        // Finite bounds subtract the raw rate and distortion before the single rate rounding. That rounding uses the magnitude of a negative rate.
        if (this.Cost == long.MaxValue || other.Cost == long.MaxValue)
        {
            return Invalid;
        }

        Av1RateDistortionStatistics remaining = new(rateMultiplier, this.Rate - other.Rate, this.Distortion - other.Distortion);
        remaining.Cost = Av1RateDistortion.GetSignedCost(rateMultiplier, remaining.Rate, remaining.Distortion);
        return remaining;
    }
}
