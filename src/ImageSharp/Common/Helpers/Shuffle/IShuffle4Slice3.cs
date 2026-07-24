// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

/// <summary>
/// Defines a stateless operation that reorders four packed components before retaining three.
/// </summary>
internal interface IShuffle4Slice3 : IComponentShuffle
{
}

/// <summary>
/// Preserves XYZ order and discards W.
/// </summary>
internal readonly struct XYZWShuffle4Slice3 : IShuffle4Slice3
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source) => source;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source) => source;
}

/// <summary>
/// Reorders XYZW components to YZW before discarding X.
/// </summary>
internal readonly struct YZWXShuffle4Slice3 : IShuffle4Slice3
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source) => BitOperations.RotateRight(source, 8);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128_.ShuffleNative(source, Vector128.Create((byte)1, 2, 3, 0, 5, 6, 7, 4, 9, 10, 11, 8, 13, 14, 15, 12));
}

/// <summary>
/// Reorders XYZW components to WZY before discarding X.
/// </summary>
internal readonly struct WZYXShuffle4Slice3 : IShuffle4Slice3
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source) => BinaryPrimitives.ReverseEndianness(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128_.ShuffleNative(source, Vector128.Create((byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12));
}

/// <summary>
/// Reorders XYZW components to ZYX before discarding W.
/// </summary>
internal readonly struct ZYXWShuffle4Slice3 : IShuffle4Slice3
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)
    {
        // Preserve W and Y while exchanging X and Z; W is subsequently discarded.
        uint wy = source & 0xFF00FF00;
        uint xz = source & 0x00FF00FF;
        return wy | BitOperations.RotateLeft(xz, 16);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128_.ShuffleNative(source, Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15));
}

[StructLayout(LayoutKind.Explicit, Size = 3)]
internal readonly struct Byte3
{
}
