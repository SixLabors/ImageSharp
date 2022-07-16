// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Detects Webp file headers.
    /// </summary>
    public sealed class WebpImageFormatDetector : IImageFormatDetector
    {
        /// <inheritdoc />
        public int HeaderSize => 12;

        /// <inheritdoc />
        public IImageFormat DetectFormat(ReadOnlySpan<byte> header) => this.IsSupportedFileFormat(header) ? WebpFormat.Instance : null;

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header) => header.Length >= this.HeaderSize && this.IsRiffContainer(header) && this.IsWebpFile(header);

        /// <summary>
        /// Checks, if the header starts with a valid RIFF FourCC.
        /// </summary>
        /// <param name="header">The header bytes.</param>
        /// <returns>True, if its a valid RIFF FourCC.</returns>
        private bool IsRiffContainer(ReadOnlySpan<byte> header) => header.Slice(0, 4).SequenceEqual(WebpConstants.RiffFourCc);

        /// <summary>
        /// Checks if 'WEBP' is present in the header.
        /// </summary>
        /// <param name="header">The header bytes.</param>
        /// <returns>True, if its a webp file.</returns>
        private bool IsWebpFile(ReadOnlySpan<byte> header) => header.Slice(8, 4).SequenceEqual(WebpConstants.WebpHeader);
    }
}
