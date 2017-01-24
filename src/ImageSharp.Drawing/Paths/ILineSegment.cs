// <copyright file="ILineSegment.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Paths
{
    using System.Numerics;

    /// <summary>
    /// Represents a simple path segment
    /// </summary>
    public interface ILineSegment
    {
        /// <summary>
        /// Converts the <see cref="ILineSegment" /> into a simple linear path..
        /// </summary>
        /// <returns>Returns the current <see cref="ILineSegment" /> as simple linear path.</returns>
        Vector2[] AsSimpleLinearPath(); // TODO move this over to ReadonlySpan<Vector2> once available
    }
}
