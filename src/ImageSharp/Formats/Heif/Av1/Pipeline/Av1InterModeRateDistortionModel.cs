// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Learns residual rate and distortion for one block geometry within a tile.
/// </summary>
internal struct Av1InterModeRateDistortionModel
{
    private int count;
    private double distortionSum;
    private double slopeSampleSum;
    private double errorSum;
    private double errorSquareSum;
    private double errorSlopeSum;
    private double distortionMean;
    private double slopeSampleMean;
    private double errorMean;
    private double errorSquareMean;
    private double errorSlopeMean;
    private double slope;
    private double intercept;

    /// <summary>
    /// Gets a value indicating whether enough completed transform searches have established an estimate.
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// Accumulates a completed residual search without retaining its pixels or coefficients.
    /// </summary>
    public void Add(long predictionError, long distortion, int rate)
    {
        // Empty residuals do not define error removed per coded bit. The bounded sample count keeps
        // a complex superblock from overwhelming the accumulated history before the next fit.
        if (rate == 0 || predictionError == distortion || this.count >= 6400)
        {
            return;
        }

        double slopeSample = (predictionError - distortion) / (double)rate;
        this.count++;
        this.distortionSum += distortion;
        this.slopeSampleSum += slopeSample;
        this.errorSum += predictionError;
        this.errorSquareSum += (double)predictionError * predictionError;
        this.errorSlopeSum += predictionError * slopeSample;
    }

    /// <summary>
    /// Updates the estimate at a superblock boundary when enough new samples are available.
    /// </summary>
    public void Fit()
    {
        if (this.count < (this.IsReady ? 64 : 200))
        {
            return;
        }

        if (this.IsReady)
        {
            // Retain three parts history to one part new observations so a single superblock does
            // not abruptly change the ranking of prediction modes in the rest of the tile.
            this.distortionMean = ((this.distortionMean * 3) + (this.distortionSum / this.count)) / 4;
            this.slopeSampleMean = ((this.slopeSampleMean * 3) + (this.slopeSampleSum / this.count)) / 4;
            this.errorMean = ((this.errorMean * 3) + (this.errorSum / this.count)) / 4;
            this.errorSquareMean = ((this.errorSquareMean * 3) + (this.errorSquareSum / this.count)) / 4;
            this.errorSlopeMean = ((this.errorSlopeMean * 3) + (this.errorSlopeSum / this.count)) / 4;
        }
        else
        {
            this.distortionMean = this.distortionSum / this.count;
            this.slopeSampleMean = this.slopeSampleSum / this.count;
            this.errorMean = this.errorSum / this.count;
            this.errorSquareMean = this.errorSquareSum / this.count;
            this.errorSlopeMean = this.errorSlopeSum / this.count;
        }

        double deviation = Math.Sqrt(this.errorSquareMean);
        this.slope = (this.errorSlopeMean - (this.errorMean * this.slopeSampleMean)) /
            ((deviation * deviation) - (this.errorMean * this.errorMean));

        this.intercept = this.slopeSampleMean - (this.slope * this.errorMean);
        this.IsReady = true;
        this.count = 0;
        this.distortionSum = 0;
        this.slopeSampleSum = 0;
        this.errorSum = 0;
        this.errorSquareSum = 0;
        this.errorSlopeSum = 0;
    }

    /// <summary>
    /// Estimates residual syntax and distortion from the complete prediction error.
    /// </summary>
    public readonly void Estimate(long predictionError, out int rate, out long distortion)
    {
        rate = 0;
        distortion = predictionError;
        if (predictionError < this.distortionMean)
        {
            return;
        }

        distortion = (long)Math.Round(this.distortionMean, MidpointRounding.AwayFromZero);
        double removedErrorPerRate = (this.slope * predictionError) + this.intercept;
        if (Math.Abs(removedErrorPerRate) < 0.01)
        {
            rate = int.MaxValue / 2;
        }
        else
        {
            double estimate = (predictionError - this.distortionMean) / removedErrorPerRate;
            rate = estimate < 0 ? 0 : (int)Math.Min(Math.Round(estimate, MidpointRounding.AwayFromZero), int.MaxValue / 2);
        }

        // A nonpositive residual rate describes prediction alone, so use its error rather than
        // the learned reconstruction error. Both quantities use the same squared-error scale.
        if (rate <= 0)
        {
            rate = 0;
            distortion = predictionError;
        }
    }
}
