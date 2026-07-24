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
    /// <summary>
    /// Reorders the packed pixels in a 256-bit vector.
    /// </summary>
    /// <param name="source">The source pixels.</param>
    /// <returns>The reordered pixels.</returns>
    public static abstract Vector256<byte> Invoke(Vector256<byte> source);

    /// <summary>
    /// Reorders the packed pixels in a 512-bit vector.
    /// </summary>
    /// <param name="source">The source pixels.</param>
    /// <returns>The reordered pixels.</returns>
    public static abstract Vector512<byte> Invoke(Vector512<byte> source);
}

/// <summary>
/// Reorders XYZW components to WXYZ.
/// </summary>
internal readonly struct WXYZShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)
    {
        // source          = [W Z Y X]
        // ROTL(8, source) = [Z Y X W]
        return BitOperations.RotateLeft(source, 8);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128_.ShuffleNative(source, CreateMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)
    {
        // AVX2 byte shuffles select within 128-bit lanes, so both halves use the same pixel-local indices.
        Vector128<byte> mask = CreateMask();
        return Vector256_.ShufflePerLane(source, Vector256.Create(mask, mask));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)
        => Vector512_.ShuffleNative(source, CreateMask512());

    /// <summary>
    /// Creates the indices that rotate each XYZW pixel to WXYZ within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateMask()
        => Vector128.Create((byte)3, 0, 1, 2, 7, 4, 5, 6, 11, 8, 9, 10, 15, 12, 13, 14);

    /// <summary>
    /// Creates absolute indices for all four 128-bit lanes in a 512-bit vector.
    /// </summary>
    /// <returns>The absolute byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> CreateMask512()
    {
        // Native vpshufb ignores the lane offsets, while Vector512.Shuffle treats the indices as
        // absolute. Encoding both meanings keeps the AVX-512 and managed fallback results identical.
        return Vector512.Create(
            0x0605040702010003UL,
            0x0E0D0C0F0A09080BUL,
            0x1615141712111013UL,
            0x1E1D1C1F1A19181BUL,
            0x2625242722212023UL,
            0x2E2D2C2F2A29282BUL,
            0x3635343732313033UL,
            0x3E3D3C3F3A39383BUL).AsByte();
    }
}

