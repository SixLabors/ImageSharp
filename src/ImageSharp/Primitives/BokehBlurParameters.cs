// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Primitives
{
    /// <summary>
    /// A <see langword="struct"/> that contains parameters to apply a bokeh blur filter
    /// </summary>
    public readonly struct BokehBlurParameters
    {
        /// <summary>
        /// The size of the convolution kernel to use when applying the bokeh blur
        /// </summary>
        public readonly int Radius;

        /// <summary>
        /// The number of complex components to use to approximate the bokeh kernel
        /// </summary>
        public readonly int Components;

        /// <summary>
        /// Initializes a new instance of the <see cref="BokehBlurParameters"/> struct.
        /// </summary>
        /// <param name="radius">The size of the kernel</param>
        /// <param name="components">The number of kernel components</param>
        public BokehBlurParameters(int radius, int components)
        {
            this.Radius = radius;
            this.Components = components;
        }
    }
}
