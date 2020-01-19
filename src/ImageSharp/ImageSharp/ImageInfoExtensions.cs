// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods that allow the addition of geometry calculating methods to the <see cref="IImageInfo"/> type
    /// </summary>
    public static class ImageInfoExtensions
    {
        /// <summary>
        /// Gets the bounds of the image.
        /// </summary>
        /// <param name="info">The image info</param>
        /// <returns>The <see cref="Size"/></returns>
        public static Size Size(this IImageInfo info) => new Size(info.Width, info.Height);

        /// <summary>
        /// Gets the bounds of the image.
        /// </summary>
        /// <param name="info">The image info</param>
        /// <returns>The <see cref="Rectangle"/></returns>
        public static Rectangle Bounds(this IImageInfo info) => new Rectangle(0, 0, info.Width, info.Height);
    }
}