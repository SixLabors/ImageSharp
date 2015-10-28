// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PngChunk.cs" company="James South">
//   Copyright (c) James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Stores header information about a chunk.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Stores header information about a chunk.
    /// </summary>
    internal sealed class PngChunk
    {
        /// <summary>
        /// Gets or sets the length.
        /// An unsigned integer giving the number of bytes in the chunk's 
        /// data field. The length counts only the data field, not itself, 
        /// the chunk type code, or the CRC. Zero is a valid length
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Gets or sets the chunk type as string with 4 chars.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the data bytes appropriate to the chunk type, if any. 
        /// This field can be of zero length. 
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Gets or sets a CRC (Cyclic Redundancy Check) calculated on the preceding bytes in the chunk, 
        /// including the chunk type code and chunk data fields, but not including the length field. 
        /// The CRC is always present, even for chunks containing no data
        /// </summary>
        public uint Crc { get; set; }
    }
}
