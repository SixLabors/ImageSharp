// <copyright file="TiffIfdEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Data structure for holding details of each TIFF IFD entry
    /// </summary>
    internal struct TiffIfdEntry
    {
        public ushort Tag;
        public TiffType Type;
        public uint Count;
        public byte[] Value;

        public TiffIfdEntry(ushort tag, TiffType type, uint count, byte[] value)
        {
            this.Tag = tag;
            this.Type = type;
            this.Count = count;
            this.Value = value;
        }
    }
}
