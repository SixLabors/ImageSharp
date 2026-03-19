// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Constants;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Configuration options for use during OpenExr encoding.
/// </summary>
internal interface IExrEncoderOptions
{
    /// <summary>
    /// Gets the pixel type of the image.
    /// </summary>
    ExrPixelType? PixelType { get; }
}
