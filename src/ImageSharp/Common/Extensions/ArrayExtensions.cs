// <copyright file="ArrayExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Extension methods for arrays.
    /// </summary>
    public static class ArrayExtensions
    {
        /// <summary>
        /// Locks the pixel buffer providing access to the pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="pixels">The pixel buffer.</param>
        /// <param name="width">Gets the width of the image represented by the pixel buffer.</param>
        /// <param name="height">The height of the image represented by the pixel buffer.</param>
        /// <returns>The <see cref="PixelAccessor{TColor}"/></returns>
        public static PixelAccessor<TColor> Lock<TColor>(this TColor[] pixels, int width, int height)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return new PixelAccessor<TColor>(width, height, pixels);
        }
    }
}