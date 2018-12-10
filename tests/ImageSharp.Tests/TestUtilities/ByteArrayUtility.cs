// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System;

    public static class ByteArrayUtility
    {
        public static byte[] WithByteOrder(this byte[] bytes, bool isLittleEndian)
        {
            if (BitConverter.IsLittleEndian != isLittleEndian)
            {
                byte[] reversedBytes = new byte[bytes.Length];
                Array.Copy(bytes, reversedBytes, bytes.Length);
                Array.Reverse(reversedBytes);
                return reversedBytes;
            }
            else
            {
                return bytes;
            }
        }
    }
}