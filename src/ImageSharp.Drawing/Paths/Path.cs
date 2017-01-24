// <copyright file="Path.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Paths
{
    using System.Numerics;

    /// <summary>
    /// A aggregate of <see cref="ILineSegment"/>s making a single logical path
    /// </summary>
    /// <seealso cref="IPath" />
    public class Path : IPath
    {
        /// <summary>
        /// The inner path.
        /// </summary>
        private readonly InternalPath innerPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="Path"/> class.
        /// </summary>
        /// <param name="segment">The segment.</param>
        public Path(params ILineSegment[] segment)
        {
            this.innerPath = new InternalPath(segment, false);
        }

        /// <inheritdoc />
        public RectangleF Bounds => this.innerPath.Bounds;

        /// <inheritdoc />
        public bool IsClosed => false;

        /// <inheritdoc />
        public float Length => this.innerPath.Length;

        /// <inheritdoc />
        public Vector2[] AsSimpleLinearPath()
        {
            return this.innerPath.Points;
        }

        /// <inheritdoc />
        public PointInfo Distance(Vector2 point)
        {
            return this.innerPath.DistanceFromPath(point);
        }
    }
}