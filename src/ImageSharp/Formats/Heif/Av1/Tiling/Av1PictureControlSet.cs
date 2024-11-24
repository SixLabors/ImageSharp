// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1PictureControlSet
{
    public required Av1NeighborArrayUnit<byte>[] LuminanceDcSignLevelCoefficientNeighbors { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] CrDcSignLevelCoefficientNeighbors { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] CbDcSignLevelCoefficientNeighbors { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] TransformFunctionContexts { get; internal set; }

    public required Av1SequenceControlSet Sequence { get; internal set; }

    public required Av1PictureParentControlSet Parent { get; internal set; }
}
