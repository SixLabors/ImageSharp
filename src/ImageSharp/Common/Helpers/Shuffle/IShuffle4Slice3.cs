// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source) => source;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source) => source;
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

        // The four-component operator owns the permutation.
        => YZWXShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)

        // The four-component operator owns the permutation.
        => YZWXShuffle4.Invoke(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)

        // The four-component operator owns the permutation.
        => YZWXShuffle4.Invoke(source);
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
