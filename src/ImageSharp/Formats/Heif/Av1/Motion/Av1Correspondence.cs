// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Names one point of a frame and the point of another frame that it moved to.
/// </summary>
/// <param name="x">The horizontal position in the frame the model maps from.</param>
/// <param name="y">The vertical position in the frame the model maps from.</param>
/// <param name="referenceX">The horizontal position in the frame the model maps to.</param>
/// <param name="referenceY">The vertical position in the frame the model maps to.</param>
/// <remarks>Reference: Correspondence.</remarks>
internal readonly struct Av1Correspondence(double x, double y, double referenceX, double referenceY)
{
    /// <summary>
    /// Gets the horizontal position in the frame the model maps from.
    /// </summary>
    public double X { get; } = x;

    /// <summary>
    /// Gets the vertical position in the frame the model maps from.
    /// </summary>
    public double Y { get; } = y;

    /// <summary>
    /// Gets the horizontal position in the frame the model maps to.
    /// </summary>
    public double ReferenceX { get; } = referenceX;

    /// <summary>
    /// Gets the vertical position in the frame the model maps to.
    /// </summary>
    public double ReferenceY { get; } = referenceY;
}
