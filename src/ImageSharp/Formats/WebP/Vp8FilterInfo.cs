// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Filter information.
    /// </summary>
    internal class Vp8FilterInfo : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8FilterInfo"/> class.
        /// </summary>
        public Vp8FilterInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8FilterInfo"/> class.
        /// </summary>
        /// <param name="other">The filter info to create an instance from.</param>
        public Vp8FilterInfo(Vp8FilterInfo other)
        {
            this.Limit = other.Limit;
            this.HighEdgeVarianceThreshold = other.HighEdgeVarianceThreshold;
            this.InnerLevel = other.InnerLevel;
            this.UseInnerFiltering = other.UseInnerFiltering;
        }

        /// <summary>
        /// Gets or sets the filter limit in [3..189], or 0 if no filtering.
        /// </summary>
        public byte Limit { get; set; }

        /// <summary>
        /// Gets or sets the inner limit in [1..63].
        /// </summary>
        public byte InnerLevel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to do inner filtering.
        /// TODO: can this be a bool?
        /// </summary>
        public byte UseInnerFiltering { get; set; }

        /// <summary>
        /// Gets or sets the high edge variance threshold in [0..2].
        /// </summary>
        public byte HighEdgeVarianceThreshold { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new Vp8FilterInfo(this);
    }
}
