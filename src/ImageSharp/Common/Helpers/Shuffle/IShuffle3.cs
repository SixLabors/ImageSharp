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
    {
        // Y is already centered; shift X and Z directly into each other's byte positions.
        uint y = source & 0x0000FF00;
        uint x = (source & 0x000000FF) << 16;
        uint z = (source & 0x00FF0000) >> 16;
        return x | y | z;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128_.ShuffleNative(source, Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15));
}
