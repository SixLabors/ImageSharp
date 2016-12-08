// <copyright file="ArrayExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Extension methods for arrays.
    /// </summary>
    public static class ArrayExtensions
    {
        /// <summary>
        /// Locks the pixel buffer providing access to the pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="pixels">The pixel buffer.</param>
        /// <param name="width">Gets the width of the image represented by the pixel buffer.</param>
        /// <param name="height">The height of the image represented by the pixel buffer.</param>
        /// <returns>The <see cref="PixelAccessor{TColor,TPacked}"/></returns>
        public static PixelAccessor<TColor, TPacked> Lock<TColor, TPacked>(this TColor[] pixels, int width, int height)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return new PixelAccessor<TColor, TPacked>(width, height, pixels);
        }
    }
}