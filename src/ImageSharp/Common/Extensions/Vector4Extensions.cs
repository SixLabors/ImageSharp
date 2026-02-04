// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#if !NET9_0_OR_GREATER
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp;

internal static class Vector4Extensions
{
    /// <summary>
    /// Reinterprets a <see cref="Vector4" /> as a new <see cref="Vector3" />.
    /// </summary>
    /// <param name="value">The vector to reinterpret.</param>
    /// <returns><paramref name="value" /> reinterpreted as a new <see cref="Vector3" />.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 AsVector3(this Vector4 value) => value.AsVector128().AsVector3();
}
#endif
