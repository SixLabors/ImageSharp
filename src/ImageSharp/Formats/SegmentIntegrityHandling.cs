// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Specifies how to handle validation of recoverable errors in ancillary and image data segments.
/// Structural errors that prevent safe decoding remain fatal regardless of the selected mode.
/// </summary>
/// <remarks>
/// Some decoders continue past damaged data inside compressed image data in each mode, and keep the pixels that they
/// decode. For example, a JPEG decoder continues at the next restart marker, and a CCITT fax decoder continues at the
/// next EOL code. Such damage is not a segment error.
/// </remarks>
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
