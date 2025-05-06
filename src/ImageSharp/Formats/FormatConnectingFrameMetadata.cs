// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// A metadata format designed to allow conversion between different image format frames.
/// </summary>
public class FormatConnectingFrameMetadata
{
    /// <summary>
    /// Gets information about the encoded pixel type if any.
    /// </summary>
    public PixelTypeInfo? PixelTypeInfo { get; init; }

    /// <summary>
    /// Gets the frame color table mode.
    /// </summary>
    public FrameColorTableMode ColorTableMode { get; init; }

    /// <summary>
    /// Gets the duration of the frame.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the frame alpha blending mode.
    /// </summary>
    public FrameBlendMode BlendMode { get; init; }

    /// <summary>
    /// Gets the frame disposal mode.
    /// </summary>
    public FrameDisposalMode DisposalMode { get; init; }

    /// <summary>
    /// Gets or sets the encoding width. <br />
    /// Used for formats that require a specific frame size.
    /// </summary>
    public int? EncodingWidth { get; set; }

    /// <summary>
    /// Gets or sets the encoding height. <br />
    /// Used for formats that require a specific frame size.
    /// </summary>
    public int? EncodingHeight { get; set; }
}
