// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies how much of a transform coefficient plane the encoder evaluates.
/// </summary>
internal enum Av1CoefficientShape
{
    /// <summary>
    /// Evaluates the complete coefficient plane.
    /// </summary>
    Default,

    /// <summary>
    /// Evaluates the half-coefficient shape.
    /// </summary>
    N2,

    /// <summary>
    /// Evaluates the quarter-coefficient shape.
    /// </summary>
    N4,

    /// <summary>
    /// Evaluates only the DC coefficient.
    /// </summary>
    OnlyDc
}
