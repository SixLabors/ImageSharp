// <copyright file="TiffFillOrder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Enumeration representing the fill orders defined by the Tiff file-format.
    /// </summary>
    internal enum TiffFillOrder
    {
        /// <summary>
        /// Pixels with lower column values are stored in the higher-order bits of the byte.
        /// </summary>
        MostSignificantBitFirst = 1,

        /// <summary>
        /// Pixels with lower column values are stored in the lower-order bits of the byte.
        /// </summary>
        LeastSignificantBitFirst = 2
    }
}