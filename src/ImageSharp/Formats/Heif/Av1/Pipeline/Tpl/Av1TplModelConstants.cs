// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The fixed dimensions and limits of the temporal dependency model.
/// </summary>
internal static class Av1TplModelConstants
{
    /// <summary>
    /// The base-two logarithm of the statistics block size in mode-information units. The blocks are 16x16.
    /// </summary>
    public const int BlockModeInfoLog2 = 2;

    /// <summary>
    /// The side of a model block in luma samples.
    /// </summary>
    public const int BlockSize = 16;

    /// <summary>
    /// The number of samples in a model block.
    /// </summary>
    public const int BlockArea = BlockSize * BlockSize;

    /// <summary>
    /// The base-two logarithm of the largest superblock in mode-information units.
    /// </summary>
    public const int MaximumModeInfoSizeLog2 = 5;

    /// <summary>
    /// The largest number of look-ahead frames.
    /// </summary>
    public const int MaximumLagBuffers = 48;

    /// <summary>
    /// The number of model frames that follow the reference-slot frames.
    /// </summary>
    public const int MaximumFrameIndex = 2 * MaximumLagBuffers;

    /// <summary>
    /// The number of reference buffer slots.
    /// </summary>
    public const int ReferenceFrameSlotCount = 8;

    /// <summary>
    /// The number of named inter references.
    /// </summary>
    public const int InterReferenceCount = 7;

    /// <summary>
    /// The length of the frame statistics buffer: the reserved slot, the reference-slot frames and the model frames.
    /// </summary>
    public const int FrameStatisticsLength = MaximumFrameIndex + ReferenceFrameSlotCount + 1;

    /// <summary>
    /// The shift that scales distortions and rates in the statistics.
    /// </summary>
    public const int DependencyCostScaleLog2 = 4;

    /// <summary>
    /// The shift of the rate term of a rate-distortion cost.
    /// </summary>
    public const int ProbabilityCostShift = 9;

    /// <summary>
    /// The shift of the distortion term of a rate-distortion cost.
    /// </summary>
    public const int RateDistortionDivisorBits = 7;

    /// <summary>
    /// The longest golden group of the rate control.
    /// </summary>
    public const int MaximumGoldenInterval = 32;

    /// <summary>
    /// The border of the model reconstructions in luma samples. It holds the block size and the interpolation extension on
    /// both sides (24 samples), rounded up to 32.
    /// </summary>
    public const int Border = 32;

    /// <summary>
    /// The number of samples that an interpolation filter reads beyond a block on each side.
    /// </summary>
    public const int InterpolationExtension = 4;

    /// <summary>
    /// Returns the rate-distortion cost of a rate and a distortion with 64-bit rate arithmetic.
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier.</param>
    /// <param name="rate">The rate in 1/512-bit units.</param>
    /// <param name="distortion">The distortion.</param>
    /// <returns>The rounded weighted rate plus the scaled distortion.</returns>
    public static long GetCost(long rateMultiplier, long rate, long distortion)
    {
        // The rate term rounds to nearest: it adds half the divisor, then shifts arithmetically.
        long weighted = rate * rateMultiplier;
        return ((weighted + (1L << (ProbabilityCostShift - 1))) >> ProbabilityCostShift) + (distortion * (1L << RateDistortionDivisorBits));
    }
}
