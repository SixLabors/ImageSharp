// <copyright file="TiffThreshholding.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Enumeration representing the threshholding types defined by the Tiff file-format.
    /// </summary>
    internal enum TiffThreshholding
    {
        /// TIFF baseline Threshholding values

        None = 1,
        Ordered = 2,
        Random = 3
    }
}