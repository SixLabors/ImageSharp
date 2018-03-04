// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;

    public class ByteBuffer
    {
        List<byte> bytes = new List<byte>();
        bool isLittleEndian;

        public ByteBuffer(bool isLittleEndian)
        {
            this.isLittleEndian = isLittleEndian;
        }

        public void AddByte(byte value)
        {
            bytes.Add(value);
        }

        public void AddUInt16(ushort value)
        {
            bytes.AddRange(BitConverter.GetBytes(value).WithByteOrder(isLittleEndian));
        }

        public void AddUInt32(uint value)
        {
            bytes.AddRange(BitConverter.GetBytes(value).WithByteOrder(isLittleEndian));
        }

        public byte[] ToArray()
        {
            return bytes.ToArray();
        }
    }
}