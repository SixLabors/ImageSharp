// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Data structure for holding details of each TIFF IFD.
    /// </summary>
    internal class TiffIfd
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffIfd"/> class.
        /// </summary>
        /// <param name="entries">An array of the entries within the IFD.</param>
        /// <param name="nextIfdOffset">Offset (in bytes) to the next IFD, or zero if this is the last IFD.</param>
        public TiffIfd(TiffIfdEntry[] entries, uint nextIfdOffset)
        {
            this.Entries = new TiffIfdEntriesContainer(entries);
            this.NextIfdOffset = nextIfdOffset;
        }

        /// <summary>
        /// Gets an array of the entries within this IFD.
        /// </summary>
        public TiffIfdEntriesContainer Entries { get; }

        /// <summary>
        /// Gets the offset (in bytes) to the next IFD, or zero if this is the last IFD.
        /// </summary>
        public uint NextIfdOffset { get; }

        /// <summary>
        /// Reads a <see cref="TiffIfd"/> from the input stream.
        /// </summary>
        /// <returns>A <see cref="TiffIfd"/> containing the retrieved data.</returns>
        public static TiffIfd Read(TiffStream stream)
        {
            long pos = stream.Position;

            ushort entryCount = stream.ReadUInt16();
            var entries = new TiffIfdEntry[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                entries[i] = TiffIfdEntry.Read(stream);
            }

            uint nextIfdOffset = stream.ReadUInt32();

            int ifdSize = 2 + (entryCount * TiffConstants.SizeOfIfdEntry) + 4;
            int readedBytes = (int)(stream.Position - pos);
            int leftBytes = ifdSize - readedBytes;
            if (leftBytes > 0)
            {
                stream.Skip(leftBytes);
            }
            else if (leftBytes < 0)
            {
                throw new InvalidDataException("Out of range of IFD structure.");
            }

            return new TiffIfd(entries, nextIfdOffset);
        }
    }
}
