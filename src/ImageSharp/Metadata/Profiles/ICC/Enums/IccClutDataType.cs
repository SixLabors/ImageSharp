// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Color lookup table data type
    /// </summary>
    internal enum IccClutDataType
    {
        /// <summary>
        /// 32bit floating point
        /// </summary>
        Float,

        /// <summary>
        /// 8bit unsigned integer (byte)
        /// </summary>
        UInt8,

        /// <summary>
        /// 16bit unsigned integer (ushort)
        /// </summary>
        UInt16,
    }
}
