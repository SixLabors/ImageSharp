// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Pending event masks and procedure statuses used by the JPEG XL decoder.
/// </summary>
internal static class JxlDecoderStatus
{
    /// <summary>
    /// Everything went smoothly.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// An error occurred.
    /// </summary>
    public const int Error = 1;

    /// <summary>
    /// Not enough bytes left to continue.
    /// </summary>
    public const int NeedMoreInput = 2;

    /// <summary>
    /// The decoder can decode a preview image and requests setting
    /// a preview output buffer.
    /// </summary>
    public const int NeedPreviewOutBuffer = 3;

    /// <summary>
    /// Not enough memory allocated for the output image buffer.
    /// </summary>
    public const int NeedMoreOutput = 6;

    /// <summary>
    /// Specifies an event mask for parsing the JXL basic info.
    /// </summary>
    public const int BasicInfo = 0x40;

    /// <summary>
    /// Specifies an event mask for parsing and decoding the ICC color profile.
    /// </summary>
    public const int ColorEncoding = 0x100;

    /// <summary>
    /// Specifies decoding a preview image or a small frame.
    /// </summary>
    public const int PreviewImage = 0x200;

    /// <summary>
    /// Specifies an event mask for decoding a single frame. This
    /// event is called once for a still image and multiple times for
    /// animated JPEG XLs.
    /// </summary>
    public const int Frame = 0x400;

    /// <summary>
    /// Specifies an event mask for decoding a full frame (or layer in case coalescing
    /// is disabled).
    /// </summary>
    public const int FullImage = 0x1000;

    /// <summary>
    /// Specifies pending JXL->JPEG decoding.
    /// </summary>
    public const int JpegReconstruction = 0x2000;

    /// <summary>
    /// Specifies pending decompression of box data.
    /// See <see cref="JxlBoxContentDecoder" />.
    /// </summary>
    public const int Box = 0x4000;

    /// <summary>
    /// Specifies an event mask for a progressive step in decoding
    /// the frame.
    /// </summary>
    public const int Progression = 0x8000;

    /// <summary>
    /// Specifies an event mask that specifies a box being decoded is
    /// now complete.
    /// </summary>
    public const int Complete = 0x10000;
}
