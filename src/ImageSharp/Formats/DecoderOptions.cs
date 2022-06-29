// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Provides general configuration options for decoding image formats.
    /// </summary>
    public sealed class DecoderOptions
    {
        /// <summary>
        /// Gets or sets a custom Configuration instance to be used by the image processing pipeline.
        /// </summary>
        public Configuration Configuration { get; set; } = Configuration.Default;

        /// <summary>
        /// Gets or sets the target size to decode the image into.
        /// </summary>
        public Size? TargetSize { get; set; } = null;

        /// <summary>
        /// Gets or sets a value indicating whether to ignore encoded metadata when decoding.
        /// </summary>
        public bool SkipMetadata { get; set; } = false;

        /// <summary>
        /// Gets or sets the number of image frames to decode.
        /// Leave <see langword="null"/> to decode all frames.
        /// </summary>
        // supported decoders may handle this internally but we will fallback to removeing additional frames after decode.
        public int? MaxFrames { get; set; } = null;
    }
}
