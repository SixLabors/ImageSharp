// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Tells the result of a read of one OBU boundary or one LEB128 value by <see cref="ObuFramer"/>.
/// </summary>
internal enum ObuFramingStatus
{
    /// <summary>
    /// The data holds the complete value or OBU.
    /// </summary>
    Complete = 0,

    /// <summary>
    /// The data ends inside the OBU header, its size field or the LEB128 value. No OBU follows.
    /// </summary>
    Truncated = 1,

    /// <summary>
    /// The OBU header is complete, but the data ends inside the OBU payload. The payload holds the bytes that remain.
    /// </summary>
    PayloadTruncated = 2,

    /// <summary>
    /// The forbidden bit of the OBU header is set.
    /// </summary>
    ForbiddenBit = 3,

    /// <summary>
    /// The LEB128 value does not end within eight bytes, or it is larger than 2^32 - 1.
    /// </summary>
    InvalidSize = 4
}
