// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Describes the interpretation of the input and output
/// buffers.
/// </summary>
internal struct JxlBitDepth
{
    /// <summary>
    /// Gets or sets the kind of bit depth.
    /// </summary>
    public JxlBitDepthType Type { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per sample when the
    /// bit depth type is custom.
    /// </summary>
    public uint BitsPerSample { get; set; }

    /// <summary>
    /// Gets or sets the custom exponent bits per sample.
    /// </summary>
    public uint ExponentBitsPerSample { get; set; }
}
