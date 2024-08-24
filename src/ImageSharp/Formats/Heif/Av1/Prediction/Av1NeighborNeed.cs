// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

[Flags]
internal enum Av1NeighborNeed
{
    Nothing = 0,
    Left = 2,
    Above = 4,
    AboveRight = 8,
    AboveLeft = 16,
    BottomLeft = 32,
}
