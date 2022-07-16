// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// The byte order of the data stream.
    /// </summary>
    public enum ByteOrder
    {
        /// <summary>
        /// The big-endian byte order (Motorola).
        /// Most-significant byte comes first, and ends with the least-significant byte.
        /// </summary>
        BigEndian,

        /// <summary>
        /// The little-endian byte order (Intel).
        /// Least-significant byte comes first and ends with the most-significant byte.
        /// </summary>
        LittleEndian
    }
}
