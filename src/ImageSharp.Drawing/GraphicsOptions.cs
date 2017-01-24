// <copyright file="GraphicsOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    /// <summary>
    /// Options for influencing the drawing functions.
    /// </summary>
    public struct GraphicsOptions
    {
        /// <summary>
        /// Represents the default <see cref="GraphicsOptions"/>.
        /// </summary>
        public static readonly GraphicsOptions Default = new GraphicsOptions(true);

        /// <summary>
        /// Whether antialiasing should be applied.
        /// </summary>
        public bool Antialias;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsOptions"/> struct.
        /// </summary>
        /// <param name="enableAntialiasing">If set to <c>true</c> [enable antialiasing].</param>
        public GraphicsOptions(bool enableAntialiasing)
        {
            this.Antialias = enableAntialiasing;
        }
    }
}