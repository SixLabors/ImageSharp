// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Configuration options for decoding Gif images.
    /// </summary>
    public sealed class GifDecoderOptions : ISpecializedDecoderOptions
    {
        /// <inheritdoc/>
        public DecoderOptions GeneralOptions { get; set; } = new();
    }
}
