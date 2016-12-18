// <copyright file="Font.cs" company="James Jackson-South">
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
    public sealed class Font
    {
        private readonly InnerFont innerFont;

        /// <summary>
        /// Initializes a new instance of the <see cref="Font"/> class.
        /// </summary>
        /// <param name="fontStream">The font stream.</param>
        public Font(Stream fontStream)
        {
            this.innerFont = new InnerFont(fontStream);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Font"/> class.
        /// </summary>
        /// <param name="prototype">The prototype from which to copy all the settings.</param>
        public Font(Font prototype)
        {
            // clone out the setting in here
            this.innerFont = prototype.innerFont;
            this.Size = prototype.Size;
            this.EnableKerning = prototype.EnableKerning;
        }

        /// <summary>
        /// Gets the font family.
        /// </summary>
        /// <value>
        /// The font family.
        /// </value>
        public string FontFamily => this.innerFont.FontFamily;

        /// <summary>
        /// Gets the font veriant.
        /// </summary>
        /// <value>
        /// The font veriant.
        /// </value>
        public string FontVeriant => this.innerFont.FontVeriant;

        /// <summary>
        /// Gets or sets the size. This defaults to 10.
        /// </summary>
        /// <value>
        /// The size.
        /// </value>
        public float Size { get; set; } = 10; // as good a size any for a defaut size.

        /// <summary>
        /// Gets or sets the height of the line in relation to the <see cref="Size"/>.
        /// </summary>
        /// <value>
        /// The height of the line.
        /// </value>
        public float LineHeight { get; set; } = 1.5f; // as good a size any for a defaut size.

        /// <summary>
        /// Gets or sets the width of the tab in number of spaces.
        /// </summary>
        /// <value>
        /// The width of the tab.
        /// </value>
        public float TabWidth { get; set; } = 4; // as good a size any for a defaut size.

        /// <summary>
        /// Gets or sets a value indicating whether to enable kerning. This defaults to true.
        /// </summary>
        /// <value>
        /// <c>true</c> if kerning is enabled otherwise <c>false</c>.
        /// </value>
        public bool EnableKerning { get; set; } = true;

        /// <summary>
        /// Measures the text with settings from the font.
        /// </summary>
        /// <param name="text">The text to mesure.</param>
        /// <returns>
        /// a <see cref="SizeF" /> of the mesured height and with of the text
        /// </returns>
        public SizeF Measure(string text)
        {
            return this.innerFont.Measure(text, this);
        }

        /// <summary>
        /// Generates the contours.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>
        /// Returns a collection of shapes making up each glyph and the realtive posion to the origin 0,0.
        /// </returns>
        public IShape[] GenerateContours(string text)
        {
            return this.innerFont.GenerateContours(text, this);
        }
    }
}