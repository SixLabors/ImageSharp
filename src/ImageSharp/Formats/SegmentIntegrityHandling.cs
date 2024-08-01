// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Specifies how to handle validation of errors in different segments of encoded image files.
/// </summary>
public enum SegmentIntegrityHandling
{
    /// <summary>
    /// Do not ignore any errors.
    /// </summary>
    IgnoreNone,

    /// <summary>
    /// Ignore errors in non-critical segments of the encoded image.
    /// </summary>
    IgnoreNonCritical,

    /// <summary>
    /// Ignore errors in data segments (e.g., image data, metadata).
    /// </summary>
    IgnoreData,

    /// <summary>
    /// Ignore errors in all segments.
    /// </summary>
    IgnoreAll
}
