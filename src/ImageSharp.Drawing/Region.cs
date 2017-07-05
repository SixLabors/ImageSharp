// <copyright file="Region.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System;
    using SixLabors.Primitives;

    /// <summary>
    /// Represents a region of an image.
    /// </summary>
    public abstract class Region
    {
        /// <summary>
        /// Gets the maximum number of intersections to could be returned.
        /// </summary>
        public abstract int MaxIntersections { get; }

        /// <summary>
        /// Gets the bounding box that entirely surrounds this region.
        /// </summary>
        /// <remarks>
        /// This should always contains all possible points returned from <see cref="Scan(float, Span{float})"/>.
        /// </remarks>
        public abstract Rectangle Bounds { get; }

        /// <summary>
        /// Scans the X axis for intersections at the Y axis position.
        /// </summary>
        /// <param name="y">The position along the y axis to find intersections.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The number of intersections found.</returns>
        public abstract int Scan(float y, Span<float> buffer);
    }
}