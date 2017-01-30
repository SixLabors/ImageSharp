// <copyright file="IDrawableRegion.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System.Numerics;

    /// <summary>
    /// Represents a region with knowledge about its outline.
    /// </summary>
    /// <seealso cref="ImageSharp.Drawing.IRegion" />
    public interface IDrawableRegion : IRegion
    {
        /// <summary>
        /// Gets the point information for the specified x and y location.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>Information about the point in relation to a drawable edge</returns>
        PointInfo GetPointInfo(int x, int y);
    }
}