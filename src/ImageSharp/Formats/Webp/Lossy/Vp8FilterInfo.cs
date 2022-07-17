// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Filter information.
    /// </summary>
    internal class Vp8FilterInfo : IDeepCloneable
    {
        private byte limit;

        private byte innerLevel;

        private byte highEdgeVarianceThreshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8FilterInfo"/> class.
        /// </summary>
        public Vp8FilterInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8FilterInfo"/> class.
        /// </summary>
        /// <param name="other">The filter info to create a copy from.</param>
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
        public byte Limit
        {
            get => this.limit;
            set
            {
                Guard.MustBeBetweenOrEqualTo(value, (byte)0, (byte)189, nameof(this.Limit));
                this.limit = value;
            }
        }

        /// <summary>
        /// Gets or sets the inner limit in [1..63], or 0 if no filtering.
        /// </summary>
        public byte InnerLevel
        {
            get => this.innerLevel;
            set
            {
                Guard.MustBeBetweenOrEqualTo(value, (byte)0, (byte)63, nameof(this.InnerLevel));
                this.innerLevel = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to do inner filtering.
        /// </summary>
        public bool UseInnerFiltering { get; set; }

        /// <summary>
        /// Gets or sets the high edge variance threshold in [0..2].
        /// </summary>
        public byte HighEdgeVarianceThreshold
        {
            get => this.highEdgeVarianceThreshold;
            set
            {
                Guard.MustBeBetweenOrEqualTo(value, (byte)0, (byte)2, nameof(this.HighEdgeVarianceThreshold));
                this.highEdgeVarianceThreshold = value;
            }
        }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new Vp8FilterInfo(this);
    }
}
