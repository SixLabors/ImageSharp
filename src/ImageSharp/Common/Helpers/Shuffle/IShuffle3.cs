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

        // Each four-byte group is a temporary XYZW pixel created by the shuffle pipeline.
        // Selecting [2, 1, 0, 3] produces ZYXW, and offsets 4, 8, and 12 repeat that
        // permutation for the next pixels. The pipeline subsequently discards every W byte.
        => Vector128.ShuffleNative(source, Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15));
}
