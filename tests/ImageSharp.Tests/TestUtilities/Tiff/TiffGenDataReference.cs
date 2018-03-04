// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// A utility data structure to represent a reference from one block of data to another in a Tiff file.
    /// </summary>
    internal class TiffGenDataReference
    {
        public TiffGenDataReference(byte[] bytes, int offset)
        {
            this.Bytes = bytes;
            this.Offset = offset;
        }

        public byte[] Bytes { get; }
        public int Offset { get; }
    }
}