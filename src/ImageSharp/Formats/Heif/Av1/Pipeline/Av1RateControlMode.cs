// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The rate control mode of the encoder.
/// </summary>
internal enum Av1RateControlMode
{
    /// <summary>
    /// Every frame codes near a fixed quantizer, without a bit budget.
    /// </summary>
    Quality,

    /// <summary>
    /// The frames share a bit budget. No frame codes at a quantizer below the constant-quality level.
    /// </summary>
    ConstrainedQuality,

    /// <summary>
    /// The frames share a bit budget that the rate model spreads over the frames.
    /// </summary>
    VariableBitRate,

    /// <summary>
    /// Each frame gets a bit target from the level of a leaky buffer.
    /// </summary>
    ConstantBitRate
}
