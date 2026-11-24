// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Specifies how a decoder handles recoverable errors in ancillary segments and in image data.
/// </summary>
/// <remarks>
/// <para>
/// An ancillary segment holds data that the decoder does not need for the pixels, for example optional metadata.
/// </para>
/// <para>
/// Image data is the encoded pixel data. Image data that is damaged, or that ends before the image is complete, is a
/// recoverable image data error.
/// </para>
/// <para>
/// In each mode, the decoder throws for an error that prevents a safe decode, for example image dimensions that are
/// not valid.
/// </para>
/// </remarks>
public enum SegmentIntegrityHandling
{
    /// <summary>
    /// The decoder throws an exception for each recoverable error in ancillary segments and in image data.
    /// </summary>
    Strict = 0,

    /// <summary>
    /// The decoder ignores recoverable errors in ancillary segments. It throws an exception for each recoverable error
    /// in image data.
    /// </summary>
    IgnoreAncillary = 1,

    /// <summary>
    /// The decoder ignores recoverable errors in ancillary segments and in image data. It returns each pixel that it
    /// can decode. The other pixels get the fill value of the format, for example transparent black.
    /// </summary>
    IgnoreImageData = 2,
}
