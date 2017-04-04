// <copyright file="TiffExtraSamples.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Enumeration representing the possible uses of extra components in TIFF format files.
    /// </summary>
    internal enum TiffExtraSamples
    {
        /// <summary>
        /// Unspecified data.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Associated alpha data (with pre-multiplied color).
        /// </summary>
        AssociatedAlpha = 1,

        /// <summary>
        /// Unassociated alpha data.
        /// </summary>
        UnassociatedAlpha = 2
    }
}