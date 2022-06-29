// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Configuration options for decoding Tiff images.
    /// </summary>
    public sealed class TiffDecoderOptions : ISpecializedDecoderOptions
    {
        /// <inheritdoc/>
        public DecoderOptions GeneralOptions { get; set; } = new();
    }
}
