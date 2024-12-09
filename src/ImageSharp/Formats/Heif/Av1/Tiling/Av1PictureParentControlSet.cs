// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1PictureParentControlSet
{
    public required Av1Common Common { get; internal set; }

    public required ObuFrameHeader FrameHeader { get; internal set; }

    public required int[] PreviousQIndex { get; internal set; }

    public int PaletteLevel { get; internal set; }

    public int AlignedWidth { get; internal set; }

    public int AlignedHeight { get; internal set; }

    public required Av1SuperblockGeometry[] SuperblockGeometry { get; internal set; }
}
