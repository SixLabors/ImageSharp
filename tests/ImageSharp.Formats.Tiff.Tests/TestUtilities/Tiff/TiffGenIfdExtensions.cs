// <copyright file="TiffGenIfdExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// A utility class for manipulating in-memory Tiff files for use in unit tests.
    /// </summary>
    internal static class TiffGenIfdExtensions
    {
        public static TiffGenIfd WithoutEntry(this TiffGenIfd ifd, ushort tag)
        {
            TiffGenEntry entry = ifd.Entries.First(e => e.Tag == tag);
            ifd.Entries.Remove(entry);
            return ifd;
        }
    }
}