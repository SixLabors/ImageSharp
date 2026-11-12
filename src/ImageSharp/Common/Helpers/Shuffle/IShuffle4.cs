// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

/// <summary>
/// Defines a stateless operation over one packed four-component pixel.
/// </summary>
internal interface IShuffle4 : IComponentShuffle
{
}

/// <summary>
/// Reorders XYZW components to WXYZ.
/// </summary>
internal readonly struct WXYZShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // source          = [W Z Y X]
        // ROTL(8, source) = [Z Y X W]
        => BitOperations.RotateLeft(source, 8);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128.ShuffleNative(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)

        // AVX2 byte shuffles select within 128-bit lanes, so both halves use the same pixel-local indices.
        => Vector256_.ShufflePerLane(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)

        // AVX-512 byte shuffles select within 128-bit lanes, so all four lanes use the same pixel-local indices.
        => Vector512_.ShufflePerLane(source, CreateLaneMask());

    /// <summary>
    /// Creates the indices that rotate each XYZW pixel to WXYZ within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateLaneMask()

        // Each four-byte group is one XYZW pixel. Selecting [3, 0, 1, 2] produces
        // WXYZ, and offsets 4, 8, and 12 repeat that permutation for the next pixels.
        => Vector128.Create((byte)3, 0, 1, 2, 7, 4, 5, 6, 11, 8, 9, 10, 15, 12, 13, 14);
}

/// <summary>
/// Reorders XYZW components to WZYX.
/// </summary>
internal readonly struct WZYXShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // source          = [W Z Y X]
        // REVERSE(source) = [X Y Z W]
        => BinaryPrimitives.ReverseEndianness(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128.ShuffleNative(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)

        // AVX2 byte shuffles select within 128-bit lanes, so both halves use the same pixel-local indices.
        => Vector256_.ShufflePerLane(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)

        // AVX-512 byte shuffles select within 128-bit lanes, so all four lanes use the same pixel-local indices.
        => Vector512_.ShufflePerLane(source, CreateLaneMask());

    /// <summary>
    /// Creates the indices that reverse each XYZW pixel to WZYX within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateLaneMask()

        // Each four-byte group is one XYZW pixel. Selecting [3, 2, 1, 0] produces
        // WZYX, and offsets 4, 8, and 12 repeat that reversal for the next pixels.
        => Vector128.Create((byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12);
}

/// <summary>
/// Reorders XYZW components to YZWX.
/// </summary>
internal readonly struct YZWXShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // source          = [W Z Y X]
        // ROTR(8, source) = [X W Z Y]
        => BitOperations.RotateRight(source, 8);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128.ShuffleNative(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)

        // AVX2 byte shuffles select within 128-bit lanes, so both halves use the same pixel-local indices.
        => Vector256_.ShufflePerLane(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)

        // AVX-512 byte shuffles select within 128-bit lanes, so all four lanes use the same pixel-local indices.
        => Vector512_.ShufflePerLane(source, CreateLaneMask());

    /// <summary>
    /// Creates the indices that rotate each XYZW pixel to YZWX within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateLaneMask()

        // Each four-byte group is one XYZW pixel. Selecting [1, 2, 3, 0] produces
        // YZWX, and offsets 4, 8, and 12 repeat that rotation for the next pixels.
        => Vector128.Create((byte)1, 2, 3, 0, 5, 6, 7, 4, 9, 10, 11, 8, 13, 14, 15, 12);
}

/// <summary>
/// Reorders XYZW components to ZYXW.
/// </summary>
internal readonly struct ZYXWShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // source                     = [W Z Y X]
        // source & 0xFF00FF00        = [W 0 Y 0]
        // ROTL(source & 0x00FF00FF)  = [0 X 0 Z]
        // combined                   = [W X Y Z]
        => (source & 0xFF00FF00) | BitOperations.RotateLeft(source & 0x00FF00FF, 16);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128.ShuffleNative(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)

        // AVX2 byte shuffles select within 128-bit lanes, so both halves use the same pixel-local indices.
        => Vector256_.ShufflePerLane(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)

        // AVX-512 byte shuffles select within 128-bit lanes, so all four lanes use the same pixel-local indices.
        => Vector512_.ShufflePerLane(source, CreateLaneMask());

    /// <summary>
    /// Creates the indices that exchange X and Z in each XYZW pixel within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateLaneMask()

        // Each four-byte group is one XYZW pixel. Selecting [2, 1, 0, 3] exchanges
        // X and Z to produce ZYXW, with offsets 4, 8, and 12 covering the next pixels.
        => Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15);
}

/// <summary>
/// Reorders XYZW components to XWZY.
/// </summary>
internal readonly struct XWZYShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)

        // source                     = [W Z Y X]
        // source & 0x00FF00FF        = [0 Z 0 X]
        // ROTL(source & 0xFF00FF00)  = [Y 0 W 0]
        // combined                   = [Y Z W X]
        => (source & 0x00FF00FF) | BitOperations.RotateLeft(source & 0xFF00FF00, 16);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128.ShuffleNative(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)

        // AVX2 byte shuffles select within 128-bit lanes, so both halves use the same pixel-local indices.
        => Vector256_.ShufflePerLane(source, CreateLaneMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)

        // AVX-512 byte shuffles select within 128-bit lanes, so all four lanes use the same pixel-local indices.
        => Vector512_.ShufflePerLane(source, CreateLaneMask());

    /// <summary>
    /// Creates the indices that exchange Y and W in each XYZW pixel within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateLaneMask()

        // Each four-byte group is one XYZW pixel. Selecting [0, 3, 2, 1] exchanges
        // Y and W to produce XWZY, with offsets 4, 8, and 12 covering the next pixels.
        => Vector128.Create((byte)0, 3, 2, 1, 4, 7, 6, 5, 8, 11, 10, 9, 12, 15, 14, 13);
}
