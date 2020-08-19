// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System.Linq;

    /// <summary>
    /// A utility class for manipulating in-memory Tiff files for use in unit tests.
    /// </summary>
    internal static class TiffGenIfdExtensions
    {
        public static TiffGenIfd WithoutEntry(this TiffGenIfd ifd, ushort tag)
        {
            TiffGenEntry entry = ifd.Entries.FirstOrDefault(e => e.Tag == tag);
            if (entry != null)
            {
                ifd.Entries.Remove(entry);
            }
            return ifd;
        }

        public static TiffGenIfd WithEntry(this TiffGenIfd ifd, TiffGenEntry entry)
        {
            ifd.WithoutEntry(entry.Tag);
            ifd.Entries.Add(entry);

            return ifd;
        }
    }
}