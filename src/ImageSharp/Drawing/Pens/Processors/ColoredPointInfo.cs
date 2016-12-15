// <copyright file="ColoredPointInfo.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Pens.Processors
{
    /// <summary>
    /// Returns details about how far awau from the inside of a shape and the color the pixel could be.
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <typeparam name="TPacked">The type of the packed.</typeparam>
    public struct ColoredPointInfo<TColor, TPacked>
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
    {
        /// <summary>
        /// The color
        /// </summary>
        public TColor Color;

        /// <summary>
        /// The distance from element
        /// </summary>
        public float DistanceFromElement;
    }
}
