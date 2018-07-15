// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Allows the approximate comparison of colorspace component values.
    /// </summary>
    internal class ApproximateColorSpaceComparer : IEqualityComparer<Rgb>
    {
        private readonly float Epsilon;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApproximateColorSpaceComparer"/> class.
        /// </summary>
        /// <param name="epsilon">The comparison error difference epsilon to use.</param>
        public ApproximateColorSpaceComparer(float epsilon = 1F)
        {
            this.Epsilon = epsilon;
        }

        /// <inheritdoc/>
        public bool Equals(Rgb x, Rgb y)
        {
            return this.Equals(x.R, y.R)
             && this.Equals(x.G, y.G)
             && this.Equals(x.B, y.B);
        }

        /// <inheritdoc/>
        public int GetHashCode(Rgb obj)
        {
            return obj.GetHashCode();
        }

        private bool Equals(float x, float y)
        {
            float d = x - y;

            return d >= -this.Epsilon && d <= this.Epsilon;
        }
    }
}