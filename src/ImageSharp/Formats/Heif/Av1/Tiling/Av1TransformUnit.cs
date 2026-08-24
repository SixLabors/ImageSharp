// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the transform syntax and coefficient range for one AV1 transform unit.
/// </summary>
internal class Av1TransformUnit
{
    /// <summary>
    /// Gets the nonzero-coefficient count for each color plane.
    /// </summary>
    public ushort[] NzCoefficientCount { get; } = new ushort[3];

    /// <summary>
    /// Gets the transform type selected for each color plane.
    /// </summary>
    public Av1TransformType[] TransformType { get; } = new Av1TransformType[Av1Constants.PlaneTypeCount];
}
