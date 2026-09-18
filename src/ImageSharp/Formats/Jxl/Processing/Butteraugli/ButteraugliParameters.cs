// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

/// <summary>
/// Parameters for Butteraugli.
/// </summary>
internal struct ButteraugliParameters()
{
    /// <summary>
    /// Gets or sets the multiplier for penalizing new HF artifacts more than
    /// blurring away features. Value of 1.0 represents neutral.
    /// </summary>
    public float HfAsymmetry { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the multiplier for the psychovisual difference in the X channel.
    /// </summary>
    public float XMultiplier { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the number of nits that correspond to 1.0f input values.
    /// </summary>
    public float IntensityTarget { get; set; } = 80f;
}
