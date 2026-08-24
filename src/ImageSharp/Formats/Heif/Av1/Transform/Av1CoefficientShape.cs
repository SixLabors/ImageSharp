// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies how much of a transform coefficient plane the encoder evaluates.
/// </summary>
internal enum Av1CoefficientShape
{
    Default,
    N2,
    N4,
    OnlyDc
}
