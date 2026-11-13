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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source) => source;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source) => source;
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

        // The four-component operator owns the permutation.
        => WXYZShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)

        // The four-component operator owns the permutation.
        => WXYZShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)

        // The four-component operator owns the permutation.
        => WXYZShuffle4.Invoke(source);
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

        // The four-component operator owns the permutation.
        => WZYXShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)

        // The four-component operator owns the permutation.
        => WZYXShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)

        // The four-component operator owns the permutation.
        => WZYXShuffle4.Invoke(source);
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

        // The four-component operator owns the permutation.
        => ZYXWShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)

        // The four-component operator owns the permutation.
        => ZYXWShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)

        // The four-component operator owns the permutation.
        => ZYXWShuffle4.Invoke(source);
}