/// <summary>
/// Reorders XYZW components to WZYX.
/// </summary>
internal readonly struct WZYXShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)
    {
        // Reversing the integer's endianness also reverses the four byte components.
        return BinaryPrimitives.ReverseEndianness(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128_.ShuffleNative(source, CreateMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)
    {
        Vector128<byte> mask = CreateMask();
        return Vector256_.ShufflePerLane(source, Vector256.Create(mask, mask));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)
        => Vector512_.ShuffleNative(source, CreateMask512());

    /// <summary>
    /// Creates the indices that reverse each XYZW pixel to WZYX within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateMask()
        => Vector128.Create((byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12);

    /// <summary>
    /// Creates absolute indices for all four 128-bit lanes in a 512-bit vector.
    /// </summary>
    /// <returns>The absolute byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> CreateMask512()
        => Vector512.Create(
            0x0405060700010203UL,
            0x0C0D0E0F08090A0BUL,
            0x1415161710111213UL,
            0x1C1D1E1F18191A1BUL,
            0x2425262720212223UL,
            0x2C2D2E2F28292A2BUL,
            0x3435363730313233UL,
            0x3C3D3E3F38393A3BUL).AsByte();
}

/// <summary>
/// Reorders XYZW components to YZWX.
/// </summary>
internal readonly struct YZWXShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)
    {
        // source          = [W Z Y X]
        // ROTR(8, source) = [X W Z Y]
        return BitOperations.RotateRight(source, 8);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128_.ShuffleNative(source, CreateMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)
    {
        Vector128<byte> mask = CreateMask();
        return Vector256_.ShufflePerLane(source, Vector256.Create(mask, mask));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)
        => Vector512_.ShuffleNative(source, CreateMask512());

    /// <summary>
    /// Creates the indices that rotate each XYZW pixel to YZWX within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateMask()
        => Vector128.Create((byte)1, 2, 3, 0, 5, 6, 7, 4, 9, 10, 11, 8, 13, 14, 15, 12);

    /// <summary>
    /// Creates absolute indices for all four 128-bit lanes in a 512-bit vector.
    /// </summary>
    /// <returns>The absolute byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> CreateMask512()
        => Vector512.Create(
            0x0407060500030201UL,
            0x0C0F0E0D080B0A09UL,
            0x1417161510131211UL,
            0x1C1F1E1D181B1A19UL,
            0x2427262520232221UL,
            0x2C2F2E2D282B2A29UL,
            0x3437363530333231UL,
            0x3C3F3E3D383B3A39UL).AsByte();
}

/// <summary>
/// Reorders XYZW components to ZYXW.
/// </summary>
internal readonly struct ZYXWShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)
    {
        // Preserve W and Y while rotating the masked X/Z bytes into each other's positions.
        uint wy = source & 0xFF00FF00;
        uint xz = source & 0x00FF00FF;
        return wy | BitOperations.RotateLeft(xz, 16);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128_.ShuffleNative(source, CreateMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)
    {
        Vector128<byte> mask = CreateMask();
        return Vector256_.ShufflePerLane(source, Vector256.Create(mask, mask));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)
        => Vector512_.ShuffleNative(source, CreateMask512());

    /// <summary>
    /// Creates the indices that exchange X and Z in each XYZW pixel within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateMask()
        => Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15);

    /// <summary>
    /// Creates absolute indices for all four 128-bit lanes in a 512-bit vector.
    /// </summary>
    /// <returns>The absolute byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> CreateMask512()
        => Vector512.Create(
            0x0704050603000102UL,
            0x0F0C0D0E0B08090AUL,
            0x1714151613101112UL,
            0x1F1C1D1E1B18191AUL,
            0x2724252623202122UL,
            0x2F2C2D2E2B28292AUL,
            0x3734353633303132UL,
            0x3F3C3D3E3B38393AUL).AsByte();
}

/// <summary>
/// Reorders XYZW components to XWZY.
/// </summary>
internal readonly struct XWZYShuffle4 : IShuffle4
{
    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public static uint Invoke(uint source)
    {
        // Preserve X and Z while rotating the masked Y/W bytes into each other's positions.
        uint xz = source & 0x00FF00FF;
        uint yw = source & 0xFF00FF00;
        return xz | BitOperations.RotateLeft(yw, 16);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Invoke(Vector128<byte> source)
        => Vector128_.ShuffleNative(source, CreateMask());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Invoke(Vector256<byte> source)
    {
        Vector128<byte> mask = CreateMask();
        return Vector256_.ShufflePerLane(source, Vector256.Create(mask, mask));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Invoke(Vector512<byte> source)
        => Vector512_.ShuffleNative(source, CreateMask512());

    /// <summary>
    /// Creates the indices that exchange Y and W in each XYZW pixel within one 128-bit lane.
    /// </summary>
    /// <returns>The pixel-local byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateMask()
        => Vector128.Create((byte)0, 3, 2, 1, 4, 7, 6, 5, 8, 11, 10, 9, 12, 15, 14, 13);

    /// <summary>
    /// Creates absolute indices for all four 128-bit lanes in a 512-bit vector.
    /// </summary>
    /// <returns>The absolute byte shuffle indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> CreateMask512()
        => Vector512.Create(
            0x0506070401020300UL,
            0x0D0E0F0C090A0B08UL,
            0x1516171411121310UL,
            0x1D1E1F1C191A1B18UL,
            0x2526272421222320UL,
            0x2D2E2F2C292A2B28UL,
            0x3536373431323330UL,
            0x3D3E3F3C393A3B38UL).AsByte();
}
