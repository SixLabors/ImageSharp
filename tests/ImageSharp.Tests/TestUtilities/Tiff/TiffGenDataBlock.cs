// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System.Collections.Generic;

    /// <summary>
    /// A utility data structure to represent an independent block of data in a Tiff file.
    /// These may be located in any order within a Tiff file.
    /// </summary>
    internal class TiffGenDataBlock
    {
        public TiffGenDataBlock(byte[] bytes)
        {
            this.Bytes = bytes;
            this.References = new List<TiffGenDataReference>();
        }

        public byte[] Bytes { get; }
        public IList<TiffGenDataReference> References { get; }

        public void AddReference(byte[] bytes, int offset)
        {
            References.Add(new TiffGenDataReference(bytes, offset));
        }
    }
}