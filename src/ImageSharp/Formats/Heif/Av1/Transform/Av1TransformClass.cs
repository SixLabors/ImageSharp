// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Classifies AV1 transforms by the axes along which non-identity transforms operate.
/// </summary>
internal enum Av1TransformClass
{
    Class2D = 0,
    ClassHorizontal = 1,
    ClassVertical = 2,
}
