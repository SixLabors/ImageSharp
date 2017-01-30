// <copyright file="IRegion.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System.Numerics;

    /// <summary>
    /// Represents a region of an image.
    /// </summary>
    public interface IRegion
    {
        /// <summary>
        /// Gets the maximum number of intersections to could be returned.
        /// </summary>
        /// <value>
        /// The maximum intersections.
        /// </value>
        int MaxIntersections { get; }

        /// <summary>
        /// Gets the bounds.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        Rectangle Bounds { get; }

        /// <summary>
        /// Scans the X axis for intersections.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="length">The length.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The number of intersections found.</returns>
        int ScanX(int x, float[] buffer, int length, int offset);

        /// <summary>
        /// Scans the Y axis for intersections.
        /// </summary>
        /// <param name="y">The position along the y axis to find intersections.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="length">The length.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The number of intersections found.</returns>
        int ScanY(int y, float[] buffer, int length, int offset);
    }
}