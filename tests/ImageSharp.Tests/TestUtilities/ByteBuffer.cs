// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.TestUtilities;

using System;
using System.Collections.Generic;

public class ByteBuffer
{
    private readonly List<byte> bytes = new();
    private readonly bool isLittleEndian;

    public ByteBuffer(bool isLittleEndian)
    {
        this.isLittleEndian = isLittleEndian;
    }

    public void AddByte(byte value)
    {
        this.bytes.Add(value);
    }

    public void AddUInt16(ushort value)
    {
        this.bytes.AddRange(BitConverter.GetBytes(value).WithByteOrder(this.isLittleEndian));
    }

    public void AddUInt32(uint value)
    {
        this.bytes.AddRange(BitConverter.GetBytes(value).WithByteOrder(this.isLittleEndian));
    }

    public byte[] ToArray()
    {
        return this.bytes.ToArray();
    }
}
