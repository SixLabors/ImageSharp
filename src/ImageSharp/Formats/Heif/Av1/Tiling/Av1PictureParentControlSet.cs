// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
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
    public required Av1EncoderCommon Common { get; set; }

    /// <summary>
    /// Gets or sets the frame header being encoded.
    /// </summary>
    public required ObuFrameHeader FrameHeader { get; set; }

    /// <summary>
    /// Gets or sets the preceding quantizer index for each tile context.
    /// </summary>
    public required Memory<int> PreviousQIndex { get; set; }

    /// <summary>
    /// Gets or sets the encoder palette-search level.
    /// </summary>
    public int PaletteLevel { get; set; }

    /// <summary>
    /// Gets or sets the native-valued encoding speed.
    /// </summary>
    public HeifEncodingSpeed EncodingSpeed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether source analysis classifies this frame as screen content.
    /// </summary>
    public bool IsScreenContent { get; set; }

    /// <summary>
    /// Gets or sets the resolved motion-search policy for the current frame.
    /// </summary>
    public Av1MotionSearchSettings MotionSearchSettings { get; set; }

    /// <summary>
    /// Gets or sets the initial full-pixel search step derived before the frame's first block.
    /// </summary>
    public int MotionSearchStepParameter { get; set; }

    /// <summary>
    /// Gets or sets the largest whole-sample magnitude written by a new-motion mode in the preceding frame.
    /// </summary>
    public int MaximumMotionVectorMagnitude { get; set; } = -1;
}
