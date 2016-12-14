// <copyright file="LinearLineSegment.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Paths
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a seriese of control points that will be joined by staight lines
    /// </summary>
    /// <seealso cref="ImageSharp.Drawing.Paths.ILineSegment" />
    public class LinearLineSegment : ILineSegment
    {
        private Vector2[] points;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearLineSegment"/> class.
        /// </summary>
        /// <param name="points">The points.</param>
        public LinearLineSegment(IEnumerable<PointF> points)
            : this(points?.Select(x => x.ToVector2()).ToArray())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearLineSegment"/> class.
        /// </summary>
        /// <param name="points">The points.</param>
        public LinearLineSegment(IEnumerable<Point> points)
            : this(points?.Select(x => x.ToVector2()).ToArray())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearLineSegment"/> class.
        /// </summary>
        /// <param name="points">The points.</param>
        public LinearLineSegment(params PointF[] points)
            : this(points?.Select(x => x.ToVector2()).ToArray())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearLineSegment"/> class.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        internal LinearLineSegment(Vector2 start, Vector2 end)
            : this(new[] { start, end })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearLineSegment"/> class.
        /// </summary>
        /// <param name="points">The points.</param>
        internal LinearLineSegment(Vector2[] points)
        {
            Guard.NotNull(points, nameof(points));
            Guard.MustBeGreaterThanOrEqualTo(points.Count(), 2, nameof(points));

            this.points = points;
        }

        /// <summary>
        /// Converts the <see cref="ILineSegment" /> into a simple linear path..
        /// </summary>
        /// <returns>
        /// Returns the current <see cref="ILineSegment" /> as simple linear path.
        /// </returns>
        public IEnumerable<Vector2> AsSimpleLinearPath()
        {
            return this.points;
        }
    }
}
