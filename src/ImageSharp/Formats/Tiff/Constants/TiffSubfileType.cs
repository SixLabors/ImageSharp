// <copyright file="TiffSubfileType.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Enumeration representing the sub-file types defined by the Tiff file-format.
    /// </summary>
    internal enum TiffSubfileType
    {
        // TIFF baseline subfile types

        FullImage = 1,
        Preview = 2,
        SinglePage = 3
    }
}