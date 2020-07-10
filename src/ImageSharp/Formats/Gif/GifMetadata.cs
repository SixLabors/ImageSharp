// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Provides Gif specific metadata information for the image.
    /// </summary>
    public class GifMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GifMetadata"/> class.
        /// </summary>
        public GifMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GifMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private GifMetadata(GifMetadata other)
        {
            this.RepeatCount = other.RepeatCount;
            this.ColorTableMode = other.ColorTableMode;
            this.GlobalColorTableLength = other.GlobalColorTableLength;

            for (int i = 0; i < other.Comments.Count; i++)
            {
                this.Comments.Add(other.Comments[i]);
            }
        }

        /// <summary>
        /// Gets or sets the number of times any animation is repeated.
        /// <remarks>
        /// 0 means to repeat indefinitely, count is set as repeat n-1 times. Defaults to 1.
        /// </remarks>
        /// </summary>
        public ushort RepeatCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets the color table mode.
        /// </summary>
        public GifColorTableMode ColorTableMode { get; set; }

        /// <summary>
        /// Gets or sets the length of the global color table if present.
        /// </summary>
        public int GlobalColorTableLength { get; set; }

        /// <summary>
        /// Gets or sets the the collection of comments about the graphics, credits, descriptions or any
        /// other type of non-control and non-graphic data.
        /// </summary>
        public IList<string> Comments { get; set; } = new List<string>();

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new GifMetadata(this);
    }
}
