// <copyright file="InnerFont.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using ImageSharp.Drawing.Paths;
    using ImageSharp.Drawing.Shapes;
    using NOpenType;

    /// <summary>
    /// Provides access to a loaded font and provides configuration options for how it should be rendered.
    /// </summary>
    internal sealed class InnerFont
    {
        private readonly Dictionary<float, GlyphPathBuilderPolygons> glyphBuilders;
        private readonly Typeface typeface;

        /// <summary>
        /// Initializes a new instance of the <see cref="InnerFont" /> class.
        /// </summary>
        /// <param name="fontStream">The font stream.</param>
        public InnerFont(Stream fontStream)
        {
            this.typeface = OpenTypeReader.Read(fontStream);

            this.glyphBuilders = new Dictionary<float, GlyphPathBuilderPolygons>();
        }

        /// <summary>
        /// Gets the font family.
        /// </summary>
        /// <value>
        /// The font family.
        /// </value>
        public string FontFamily => this.typeface.Name;

        /// <summary>
        /// Gets the font veriant.
        /// </summary>
        /// <value>
        /// The font veriant.
        /// </value>
        public string FontVeriant => this.typeface.FontSubFamily;

        /// <summary>
        /// Measures the text with settings from the font.
        /// </summary>
        /// <param name="text">The text to mesure.</param>
        /// <param name="font">The font.</param>
        /// <returns>
        /// a <see cref="SizeF" /> of the mesured height and with of the text
        /// </returns>
        public SizeF Measure(string text, Font font)
        {
            var shapes = this.GenerateContours(text, font);
            RectangleF fillBounds;
            if (shapes.Length == 1)
            {
                fillBounds = shapes[0].Bounds;
            }
            else
            {
                var polysmaxX = shapes.Max(x => x.Bounds.Right);
                var polysminX = shapes.Min(x => x.Bounds.Left);
                var polysmaxY = shapes.Max(x => x.Bounds.Bottom);
                var polysminY = shapes.Min(x => x.Bounds.Top);

                fillBounds = new RectangleF(polysminX, polysminY, polysmaxX - polysminX, polysmaxY - polysminY);
            }

            return fillBounds.Size;
        }

        /// <summary>
        /// Generates the contours.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="font">The font settings to use to generate .</param>
        /// <returns>
        /// Returns a collection of shapes making up each glyph and the realtive posion to the origin 0,0.
        /// </returns>
        public IShape[] GenerateContours(string str, Font font)
        {
            GlyphPathBuilderPolygons builder;
            if (this.glyphBuilders.ContainsKey(font.Size))
            {
                builder = this.glyphBuilders[font.Size];
            }
            else
            {
                lock (this.glyphBuilders)
                {
                    if (this.glyphBuilders.ContainsKey(font.Size))
                    {
                        builder = this.glyphBuilders[font.Size];
                    }
                    else
                    {
                        builder = new GlyphPathBuilderPolygons(this.typeface, font.Size);
                        this.glyphBuilders.Add(font.Size, builder);
                    }
                }
            }

            var charCount = str.Length;
            bool enable_kerning = font.EnableKerning;
            ushort prevIdx = 0;

            // TODO add support for clipping (complex polygons should help here)
            // TODO add support for wrapping (line heights)
            var glyphs = new List<GlyphPolygon>(charCount); // can't be more that charCount in length

            float scale = this.typeface.CalculateScale(font.Size);

            float computedLineHeight = font.LineHeight * font.Size;
            bool startOfLine = true;

            var spaceIndex = (ushort)this.typeface.LookupIndex(' ');
            var spaceWidth = this.typeface.GetAdvanceWidthFromGlyphIndex(spaceIndex) * scale;
            var tabWidth = spaceWidth * font.TabWidth;
            Vector2 offset = Vector2.Zero;
            for (int i = 0; i < charCount; ++i)
            {
                char c = str[i];
                bool doKerning = enable_kerning && !startOfLine;
                startOfLine = false;

                switch (c)
                {
                    case '\n':
                        offset.Y += computedLineHeight;
                        offset.X = 0;
                        startOfLine = true;
                        break;
                    case '\r':
                        // ignore '\r's
                        break;
                    case ' ':
                        offset.X += spaceWidth;
                        prevIdx = spaceIndex;
                        break;
                    case '\t':
                        var diff = offset.X % tabWidth;
                        
                        var newWidth = offset.X + diff; 
                        if (newWidth == offset.X)
                        {
                            offset.X += tabWidth;
                        }else
                        {
                            offset.X = newWidth;
                        }
                        prevIdx = spaceIndex;
                        break;
                    default:

                        var srcGlyph = builder.BuildGlyph(c);

                        // srcGlyph.IsEmpty means that the glyph doesn't actualy have any visible features (space for example)
                        if (!srcGlyph.IsEmpty)
                        {
                            if (offset == Vector2.Zero)
                            {
                                // if we don't have to move this from the current position (will always happen for the first character without any form of justification)
                                // then don't bother wrapping it as an ofset glyph
                                glyphs.Add(srcGlyph);
                            }
                            else
                            {
                                // move the offset back to compinsate for Kerning between this char and the previous
                                if (doKerning)
                                {
                                    // check kerning
                                    offset.X += this.typeface.GetKernDistance(prevIdx, srcGlyph.GlyphIndex) * scale;
                                }

                                glyphs.Add(new GlyphPolygon(srcGlyph, offset));
                            }
                        }

                        // move the offset for the next chartacter by the standard offset for this glyph
                        offset.X += this.typeface.GetAdvanceWidthFromGlyphIndex(srcGlyph.GlyphIndex) * scale;
                        prevIdx = srcGlyph.GlyphIndex;
                        break;
                }
            }

            return glyphs.ToArray();
        }
    }
}