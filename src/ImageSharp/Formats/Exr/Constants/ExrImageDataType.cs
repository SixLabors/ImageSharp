// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Exr.Constants;

/// <summary>
/// This enum represents the type of pixel data in the EXR image.
/// </summary>
public enum ExrImageDataType
{
    /// <summary>
    /// The pixel data is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The pixel data has 3 channels: red, green and blue.
    /// </summary>
    Rgb = 1,

    /// <summary>
    /// The pixel data has four channels: red, green, blue and a alpha channel.
    /// </summary>
    Rgba = 2,

    /// <summary>
    /// There is only one channel with the luminance.
    /// </summary>
    Gray = 3,
}
