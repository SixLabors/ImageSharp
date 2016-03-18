// <copyright file="Box.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Quantizers
{
    /// <summary>
    /// Represents a box color cube.
    /// </summary>
    internal sealed class Box
    {
        /// <summary>
        /// Gets or sets the min red value, exclusive.
        /// </summary>
        public int R0 { get; set; }

        /// <summary>
        /// Gets or sets the max red value, inclusive.
        /// </summary>
        public int R1 { get; set; }

        /// <summary>
        /// Gets or sets the min green value, exclusive.
        /// </summary>
        public int G0 { get; set; }

        /// <summary>
        /// Gets or sets the max green value, inclusive.
        /// </summary>
        public int G1 { get; set; }

        /// <summary>
        /// Gets or sets the min blue value, exclusive.
        /// </summary>
        public int B0 { get; set; }

        /// <summary>
        /// Gets or sets the max blue value, inclusive.
        /// </summary>
        public int B1 { get; set; }

        /// <summary>
        /// Gets or sets the min alpha value, exclusive.
        /// </summary>
        public int A0 { get; set; }

        /// <summary>
        /// Gets or sets the max alpha value, inclusive.
        /// </summary>
        public int A1 { get; set; }

        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        public int Volume { get; set; }
    }
}
