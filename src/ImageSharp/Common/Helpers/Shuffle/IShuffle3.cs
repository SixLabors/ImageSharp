// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

/// <summary>
/// Identifies a stateless three-component shuffle operator.
/// </summary>
internal interface IShuffle3 : IComponentShuffle
{
}

/// <summary>
/// Reorders XYZ components to ZYX.
/// </summary>
internal readonly struct ZYXShuffle3 : IShuffle3
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // The scalar tail is staged as XYZW with an unused W byte. Reusing the four-component
        // ZYXW operator produces ZYX in the low three bytes consumed by the caller.
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
