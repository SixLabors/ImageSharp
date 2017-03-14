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
        // TIFF baseline FillOrder values

        MostSignificantBitFirst = 1,
        LeastSignificantBitFirst = 2
    }
}