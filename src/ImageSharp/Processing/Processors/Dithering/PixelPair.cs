// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Represents a composite pair of pixels. Used for caching color distance lookups.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal readonly struct PixelPair<TPixel> : IEquatable<PixelPair<TPixel>>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelPair{TPixel}"/> struct.
        /// </summary>
        /// <param name="first">The first pixel color</param>
        /// <param name="second">The second pixel color</param>
        public PixelPair(TPixel first, TPixel second)
        {
            this.First = first;
            this.Second = second;
        }

        /// <summary>
        /// Gets the first pixel color
        /// </summary>
        public TPixel First { get; }

        /// <summary>
        /// Gets the second pixel color
        /// </summary>
        public TPixel Second { get; }

        /// <inheritdoc/>
        public bool Equals(PixelPair<TPixel> other)
            => this.First.Equals(other.First) && this.Second.Equals(other.Second);

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is PixelPair<TPixel> other && this.First.Equals(other.First) && this.Second.Equals(other.Second);

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashHelpers.Combine(this.First.GetHashCode(), this.Second.GetHashCode());
    }
}