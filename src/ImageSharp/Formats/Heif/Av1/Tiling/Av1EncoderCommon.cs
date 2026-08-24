// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds sequence, frame, and macroblock state shared across AV1 encoder stages.
/// </summary>
internal class Av1EncoderCommon
{
    /// <summary>
    /// Gets or sets the frame height in 4x4 mode-information units.
    /// </summary>
    public int ModeInfoRowCount { get; internal set; }

    /// <summary>
    /// Gets or sets the frame width in 4x4 mode-information units.
    /// </summary>
    public int ModeInfoColumnCount { get; internal set; }

    /// <summary>
    /// Gets or sets the row stride of frame mode information in 4x4 units.
    /// </summary>
    public int ModeInfoStride { get; internal set; }

    /// <summary>
    /// Gets or sets the coded frame dimensions.
    /// </summary>
    public required ObuFrameSize FrameSize { get; internal set; }

    /// <summary>
    /// Gets or sets the tile layout for the current frame.
    /// </summary>
    public required ObuTileGroupHeader TilesInfo { get; internal set; }
}
