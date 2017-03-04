// <copyright file="TiffGenDataReference.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;

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