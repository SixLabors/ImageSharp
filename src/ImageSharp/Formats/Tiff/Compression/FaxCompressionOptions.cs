// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Fax compression options, see TIFF spec page 51f (T4Options).
    /// </summary>
    [Flags]
    public enum FaxCompressionOptions : uint
    {
        /// <summary>
        /// No options.
        /// </summary>
        None = 0,

        /// <summary>
        /// If set, 2-dimensional coding is used (otherwise 1-dimensional is assumed).
        /// </summary>
        TwoDimensionalCoding = 1,

        /// <summary>
        /// If set, uncompressed mode is used.
        /// </summary>
        UncompressedMode = 2,

        /// <summary>
        /// If set, fill bits have been added as necessary before EOL codes such that
        /// EOL always ends on a byte boundary, thus ensuring an EOL-sequence of 1 byte
        /// preceded by a zero nibble: xxxx-0000 0000-0001.
        /// </summary>
        EolPadding = 4
    }
}
