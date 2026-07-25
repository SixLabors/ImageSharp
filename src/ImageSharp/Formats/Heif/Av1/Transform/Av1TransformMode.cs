// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal enum Av1TransformMode : byte
{
    Only4x4 = 0,
    Largest = 1,
    Select = 2,
}
