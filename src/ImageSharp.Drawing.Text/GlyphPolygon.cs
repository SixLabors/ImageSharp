// <copyright file="GlyphPolygon.cs" company="James Jackson-South">
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

    /// <summary>
    /// a specailist polygon that understand typeface glyphs and knows that they should never overlap.
    /// </summary>
    /// <seealso cref="ImageSharp.Drawing.Shapes.IShape" />
    internal class GlyphPolygon : IShape
    {
        private readonly Polygon[] polygons;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlyphPolygon" /> class.
        /// </summary>
        /// <param name="character">The character.</param>
        /// <param name="index">The index.</param>
        /// <param name="polygons">The polygons.</param>
        public GlyphPolygon(char character, ushort index, Polygon[] polygons)
        {
            this.GlyphIndex = index;
            this.Character = character;
            this.polygons = polygons;
            this.IsEmpty = false;

            var minX = this.polygons.Min(x => x.Bounds.Left);
            var maxX = this.polygons.Max(x => x.Bounds.Right);
            var minY = this.polygons.Min(x => x.Bounds.Top);
            var maxY = this.polygons.Max(x => x.Bounds.Bottom);

            this.Bounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GlyphPolygon" /> class.
        /// </summary>
        /// <param name="character">The character.</param>
        /// <param name="index">The index.</param>
        public GlyphPolygon(char character, ushort index)
        {
            this.GlyphIndex = index;
            this.Character = character;
            this.IsEmpty = true;
            this.Bounds = RectangleF.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GlyphPolygon" /> class.
        /// </summary>
        /// <param name="srcGlyph">The source glyph.</param>
        /// <param name="offset">The offset.</param>
        public GlyphPolygon(GlyphPolygon srcGlyph, Vector2 offset)
            : this(srcGlyph.Character, srcGlyph.GlyphIndex, Translate(srcGlyph.polygons, offset))
        {
        }

        /// <summary>
        /// Gets the character this glyph represents
        /// </summary>
        /// <value>
        /// The character.
        /// </value>
        public char Character { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is empty.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is empty; otherwise, <c>false</c>.
        /// </value>
        public bool IsEmpty { get; }

        /// <summary>
        /// Gets the bounding box of this shape.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        public RectangleF Bounds { get; }

        /// <summary>
        /// Gets the index of the glyph within its typeface.
        /// </summary>
        /// <value>
        /// The index of the glyph.
        /// </value>
        public ushort GlyphIndex { get; }

        /// <summary>
        /// the distance of the point from the outline of the shape, if the value is negative it is inside the polygon bounds
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// Returns the distance from the shape to the point
        /// </returns>
        public float Distance(Vector2 point)
        {
            float dist = float.MaxValue;
            bool inside = false;
            foreach (var shape in this.polygons)
            {
                var d = shape.Distance(point);

                if (d <= 0)
                {
                    // we are inside a poly
                    d = -d;  // flip the sign
                    inside ^= true; // flip the inside flag
                }

                if (d < dist)
                {
                    dist = d;
                }
            }

            if (inside)
            {
                return -dist;
            }

            return dist;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<IPath> GetEnumerator()
        {
            return ((IEnumerable<IPath>)this.polygons).GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.polygons.GetEnumerator();
        }

        private static Polygon[] Translate(Polygon[] polygons, Vector2 offset)
        {
            var translatedPolygons = new Polygon[polygons.Length];
            var len = polygons.Length;
            for (var i = 0; i < len; i++)
            {
                translatedPolygons[i] = new Polygon(polygons[i], offset);
            }

            return translatedPolygons;
        }
    }
}
