// <copyright file="TiffIfd.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Data structure for holding details of each TIFF IFD
    /// </summary>
    internal struct TiffIfd
    {
        public TiffIfdEntry[] Entries;
        public uint NextIfdOffset;

        public TiffIfd(TiffIfdEntry[] entries, uint nextIfdOffset)
        {
            this.Entries = entries;
            this.NextIfdOffset = nextIfdOffset;
        }
    }
}
