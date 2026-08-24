// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

/// <summary>
/// Defines a stateless operation that reorders a three-component pixel after adding opaque alpha.
/// </summary>
internal interface IPad3Shuffle4 : IComponentShuffle
{
}

/// <summary>
/// Preserves XYZ order and appends opaque W.
/// </summary>
internal readonly struct XYZWPad3Shuffle4 : IPad3Shuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source) => source;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source) => source;
}

/// <summary>
/// Reorders padded XYZW components to WXYZ.
/// </summary>
internal readonly struct WXYZPad3Shuffle4 : IPad3Shuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // The scalar pipeline has already appended opaque W, so the four-component
        // WXYZ operator performs the complete remaining permutation.
        => WXYZShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)

        // Each four-byte group is an XYZW pixel with opaque W. Selecting [3, 0, 1, 2]
        // produces WXYZ, and offsets 4, 8, and 12 repeat that rotation for the next pixels.
        => Vector128.ShuffleNative(source, Vector128.Create((byte)3, 0, 1, 2, 7, 4, 5, 6, 11, 8, 9, 10, 15, 12, 13, 14));
}

/// <summary>
/// Reorders padded XYZW components to WZYX.
/// </summary>
internal readonly struct WZYXPad3Shuffle4 : IPad3Shuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // The scalar pipeline has already appended opaque W, so the four-component
        // WZYX operator performs the complete remaining permutation.
        => WZYXShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)

        // Each four-byte group is an XYZW pixel with opaque W. Selecting [3, 2, 1, 0]
        // produces WZYX, and offsets 4, 8, and 12 repeat that reversal for the next pixels.
        => Vector128.ShuffleNative(source, Vector128.Create((byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12));
}

/// <summary>
/// Reorders padded XYZW components to ZYXW.
/// </summary>
internal readonly struct ZYXWPad3Shuffle4 : IPad3Shuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // The scalar pipeline has already appended opaque W, so the four-component
        // ZYXW operator performs the complete remaining permutation.
        => ZYXWShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)

        // Each four-byte group is an XYZW pixel with opaque W. Selecting [2, 1, 0, 3]
        // exchanges X and Z to produce ZYXW, with offsets 4, 8, and 12 covering the next pixels.
        => Vector128.ShuffleNative(source, Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15));
}
