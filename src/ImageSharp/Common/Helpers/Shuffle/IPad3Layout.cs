// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp;

/// <summary>
/// Defines the byte shuffles that pad pixels of three samples to four samples, and slice them back to three samples.
/// </summary>
/// <remarks>
/// A 128-bit lane holds 16 bytes of padded pixels. The same pixels with three samples use 12 bytes. Thus three registers
/// of 3-sample pixels fill four registers of padded pixels. <c>SimdUtils.Pad3</c> and <c>SimdUtils.Slice3</c> move
/// the 12-byte groups between registers. The masks of the layout move the bytes inside each 128-bit lane.
/// </remarks>
internal interface IPad3Layout
{
    /// <summary>
    /// Gets the byte shuffle that pads the 3-sample pixels in bytes 0 to 11 of a lane to the 16 bytes of the lane.
    /// Index 0x80 sets the fourth sample of each pixel to 0.
    /// </summary>
    public static abstract Vector128<byte> PadMask { get; }

    /// <summary>
    /// Gets the byte shuffle that slices the first three samples of each padded pixel in a lane to bytes 0 to 11 of the
    /// lane. Index 0x80 sets bytes 12 to 15 to 0.
    /// </summary>
    public static abstract Vector128<byte> SliceMask { get; }

    /// <summary>
    /// Gets <see cref="SliceMask"/> moved four bytes up. It slices the first three samples of each padded pixel to
    /// bytes 4 to 15 of the lane. Index 0x80 sets bytes 0 to 3 to 0.
    /// </summary>
    public static abstract Vector128<byte> SliceEndMask { get; }
}

/// <summary>
/// Pads pixels of three 8-bit samples to 32-bit lanes. Byte 3 of each lane is 0.
/// </summary>
internal readonly struct Pad3x8Layout : IPad3Layout
{
    /// <inheritdoc />
    public static Vector128<byte> PadMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 1, 2, 0x80, 3, 4, 5, 0x80, 6, 7, 8, 0x80, 9, 10, 11, 0x80);
    }

    /// <inheritdoc />
    public static Vector128<byte> SliceMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 0x80, 0x80, 0x80, 0x80);
    }

    /// <inheritdoc />
    public static Vector128<byte> SliceEndMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0x80, 0x80, 0x80, 0x80, 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14);
    }
}

/// <summary>
/// Pads pixels of three 16-bit samples to 64-bit lanes. The fourth 16-bit sample of each lane is 0.
/// </summary>
internal readonly struct Pad3x16Layout : IPad3Layout
{
    /// <inheritdoc />
    public static Vector128<byte> PadMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 1, 2, 3, 4, 5, 0x80, 0x80, 6, 7, 8, 9, 10, 11, 0x80, 0x80);
    }

    /// <inheritdoc />
    public static Vector128<byte> SliceMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 13, 0x80, 0x80, 0x80, 0x80);
    }

    /// <inheritdoc />
    public static Vector128<byte> SliceEndMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0x80, 0x80, 0x80, 0x80, 0, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 13);
    }
}
