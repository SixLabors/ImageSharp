// <copyright file="Pen{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Pens
{
    using System;
    using System.Numerics;

    using ImageSharp.Drawing.Brushes;
    using ImageSharp.Drawing.Paths;
    using Processors;

    /// <summary>
    /// Provides a pen that can apply a pattern to a line with a set brush and thickness
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <remarks>
    /// The pattern will be in to the form of new float[]{ 1f, 2f, 0.5f} this will be
    /// converted into a pattern that is 3.5 times longer that the width with 3 sections
    /// section 1 will be width long (making a square) and will be filled by the brush
    /// section 2 will be width * 2 long and will be empty
    /// section 3 will be width/2 long and will be filled
    /// the the pattern will imidiatly repeat without gap.
    /// </remarks>
    public class Pen<TColor> : IPen<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        private static readonly float[] EmptyPattern = new float[0];
        private readonly float[] pattern;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharp.Drawing.Pens.Pen{TColor}"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <param name="pattern">The pattern.</param>
        public Pen(TColor color, float width, float[] pattern)
            : this(new SolidBrush<TColor>(color), width, pattern)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharp.Drawing.Pens.Pen{TColor}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <param name="pattern">The pattern.</param>
        public Pen(IBrush<TColor> brush, float width, float[] pattern)
        {
            this.Brush = brush;
            this.Width = width;
            this.pattern = pattern;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharp.Drawing.Pens.Pen{TColor}"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        public Pen(TColor color, float width)
            : this(new SolidBrush<TColor>(color), width)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharp.Drawing.Pens.Pen{TColor}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        public Pen(IBrush<TColor> brush, float width)
            : this(brush, width, EmptyPattern)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharp.Drawing.Pens.Pen{TColor}"/> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        internal Pen(Pen<TColor> pen)
           : this(pen.Brush, pen.Width, pen.pattern)
        {
        }

        /// <summary>
        /// Gets the brush.
        /// </summary>
        /// <value>
        /// The brush.
        /// </value>
        public IBrush<TColor> Brush { get; }

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
        /// <param name="sourcePixels">The source pixels.</param>
        /// <param name="region">The region the pen will be applied to.</param>
        /// <returns>
        /// Returns a the applicator for the pen.
        /// </returns>
        /// <remarks>
        /// The <paramref name="region" /> when being applied to things like shapes would ussually be the
        /// bounding box of the shape not necorserrally the shape of the whole image
        /// </remarks>
        public PenApplicator<TColor> CreateApplicator(PixelAccessor<TColor> sourcePixels, RectangleF region)
        {
            if (this.pattern == null || this.pattern.Length < 2)
            {
                // if there is only one item in the pattern then 100% of it will
                // be solid so use the quicker applicator
                return new SolidPenApplicator(sourcePixels, this.Brush, region, this.Width);
            }

            return new PatternPenApplicator(sourcePixels, this.Brush, region, this.Width, this.pattern);
        }

        private class SolidPenApplicator : PenApplicator<TColor>
        {
            private readonly BrushApplicator<TColor> brush;
            private readonly float halfWidth;

            public SolidPenApplicator(PixelAccessor<TColor> sourcePixels, IBrush<TColor> brush, RectangleF region, float width)
            {
                this.brush = brush.CreateApplicator(sourcePixels, region);
                this.halfWidth = width / 2;
                this.RequiredRegion = RectangleF.Outset(region, width);
            }

            public override RectangleF RequiredRegion
            {
                get;
            }

            public override void Dispose()
            {
                this.brush.Dispose();
            }

            public override ColoredPointInfo<TColor> GetColor(PointInfo info)
            {
                var result = default(ColoredPointInfo<TColor>);
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

        private class PatternPenApplicator : PenApplicator<TColor>
        {
            private readonly BrushApplicator<TColor> brush;
            private readonly float halfWidth;
            private readonly float[] pattern;
            private readonly float totalLength;

            public PatternPenApplicator(PixelAccessor<TColor> sourcePixels, IBrush<TColor> brush, RectangleF region, float width, float[] pattern)
            {
                this.brush = brush.CreateApplicator(sourcePixels, region);
                this.halfWidth = width / 2;
                this.totalLength = 0;

                this.pattern = new float[pattern.Length + 1];
                this.pattern[0] = 0;
                for (var i = 0; i < pattern.Length; i++)
                {
                    this.totalLength += pattern[i] * width;
                    this.pattern[i + 1] = this.totalLength;
                }

                this.RequiredRegion = RectangleF.Outset(region, width);
            }

            public override RectangleF RequiredRegion
            {
                get;
            }

            public override void Dispose()
            {
                this.brush.Dispose();
            }

            public override ColoredPointInfo<TColor> GetColor(PointInfo info)
            {
                var infoResult = default(ColoredPointInfo<TColor>);
                infoResult.DistanceFromElement = float.MaxValue; // is really outside the element

                var length = info.DistanceAlongPath % this.totalLength;

                // we can treat the DistanceAlongPath and DistanceFromPath as x,y coords for the pattern
                // we need to calcualte the distance from the outside edge of the pattern
                // and set them on the ColoredPointInfo<TColor> along with the color.
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
