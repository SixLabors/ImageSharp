// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
