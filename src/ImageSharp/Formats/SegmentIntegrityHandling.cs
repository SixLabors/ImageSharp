// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Specifies how to handle validation of recoverable errors in ancillary and image data segments.
/// Structural errors that prevent safe decoding remain fatal regardless of the selected mode.
/// </summary>
public enum SegmentIntegrityHandling
{
    /// <summary>
    /// Do not ignore any recoverable ancillary or image data segment errors.
    /// </summary>
    Strict = 0,

    /// <summary>
    /// Ignore recoverable errors in ancillary segments, such as optional metadata.
    /// </summary>
    IgnoreAncillary = 1,

    /// <summary>
    /// Ignore recoverable errors in image data segments in addition to ancillary segments.
    /// </summary>
    IgnoreImageData = 2,
}
