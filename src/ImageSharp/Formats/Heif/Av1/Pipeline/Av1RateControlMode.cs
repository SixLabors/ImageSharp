// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The rate control mode of the encoder. Reference: aom_rc_mode.
/// </summary>
internal enum Av1RateControlMode
{
    /// <summary>
    /// Every frame codes near a fixed quantizer, without a bit budget. Reference: AOM_Q.
    /// </summary>
    Quality,

    /// <summary>
    /// The frames share a bit budget, and no frame codes at a quantizer below the constant-quality level.
    /// Reference: AOM_CQ.
    /// </summary>
    ConstrainedQuality,

    /// <summary>
    /// The frames share a bit budget that the rate model spreads over the frames. Reference: AOM_VBR.
    /// </summary>
    VariableBitRate,

    /// <summary>
    /// Each frame gets a bit target from the level of a leaky buffer. Reference: AOM_CBR.
    /// </summary>
    ConstantBitRate
}
