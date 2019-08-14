// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Data structure for holding details of each TIFF IFD.
    /// </summary>
    internal static class TiffFileFormatReader
    {
        /// <summary>
        /// Reads the file IFDs.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <param name="stream">The stream.</param>
        public static TiffIfd[] ReadIfds(TiffHeader header, TiffStream stream)
        {
            DebugGuard.IsTrue(header.ByteOrder == stream.ByteOrder, "Byte orders must be equals.");

            uint ifdOffset = header.FirstIfdOffset;
            var list = new List<TiffIfd>();
            do
            {
                stream.Seek(ifdOffset);
                var ifd = TiffIfd.Read(stream);
                list.Add(ifd);
                ifdOffset = ifd.NextIfdOffset;
            }
            while (ifdOffset != 0);

            TiffIfd[] result = list.ToArray();

            // cache ext entries data
            foreach (TiffIfd ifd in result)
            {
                foreach (TiffIfdEntry entry in ifd.Entries.Entries)
                {
                    entry.ReadExtValueData(stream);
                }
            }

            return result;
        }
    }
}
