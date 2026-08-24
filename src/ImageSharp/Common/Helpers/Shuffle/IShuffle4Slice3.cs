// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    public static uint Invoke(uint source)

        // Reuse the four-component rotation; the caller stores only the low YZW
        // bytes and therefore discards the rotated X byte.
        => YZWXShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)

        // Each four-byte group is an XYZW pixel. Selecting [1, 2, 3, 0] produces
        // YZWX, and offsets 4, 8, and 12 repeat that rotation for the next pixels.
        // The surrounding pipeline subsequently removes every fourth byte.
        => Vector128.ShuffleNative(source, Vector128.Create((byte)1, 2, 3, 0, 5, 6, 7, 4, 9, 10, 11, 8, 13, 14, 15, 12));
}

/// <summary>
/// Reorders XYZW components to WZY before discarding X.
/// </summary>
internal readonly struct WZYXShuffle4Slice3 : IShuffle4Slice3
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // Reuse the four-component reversal; the caller stores only the low WZY
        // bytes and therefore discards the reversed X byte.
        => WZYXShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)

        // Each four-byte group is an XYZW pixel. Selecting [3, 2, 1, 0] produces
        // WZYX, and offsets 4, 8, and 12 repeat that reversal for the next pixels.
        // The surrounding pipeline subsequently removes every fourth byte.
        => Vector128.ShuffleNative(source, Vector128.Create((byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12));
}

/// <summary>
/// Reorders XYZW components to ZYX before discarding W.
/// </summary>
internal readonly struct ZYXWShuffle4Slice3 : IShuffle4Slice3
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // Reuse the four-component exchange; the caller stores only the low ZYX
        // bytes and therefore discards the preserved W byte.
        => ZYXWShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)

        // Each four-byte group is an XYZW pixel. Selecting [2, 1, 0, 3] produces
        // ZYXW, and offsets 4, 8, and 12 repeat that exchange for the next pixels.
        // The surrounding pipeline subsequently removes every fourth byte.
        => Vector128.ShuffleNative(source, Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15));
}

/// <summary>
/// Represents one tightly packed three-byte value for scalar four-to-three component writes.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 3)]
internal readonly struct Byte3
{
}
