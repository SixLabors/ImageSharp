// <copyright file="TextGraphicsOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    /// <summary>
    /// Options for influencing the drawing functions.
    /// </summary>
    public struct TextGraphicsOptions
    {
        /// <summary>
        /// Represents the default <see cref="TextGraphicsOptions"/>.
        /// </summary>
        public static readonly TextGraphicsOptions Default = new TextGraphicsOptions(true);

        /// <summary>
        /// Whether antialiasing should be applied.
        /// </summary>
        public bool Antialias;

        /// <summary>
        /// Whether the text should be drawing with kerning enabled.
        /// </summary>
        public bool ApplyKerning;

        /// <summary>
        /// The number of space widths a tab should lock to.
        /// </summary>
        public float TabWidth;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextGraphicsOptions" /> struct.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        public TextGraphicsOptions(bool enableAntialiasing)
        {
            this.Antialias = enableAntialiasing;
            this.ApplyKerning = true;
            this.TabWidth = 4;
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="GraphicsOptions"/> to <see cref="TextGraphicsOptions"/>.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator TextGraphicsOptions(GraphicsOptions options)
        {
            return new TextGraphicsOptions(options.Antialias);
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="TextGraphicsOptions"/> to <see cref="GraphicsOptions"/>.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator GraphicsOptions(TextGraphicsOptions options)
        {
            return new GraphicsOptions(options.Antialias);
        }
    }
}