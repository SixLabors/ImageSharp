// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Image decoder options for decoding Windows bitmap streams.
    /// </summary>
    public sealed class BmpDecoderOptions : ISpecializedDecoderOptions
    {
        /// <inheritdoc/>
        public DecoderOptions GeneralOptions { get; set; } = new();

        /// <summary>
        /// Gets the value indicating how to deal with skipped pixels,
        /// which can occur during decoding run length encoded bitmaps.
        /// </summary>
        public RleSkippedPixelHandling RleSkippedPixelHandling { get; }
    }
}
