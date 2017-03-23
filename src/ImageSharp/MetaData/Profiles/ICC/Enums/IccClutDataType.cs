// <copyright file="IccClutDataType.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
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
