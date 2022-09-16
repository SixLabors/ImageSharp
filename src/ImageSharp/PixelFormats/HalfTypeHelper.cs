// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Helper methods for packing and unpacking floating point values
/// </summary>
internal static class HalfTypeHelper
{
    /// <summary>
    /// Packs a <see cref="float"/> into an <see cref="ushort"/>
    /// </summary>
    /// <param name="value">The float to pack</param>
    /// <returns>The <see cref="ushort"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ushort Pack(float value)
    {
        Half h = (Half)value;
        return Unsafe.As<Half, ushort>(ref h);
    }

    /// <summary>
    /// Unpacks a <see cref="ushort"/> into a <see cref="float"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The <see cref="float"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Unpack(ushort value) => (float)BitConverter.UInt16BitsToHalf(value);
}
