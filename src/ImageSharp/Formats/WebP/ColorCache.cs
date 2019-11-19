// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class ColorCache
    {
        /// <summary>
        /// Color entries.
        /// </summary>
        public List<uint> Colors { get; set; }

        /// <summary>
        /// Hash shift: 32 - hashBits.
        /// </summary>
        public uint HashShift { get; set; }

        public uint HashBits { get; set; }
    }
}
