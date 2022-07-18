// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Provides general configuration options for decoding image formats.
    /// </summary>
    public sealed class DecoderOptions
    {
        private static readonly Lazy<DecoderOptions> LazyOptions = new(() => new());

        private uint maxFrames = int.MaxValue;

        /// <summary>
        /// Gets the shared default general decoder options instance.
        /// </summary>
        public static DecoderOptions Default { get; } = LazyOptions.Value;

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
        /// Gets or sets the maximum number of image frames to decode, inclusive.
        /// </summary>
        public uint MaxFrames { get => this.maxFrames; set => this.maxFrames = Math.Clamp(value, 1, int.MaxValue); }
    }
}
