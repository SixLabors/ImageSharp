// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
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
