// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.TestUtilities;

using System;

public static class ByteArrayUtility
{
    public static byte[] WithByteOrder(this byte[] bytes, bool isLittleEndian)
    {
        if (isLittleEndian != BitConverter.IsLittleEndian)
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
