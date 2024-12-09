// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1EncoderBlockStruct
{
    public Av1TransformUnit[] TransformBlocks { get; } = new Av1TransformUnit[Av1Constants.MaxTransformUnitCount];

    public required Av1MacroBlockD MacroBlock { get; set; }

    public int ModeDecisionScanIndex { get; set; }

    public int QuantizationIndex { get; set; }

    public int SegmentId { get; set; }

    public Av1FilterIntraMode FilterIntraMode { get; set; }

    public required int[] PaletteSize { get; internal set; }
}
