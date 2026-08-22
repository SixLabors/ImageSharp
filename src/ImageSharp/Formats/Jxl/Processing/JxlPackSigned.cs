// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Provides PackSigned and UnpackSigned methods.
/// </summary>
internal static class JxlPackSigned
{
    /// <summary>
    /// Encodes non-negative (X) into (2 * X), negative (-X) into (2 * X - 1)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint PackUnsigned(int x)
    {
        unchecked
        {
            uint value = (uint)x;
            return (value << 1) ^ ((~value >> 31) - 1);
        }
    }

    /// <summary>
    /// Reverse to PackSigned, i.e. UnpackSigned(PackSigned(X)) == X.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UnpackSigned(uint x)
    {
        unchecked
        {
            return (int)((x >> 1) ^ (((~x) & 1) - 1));
        }
    }
}
