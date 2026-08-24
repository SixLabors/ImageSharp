// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Identifies the compression method used by a coded image item in a HEIF file.
/// </summary>
public enum HeifCompressionMethod
{
    /// <summary>
    /// High Efficiency Video Coding (HEVC).
    /// </summary>
    Hevc,

    /// <summary>
    /// Legacy JPEG coding.
    /// </summary>
    LegacyJpeg,

    /// <summary>
    /// JPEG 2000 coding.
    /// </summary>
    Jpeg2000,

    /// <summary>
    /// JPEG XR coding.
    /// </summary>
    JpegXR,

    /// <summary>
    /// JPEG XS coding.
    /// </summary>
    JpegXS,

    /// <summary>
    /// AOMedia Video 1 (AV1) coding.
    /// </summary>
    Av1,

    /// <summary>
    /// Advanced Video Coding (AVC).
    /// </summary>
    Avc,
}
