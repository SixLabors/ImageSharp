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
        /// Applies the swizzle transformation to a given point.
        /// </summary>
        /// <param name="point">Point to transform.</param>
        /// <returns>Transformed point.</returns>
        Point Transform(Point point);
    }
}
