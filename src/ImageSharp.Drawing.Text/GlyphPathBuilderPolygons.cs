// <copyright file="GlyphPathBuilderPolygons.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using ImageSharp.Drawing.Paths;
    using ImageSharp.Drawing.Shapes;
    using NOpenType;

    /// <summary>
    /// Used to convert the fint glyphs into GlyphPolygons for rendering.
    /// </summary>
    /// <seealso cref="NOpenType.GlyphPathBuilderBase" />
    internal class GlyphPathBuilderPolygons : NOpenType.GlyphPathBuilderBase
    {
        private static readonly Vector2 TwoThirds = new Vector2(2f / 3f);
        private object locker = new object();
        private Dictionary<char, GlyphPolygon> glyphCache = new Dictionary<char, GlyphPolygon>();
        private List<Polygon> polygons = new List<Polygon>();
        private List<ILineSegment> segments = new List<ILineSegment>();
        private Vector2 lastPoint;
        private Vector2 offset;
        private Vector2 scale;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlyphPathBuilderPolygons" /> class.
        /// </summary>
        /// <param name="typeface">The typeface.</param>
        /// <param name="fontSize">Size of the font.</param>
        public GlyphPathBuilderPolygons(Typeface typeface, float fontSize)
            : base(typeface)
        {
            this.offset = new Vector2(0, fontSize);
            var scaleFactor = typeface.CalculateScale(fontSize);
            this.scale = new Vector2(scaleFactor, -scaleFactor);
        }

        /// <summary>
        /// Builds the glyph.
        /// </summary>
        /// <param name="character">The character.</param>
        /// <returns>
        /// Returns the polygon for the requested glyph
        /// </returns>
        public GlyphPolygon BuildGlyph(char character)
        {
            if (this.glyphCache.ContainsKey(character))
            {
                return this.glyphCache[character];
            }

            // building a glyph isn't thread safe because it uses a class wide state while building
            lock (this.locker)
            {
                if (this.glyphCache.ContainsKey(character))
                {
                    return this.glyphCache[character];
                }

                var glyIndex = (ushort)this.TypeFace.LookupIndex(character);

                this.BuildFromGlyphIndex(glyIndex, 1);

                GlyphPolygon result;
                if (this.polygons.Any())
                {
                    result = new GlyphPolygon(character, glyIndex, this.polygons.ToArray());
                    this.polygons.Clear();
                }
                else
                {
                    result = new GlyphPolygon(character, glyIndex);
                    return null;
                }

                this.glyphCache.Add(character, result);
                return result;
            }
        }

        /// <summary>
        /// Called when [begin read].
        /// </summary>
        /// <param name="countourCount">The countour count.</param>
        protected override void OnBeginRead(int countourCount)
        {
            this.segments.Clear();
            this.polygons.Clear();
        }

        /// <summary>
        /// Called when [end read].
        /// </summary>
        protected override void OnEndRead()
        {
        }

        /// <summary>
        /// Called when [close figure].
        /// </summary>
        protected override void OnCloseFigure()
        {
            if (this.segments.Any())
            {
                this.polygons.Add(new Polygon(this.segments.ToArray()));
                this.segments.Clear();
            }
        }

        /// <summary>
        /// Called when [curve3].
        /// </summary>
        /// <param name="p2x">The P2X.</param>
        /// <param name="p2y">The p2y.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        protected override void OnCurve3(short p2x, short p2y, short x, short y)
        {
            var controlPoint = this.offset + (new Vector2(p2x, p2y) * this.scale);
            var endPoint = this.offset + (new Vector2(x, y) * this.scale);

            var c1 = ((controlPoint - this.lastPoint) * TwoThirds) + this.lastPoint;
            var c2 = ((controlPoint - endPoint) * TwoThirds) + endPoint;

            this.segments.Add(new BezierLineSegment(this.lastPoint, c1, c2, endPoint));

            this.lastPoint = endPoint;
        }

        /// <summary>
        /// Called when [curve4].
        /// </summary>
        /// <param name="p2x">The P2X.</param>
        /// <param name="p2y">The p2y.</param>
        /// <param name="p3x">The P3X.</param>
        /// <param name="p3y">The p3y.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        protected override void OnCurve4(short p2x, short p2y, short p3x, short p3y, short x, short y)
        {
            var endPoint = this.offset + (new Vector2(x, y) * this.scale);
            var c1 = this.offset + (new Vector2(p2x, p2y) * this.scale);
            var c2 = this.offset + (new Vector2(p3x, p3y) * this.scale);

            this.segments.Add(new BezierLineSegment(this.lastPoint, c1, c2, endPoint));
            this.lastPoint = endPoint;
        }

        /// <summary>
        /// Called when [line to].
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        protected override void OnLineTo(short x, short y)
        {
            var endPoint = this.offset + (new Vector2(x, y) * this.scale);
            this.segments.Add(new LinearLineSegment(this.lastPoint, endPoint));
            this.lastPoint = endPoint;
        }

        /// <summary>
        /// Called when [move to].
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        protected override void OnMoveTo(short x, short y)
        {
            // we close of the current segemnts in here
            if (this.segments.Any())
            {
                this.polygons.Add(new Polygon(this.segments.ToArray()));
                this.segments.Clear();
            }

            this.lastPoint = this.offset + (new Vector2(x, y) * this.scale);
        }
    }
}
