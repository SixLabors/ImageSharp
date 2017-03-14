// <copyright file="ByteArrayUtility.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
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