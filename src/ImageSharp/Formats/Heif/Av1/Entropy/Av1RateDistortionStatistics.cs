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
    }

    /// <summary>
    /// Gets the sentinel for a candidate that cannot win a cost comparison.
    /// </summary>
    public static Av1RateDistortionStatistics Invalid => new()
    {
        Rate = int.MaxValue,
        Distortion = long.MaxValue,
        Cost = long.MaxValue
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
    /// Gets the rounded rate-distortion cost.
    /// </summary>
    public long Cost { get; private set; }

    /// <summary>
    /// Adds a valid candidate's rate and distortion and updates the combined cost.
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier for the combined candidate.</param>
    /// <param name="other">The valid candidate to add.</param>
    public void Add(int rateMultiplier, in Av1RateDistortionStatistics other)
    {
        // Round the combined rate only once. Adding the already rounded child costs can change
        // partition and inter/intra decisions even when both children have the same reconstruction.
        this.Rate += other.Rate;
        this.Distortion += other.Distortion;
        this.Cost = Av1RateDistortion.GetCost(rateMultiplier, this.Rate, this.Distortion);
    }
}
