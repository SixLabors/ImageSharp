// <copyright file="DecoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Encapsulates the shared decoder options.
    /// </summary>
    public class DecoderOptions : IDecoderOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DecoderOptions"/> class.
        /// </summary>
        public DecoderOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DecoderOptions"/> class.
        /// </summary>
        /// <param name="options">The decoder options</param>
        protected DecoderOptions(IDecoderOptions options)
        {
            if (options != null)
            {
                this.IgnoreMetadata = options.IgnoreMetadata;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; } = false;
    }
}
