// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies how transform-block sizes are selected within an AV1 frame.
/// </summary>
internal enum Av1TransformMode : byte
{
    Only4x4 = 0,
    Largest = 1,
    Select = 2,
}
