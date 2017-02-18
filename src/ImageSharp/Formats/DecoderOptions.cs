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
        /// Gets the default decoder options
        /// </summary>
        public static DecoderOptions Default
        {
            get
            {
                return new DecoderOptions()
                {
                    IgnoreMetadata = false
                };
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }
    }
}
