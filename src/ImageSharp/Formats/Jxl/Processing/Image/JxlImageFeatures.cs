// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

/// <summary>
/// Image features for the JPEG XL passes decoder
/// </summary>
internal sealed class JxlImageFeatures
{
    /// <summary>
    /// Gets or sets noise parameters for the passes decoder
    /// </summary>
    public JxlNoiseParameters NoiseParameters { get; set; } = new();

    /// <summary>
    /// Gets or sets patch dictionary for the passes decoder
    /// </summary>
    public JxlPatchDictionary PatchDictionary { get; set; } = new();

    /// <summary>
    /// Gets or sets splines for the passes decoder
    /// </summary>
    public JxlSplines Splines { get; set; } = new();
}
