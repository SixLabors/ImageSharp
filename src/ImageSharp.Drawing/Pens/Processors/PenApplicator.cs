// <copyright file="PenApplicator.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// primitive that converts a <see cref="PointInfo"/> into a color and a distance away from the drawable part of the path.
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    public abstract class PenApplicator<TColor> : IDisposable
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// Gets the required region.
        /// </summary>
        /// <value>
        /// The required region.
        /// </value>
        public abstract RectangleF RequiredRegion { get; }

        /// <inheritdoc/>
        public abstract void Dispose();

        /// <summary>
        /// Gets a <see cref="ColoredPointInfo{TColor}" /> from a point represented by a <see cref="PointInfo" />.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="info">The information to extract color details about.</param>
        /// <returns>
        /// Returns the color details and distance from a solid bit of the line.
        /// </returns>
        public abstract ColoredPointInfo<TColor> GetColor(int x, int y, PointInfo info);
    }
}
