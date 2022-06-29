// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Configuration options for decoding Webp images.
    /// </summary>
    public sealed class WebpDecoderOptions : ISpecializedDecoderOptions
    {
        /// <inheritdoc/>
        public DecoderOptions GeneralOptions { get; set; } = new();
    }
}
