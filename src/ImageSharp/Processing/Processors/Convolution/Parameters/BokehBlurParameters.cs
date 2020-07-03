// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution.Parameters
{
    /// <summary>
    /// A <see langword="struct"/> that contains parameters to apply a bokeh blur filter
    /// </summary>
    internal readonly struct BokehBlurParameters : IEquatable<BokehBlurParameters>
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

        /// <inheritdoc/>
        public bool Equals(BokehBlurParameters other)
        {
            return this.Radius.Equals(other.Radius) && this.Components.Equals(other.Components);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is BokehBlurParameters other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Radius.GetHashCode() * 397) ^ this.Components.GetHashCode();
            }
        }
    }
}
