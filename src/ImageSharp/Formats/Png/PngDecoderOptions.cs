// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Configuration options for decoding Png images.
    /// </summary>
    public sealed class PngDecoderOptions : ISpecializedDecoderOptions
    {
        /// <inheritdoc/>
        public DecoderOptions GeneralOptions { get; set; } = new();
    }
}
