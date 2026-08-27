// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the sequence-level order-hint and dependent prediction-tool settings.
/// </summary>
internal class ObuOrderHintInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether order hints are enabled.
    /// </summary>
    public bool EnableOrderHint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether joint compound prediction is enabled.
    /// </summary>
    public bool EnableJointCompound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reference-frame motion vectors are enabled.
    /// </summary>
    public bool EnableReferenceFrameMotionVectors { get; set; }

    /// <summary>
    /// Gets or sets the number of bits used to encode order hints.
    /// </summary>
    public int OrderHintBits { get; set; }

    /// <summary>
    /// Computes the signed distance between two order hints in the sequence's modulo order-hint domain.
    /// </summary>
    /// <param name="first">The first order hint.</param>
    /// <param name="second">The order hint subtracted from <paramref name="first"/>.</param>
    /// <returns>
    /// The shortest signed modulo distance, or zero when order hints are disabled for the sequence.
    /// </returns>
    public int GetRelativeDistance(uint first, uint second)
    {
        if (!this.EnableOrderHint)
        {
            return 0;
        }

        int difference = (int)first - (int)second;
        int signBit = 1 << (this.OrderHintBits - 1);

        // Folding around the sign bit maps the unsigned difference to [-2^(bits - 1), 2^(bits - 1)), including
        // the wraparound between the highest encoded order hint and zero.
        return (difference & (signBit - 1)) - (difference & signBit);
    }
}
