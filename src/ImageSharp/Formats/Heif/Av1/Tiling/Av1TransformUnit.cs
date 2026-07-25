// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1TransformUnit
{
    public ushort[] NzCoefficientCount { get; } = new ushort[3];

    public Av1TransformType[] TransformType { get; } = new Av1TransformType[Av1Constants.PlaneTypeCount];
}
