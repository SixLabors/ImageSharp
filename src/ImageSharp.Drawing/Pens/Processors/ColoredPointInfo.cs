// <copyright file="ColoredPointInfo.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Returns details about how far away from the inside of a shape and the color the pixel could be.
    /// </summary>
    /// <typeparam name="TPixel">The type of the color.</typeparam>
    public struct ColoredPointInfo<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The color
        /// </summary>
        public TPixel Color;

        /// <summary>
        /// The distance from element
        /// </summary>
        public float DistanceFromElement;
    }
}
