// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Represents the different spaces used in transformation operations.
/// </summary>
public enum TransformSpace
{
    /// <summary>
    /// Coordinate space is a continuous, mathematical grid where objects and positions
    /// are defined with precise, often fractional values. This space allows for fine-grained
    /// transformations like scaling, rotation, and translation with high precision.
    /// In coordinate space, an image can span from (0,0) to (4,4) for a 4x4 image, including the boundaries.
    /// </summary>
    Coordinate,

    /// <summary>
    /// Pixel space is a discrete grid where each position corresponds to a specific pixel on the screen.
    /// In this space, positions are defined by whole numbers, with no fractional values.
    /// A 4x4 image in pixel space covers exactly 4 pixels wide and 4 pixels tall, ranging from (0,0) to (3,3).
    /// Pixel space is used when rendering images to ensure that everything aligns with the actual pixels on the screen.
    /// </summary>
    Pixel
}
