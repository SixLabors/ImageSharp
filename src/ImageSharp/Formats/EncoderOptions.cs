// <copyright file="EncoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Encapsulates the shared encoder options.
    /// </summary>
    public class EncoderOptions : IEncoderOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EncoderOptions"/> class.
        /// </summary>
        public EncoderOptions()
        {
            this.InitializeWithDefaults();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncoderOptions"/> class.
        /// </summary>
        /// <param name="options">The encoder options</param>
        protected EncoderOptions(IEncoderOptions options)
        {
            if (options == null)
            {
                this.InitializeWithDefaults();
                return;
            }

            this.IgnoreMetadata = options.IgnoreMetadata;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being encoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        private void InitializeWithDefaults()
        {
            this.IgnoreMetadata = false;
        }
    }
}
