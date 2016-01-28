// <copyright file="Endianness.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.IO
{
    /// <summary>
    /// Endianness of a converter
    /// </summary>
    internal enum Endianness
    {
        /// <summary>
        /// Little endian - least significant byte first
        /// </summary>
        LittleEndian,

        /// <summary>
        /// Big endian - most significant byte first
        /// </summary>
        BigEndian
    }
}
