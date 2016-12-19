// <copyright file="Pen.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Pens
{
    using System;
    using System.Numerics;

    using Brushes;
    using Drawing.Processors;
    using Paths;
    using Processors;

    /// <summary>
    /// Represenets a <see cref="Pen{TColor, TPacked}"/> in the <see cref="Color"/> color space.
    /// </summary>
    public partial class Pen : Pen<Color, uint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Pen"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        public Pen(Color color, float width)
            : base(color, width)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        public Pen(IBrush<Color, uint> brush, float width)
            : base(brush, width)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <param name="pattern">The pattern.</param>
        public Pen(IBrush<Color, uint> brush, float width, float[] pattern)
            : base(brush, width, pattern)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen"/> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        internal Pen(Pen<Color, uint> pen)
            : base(pen)
        {
        }
    }

    /// <summary>
    /// Provides a pen that can apply a pattern to a line with a set brush and thickness
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <typeparam name="TPacked">The type of the packed.</typeparam>
    /// <remarks>
    /// The pattern will be in to the form of new float[]{ 1f, 2f, 0.5f} this will be
    /// converted into a pattern that is 3.5 times longer that the width with 3 sections
    /// section 1 will be width long (making a square) and will be filled by the brush
    /// section 2 will be width * 2 long and will be empty
    /// section 3 will be width/2 long and will be filled
    /// the the pattern will imidiatly repeat without gap.
    /// </remarks>
    public class Pen<TColor, TPacked> : IPen<TColor, TPacked>
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct, IEquatable<TPacked>
    {
        private static readonly float[] EmptyPattern = new float[0];
        private readonly float[] pattern;

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <param name="pattern">The pattern.</param>
        public Pen(TColor color, float width, float[] pattern)
            : this(new SolidBrush<TColor, TPacked>(color), width, pattern)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <param name="pattern">The pattern.</param>
        public Pen(IBrush<TColor, TPacked> brush, float width, float[] pattern)
        {
            this.Brush = brush;
            this.Width = width;
            this.pattern = pattern;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        public Pen(TColor color, float width)
            : this(new SolidBrush<TColor, TPacked>(color), width)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        public Pen(IBrush<TColor, TPacked> brush, float width)
            : this(brush, width, EmptyPattern)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        internal Pen(Pen<TColor, TPacked> pen)
           : this(pen.Brush, pen.Width, pen.pattern)
        {
        }

        /// <summary>
        /// Gets the brush.
        /// </summary>
        /// <value>
        /// The brush.
        /// </value>
        public IBrush<TColor, TPacked> Brush { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        /// <value>
        /// The width.
        /// </value>
        public float Width { get; }

        /// <summary>
        /// Creates the applicator for applying this pen to an Image
        /// </summary>
        /// <param name="region">The region the pen will be applied to.</param>
        /// <returns>
        /// Returns a the applicator for the pen.
        /// </returns>
        /// <remarks>
        /// The <paramref name="region" /> when being applied to things like shapes would ussually be the
        /// bounding box of the shape not necorserrally the shape of the whole image
        /// </remarks>
        public IPenApplicator<TColor, TPacked> CreateApplicator(RectangleF region)
        {
            if (this.pattern == null || this.pattern.Length < 2)
            {
                // if there is only one item in the pattern then 100% of it will
                // be solid so use the quicker applicator
                return new SolidPenApplicator(this.Brush, region, this.Width);
            }

            return new PatternPenApplicator(this.Brush, region, this.Width, this.pattern);
        }

        private class SolidPenApplicator : IPenApplicator<TColor, TPacked>
        {
            private readonly IBrushApplicator<TColor, TPacked> brush;
            private readonly float halfWidth;
            private readonly int width;

            public SolidPenApplicator(IBrush<TColor, TPacked> brush, RectangleF region, float width)
            {
                this.brush = brush.CreateApplicator(region);
                this.halfWidth = width / 2;
                this.width = (int)Math.Ceiling(width);
            }

            public int DrawingPadding => this.width;

            public void Dispose()
            {
                this.brush.Dispose();
            }

            public ColoredPointInfo<TColor, TPacked> GetColor(PointInfo info)
            {
                var result = default(ColoredPointInfo<TColor, TPacked>);
                result.Color = this.brush.GetColor(info.SearchPoint);

                if (info.DistanceFromPath < this.halfWidth)
                {
                    // inside strip
                    result.DistanceFromElement = 0;
                }
                else
                {
                    result.DistanceFromElement = info.DistanceFromPath - this.halfWidth;
                }

                return result;
            }
        }

        private class PatternPenApplicator : IPenApplicator<TColor, TPacked>
        {
            private readonly IBrushApplicator<TColor, TPacked> brush;
            private readonly float halfWidth;
            private readonly float[] pattern;
            private readonly float totalLength;
            private readonly int width;

            public PatternPenApplicator(IBrush<TColor, TPacked> brush, RectangleF region, float width, float[] pattern)
            {
                this.brush = brush.CreateApplicator(region);
                this.halfWidth = width / 2;
                this.totalLength = 0;
                this.width = (int)Math.Ceiling(width);
                this.pattern = new float[pattern.Length + 1];
                this.pattern[0] = 0;
                for (var i = 0; i < pattern.Length; i++)
                {
                    this.totalLength += pattern[i] * width;
                    this.pattern[i + 1] = this.totalLength;
                }
            }

            public int DrawingPadding => this.width;

            public void Dispose()
            {
                this.brush.Dispose();
            }

            public ColoredPointInfo<TColor, TPacked> GetColor(PointInfo info)
            {
                var infoResult = default(ColoredPointInfo<TColor, TPacked>);
                infoResult.DistanceFromElement = float.MaxValue; // is really outside the element

                var length = info.DistanceAlongPath % this.totalLength;

                // we can treat the DistanceAlongPath and DistanceFromPath as x,y coords for the pattern
                // we need to calcualte the distance from the outside edge of the pattern
                // and set them on the ColoredPointInfo<TColor, TPacked> along with the color.
                infoResult.Color = this.brush.GetColor(info.SearchPoint);

                float distanceWAway = 0;

                if (info.DistanceFromPath < this.halfWidth)
                {
                    // inside strip
                    distanceWAway = 0;
                }
                else
                {
                    distanceWAway = info.DistanceFromPath - this.halfWidth;
                }

                for (var i = 0; i < this.pattern.Length - 1; i++)
                {
                    var start = this.pattern[i];
                    var end = this.pattern[i + 1];

                    if (length >= start && length < end)
                    {
                        // in section
                        if (i % 2 == 0)
                        {
                            // solid part return the maxDistance
                            infoResult.DistanceFromElement = distanceWAway;
                            return infoResult;
                        }
                        else
                        {
                            // this is a none solid part
                            var distanceFromStart = length - start;
                            var distanceFromEnd = end - length;

                            var closestEdge = Math.Min(distanceFromStart, distanceFromEnd);

                            var distanceAcross = closestEdge;

                            if (distanceWAway > 0)
                            {
                                infoResult.DistanceFromElement = new Vector2(distanceAcross, distanceWAway).Length();
                            }
                            else
                            {
                                infoResult.DistanceFromElement = closestEdge;
                            }

                            return infoResult;
                        }
                    }
                }

                return infoResult;
            }
        }
    }
}