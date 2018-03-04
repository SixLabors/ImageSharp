// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Data structure for holding details of each TIFF IFD entry.
    /// </summary>
    internal struct TiffIfdEntry
    {
        /// <summary>
        /// The Tag ID for this entry. See <see cref="TiffTags"/> for typical values.
        /// </summary>
        public ushort Tag;

        /// <summary>
        /// The data-type of this entry.
        /// </summary>
        public TiffType Type;

        /// <summary>
        /// The number of array items in this entry, or one if only a single value.
        /// </summary>
        public uint Count;

        /// <summary>
        /// The raw byte data for this entry.
        /// </summary>
        public byte[] Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffIfdEntry"/> struct.
        /// </summary>
        /// <param name="tag">The Tag ID for this entry.</param>
        /// <param name="type">The data-type of this entry.</param>
        /// <param name="count">The number of array items in this entry.</param>
        /// <param name="value">The raw byte data for this entry.</param>
        public TiffIfdEntry(ushort tag, TiffType type, uint count, byte[] value)
        {
            this.Tag = tag;
            this.Type = type;
            this.Count = count;
            this.Value = value;
        }
    }
}
