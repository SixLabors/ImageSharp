// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// A metadata format designed to allow conversion between different image format frames.
/// </summary>
public class FormatConnectingFrameMetadata
{
    /// <summary>
    /// Gets or sets the frame color table.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <summary>
    /// Gets or sets the frame color table mode.
    /// </summary>
    public FrameColorTableMode ColorTableMode { get; set; }

    /// <summary>
    /// Gets or sets the duration of the frame.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the frame alpha blending mode.
    /// </summary>
    public FrameBlendMode BlendMode { get; set; }

    /// <summary>
    /// Gets or sets the frame disposal mode.
    /// </summary>
    public FrameDisposalMode DisposalMode { get; set; }
}
