// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Data structure for holding details of each TIFF IFD.
    /// </summary>
    internal struct TiffIfd
    {
        /// <summary>
        /// An array of the entries within this IFD.
        /// </summary>
        public TiffIfdEntry[] Entries;

        /// <summary>
        /// Offset (in bytes) to the next IFD, or zero if this is the last IFD.
        /// </summary>
        public uint NextIfdOffset;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffIfd"/> struct.
        /// </summary>
        /// <param name="entries">An array of the entries within the IFD.</param>
        /// <param name="nextIfdOffset">Offset (in bytes) to the next IFD, or zero if this is the last IFD.</param>
        public TiffIfd(TiffIfdEntry[] entries, uint nextIfdOffset)
        {
            this.Entries = entries;
            this.NextIfdOffset = nextIfdOffset;
        }

        /// <summary>
        /// Gets the child <see cref="TiffIfdEntry"/> with the specified tag ID.
        /// </summary>
        /// <param name="tag">The tag ID to search for.</param>
        /// <returns>The resulting <see cref="TiffIfdEntry"/>, or null if it does not exists.</returns>
        public TiffIfdEntry? GetIfdEntry(ushort tag)
        {
            for (int i = 0; i < this.Entries.Length; i++)
            {
                if (this.Entries[i].Tag == tag)
                {
                    return this.Entries[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the child <see cref="TiffIfdEntry"/> with the specified tag ID.
        /// </summary>
        /// <param name="tag">The tag ID to search for.</param>
        /// <param name="entry">The resulting <see cref="TiffIfdEntry"/>, if it exists.</param>
        /// <returns>A flag indicating whether the requested entry exists.</returns>
        public bool TryGetIfdEntry(ushort tag, out TiffIfdEntry entry)
        {
            TiffIfdEntry? nullableEntry = this.GetIfdEntry(tag);
            entry = nullableEntry ?? default(TiffIfdEntry);
            return nullableEntry.HasValue;
        }
    }
}
