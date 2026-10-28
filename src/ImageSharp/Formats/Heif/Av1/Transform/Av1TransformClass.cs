// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Classifies AV1 transforms by the axes along which non-identity transforms operate.
/// </summary>
internal enum Av1TransformClass
{
    /// <summary>
    /// A two-dimensional transform with non-identity processing on both axes.
    /// </summary>
    Class2D = 0,

    /// <summary>
    /// A horizontal transform with identity processing on the vertical axis.
    /// </summary>
    ClassHorizontal = 1,

    /// <summary>
    /// A vertical transform with identity processing on the horizontal axis.
    /// </summary>
    ClassVertical = 2,
}
