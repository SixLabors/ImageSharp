// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Holds the tree header in coded form.
    /// </summary>
    [DebuggerDisplay("Code = {Code}, ExtraBits = {ExtraBits}")]
    internal class HuffmanTreeToken
    {
        /// <summary>
        /// Gets or sets the code. Value (0..15) or escape code (16, 17, 18).
        /// </summary>
        public byte Code { get; set; }

        /// <summary>
        /// Gets or sets the extra bits for escape codes.
        /// </summary>
        public byte ExtraBits { get; set; }
    }
}
