// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Encapsulate an algorithm to swizzle pixels in an image.
    /// </summary>
    public interface ISwizzler
    {
        /// <summary>
        /// Gets the size of the image after transformation.
        /// </summary>
        Size DestinationSize { get; }

        /// <summary>
        /// Applies the swizzle transformation to a given point.
        /// </summary>
        /// <param name="point">Point to transform.</param>
        /// <returns>The transformed point.</returns>
        Point Transform(Point point);
    }
}
