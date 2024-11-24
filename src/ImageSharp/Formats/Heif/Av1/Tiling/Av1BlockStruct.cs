// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1BlockStruct
{
    public Av1TransformUnit[] TransformBlocks { get; } = new Av1TransformUnit[Av1Constants.MaxTransformUnitCount];

    public required Av1MacroBlockD MacroBlock { get; set; }

    public int MdScanIndex { get; set; }

    public int QIndex { get; set; }

    public int SegmentId { get; set; }

    public Av1FilterIntraMode FilterIntraMode { get; set; }
}
