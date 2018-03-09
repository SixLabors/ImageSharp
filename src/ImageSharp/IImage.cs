// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates the properties and methods that describe an image.
    /// </summary>
    public interface IImage : IImageInfo, IDisposable
    {
        /// <summary>
        /// Gets the size of the image.
        /// </summary>
        /// <returns>The <see cref="Size"/></returns>
        Size Size();

        /// <summary>
        /// Gets the bounds of the image.
        /// </summary>
        /// <returns>The <see cref="Rectangle"/></returns>
        Rectangle Bounds();
    }
}