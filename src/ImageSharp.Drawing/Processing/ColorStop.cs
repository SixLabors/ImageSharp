// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A struct that defines a single color stop.
    /// </summary>
    [DebuggerDisplay("ColorStop({Ratio} -> {Color}")]
    public readonly struct ColorStop
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ColorStop" /> struct.
        /// </summary>
        /// <param name="ratio">Where should it be? 0 is at the start, 1 at the end of the Gradient.</param>
        /// <param name="color">What color should be used at that point?</param>
        public ColorStop(float ratio, in Color color)
        {
            this.Ratio = ratio;
            this.Color = color;
        }

        /// <summary>
        /// Gets the point along the defined gradient axis.
        /// </summary>
        public float Ratio { get; }

        /// <summary>
        /// Gets the color to be used.
        /// </summary>
        public Color Color { get; }
    }
}