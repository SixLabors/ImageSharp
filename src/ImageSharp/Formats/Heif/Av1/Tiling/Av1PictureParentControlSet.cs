// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds encoder state that is shared by all coding passes for one AV1 picture.
/// </summary>
internal class Av1PictureParentControlSet
{
    /// <summary>
    /// Gets or sets frame dimensions and tile state shared by encoder stages.
    /// </summary>
    public required Av1EncoderCommon Common { get; internal set; }

    /// <summary>
    /// Gets or sets the frame header being encoded.
    /// </summary>
    public required ObuFrameHeader FrameHeader { get; internal set; }

    /// <summary>
    /// Gets or sets the preceding quantizer index for each tile context.
    /// </summary>
    public required int[] PreviousQIndex { get; internal set; }

    /// <summary>
    /// Gets or sets the encoder palette-search level.
    /// </summary>
    public int PaletteLevel { get; internal set; }

    /// <summary>
    /// Gets or sets the frame width aligned for superblock traversal.
    /// </summary>
    public int AlignedWidth { get; internal set; }

    /// <summary>
    /// Gets or sets the frame height aligned for superblock traversal.
    /// </summary>
    public int AlignedHeight { get; internal set; }

    /// <summary>
    /// Gets or sets the geometry state for each superblock in the picture.
    /// </summary>
    public required Av1SuperblockGeometry[] SuperblockGeometry { get; internal set; }
}
