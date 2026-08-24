// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies the one-dimensional transform applied along one axis of an AV1 transform block.
/// </summary>
internal enum Av1TransformType1d
{
    Dct,
    Adst,
    FlipAdst,
    Identity
}
