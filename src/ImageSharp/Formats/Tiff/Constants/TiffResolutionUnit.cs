// <copyright file="TiffResolutionUnit.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Enumeration representing the resolution units defined by the Tiff file-format.
    /// </summary>
    internal enum TiffResolutionUnit
    {
        // TIFF baseline ResolutionUnit values

        None = 1,
        Inch = 2,
        Centimeter = 2
    }
}