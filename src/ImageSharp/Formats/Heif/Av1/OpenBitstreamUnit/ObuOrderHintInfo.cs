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
}
