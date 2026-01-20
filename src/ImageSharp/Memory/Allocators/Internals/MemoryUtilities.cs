// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Memory.Allocators.Internals;

/// <summary>
/// Utilities for memory alignment and related operations.
/// </summary>
internal static class MemoryUtilities
{
    /// <summary>
    /// Returns the recommended memory alignment, in bytes, for optimal SIMD operations on the current hardware
    /// platform.
    /// </summary>
    /// <remarks>
    /// Use this value when allocating memory buffers intended for SIMD processing to help achieve optimal
    /// performance. The returned alignment corresponds to the preferred alignment characteristics of the most
    /// advanced SIMD instruction set supported by the processor, such as AVX-512, AVX2, SSE2, or ARM64 NEON.
    /// </remarks>
    /// <returns>
    /// A value, in bytes, representing the alignment boundary that should be used for efficient vectorized operations.
    /// The value is always a power of two and reflects the largest supported SIMD instruction set available at runtime.
    /// </returns>
    public static nuint GetAlignment()
    {
        if (Vector512.IsHardwareAccelerated)
        {
            return (nuint)Vector512<byte>.Count; // 64
        }

        if (Vector256.IsHardwareAccelerated)
        {
            return (nuint)Vector256<byte>.Count; // 32
        }

        if (Vector128.IsHardwareAccelerated)
        {
            return (nuint)Vector128<byte>.Count; // 16
        }

        // Safe fallback. Alignment must be power-of-two.
        return 16;
    }

    /// <summary>
    /// Returns a span of <paramref name="length"/> elements over <paramref name="buffer"/>, trimmed so the first element
    /// begins at an address aligned to <see cref="GetAlignment"/>.
    /// </summary>
    /// <param name="buffer">The backing byte array.</param>
    /// <param name="length">The number of elements in the returned span.</param>
    /// <remarks>
    /// Callers must rent/provide <paramref name="buffer"/> with enough slack (alignment - 1 bytes) so the trimmed slice
    /// always fits.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetAlignedSpan<T>(byte[] buffer, int length)
        where T : struct
    {
        int lengthInBytes = checked(length * Unsafe.SizeOf<T>());
        int offsetBytes = GetAlignedOffsetBytes<T>(buffer);

        return MemoryMarshal.Cast<byte, T>(buffer.AsSpan(offsetBytes, lengthInBytes));
    }

    /// <summary>
    /// Computes the byte offset required to align a sliced view of <paramref name="buffer"/> to the
    /// alignment returned by <see cref="GetAlignment"/>.
    /// </summary>
    /// <remarks>
    /// This method is intended for use with pooled managed arrays where the exposed span must begin at an
    /// aligned address. The returned offset is the number of leading bytes that should be skipped so that
    /// the first element of a <see cref="Span{T}"/> begins at an aligned boundary.
    ///
    /// This method does not pin the array. If the array may move during use, callers that require a stable
    /// aligned address must compute the offset from the pinned base address instead.
    /// </remarks>
    /// <typeparam name="T">
    /// The element type that the buffer will be reinterpreted as. The computed offset is guaranteed to be
    /// compatible with <see cref="MemoryMarshal.Cast{TFrom, TTo}(Span{TFrom})"/>.
    /// </typeparam>
    /// <param name="buffer">The backing byte array.</param>
    /// <returns>
    /// The number of bytes to skip from the start of <paramref name="buffer"/> to reach the next aligned
    /// element boundary.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe int GetAlignedOffsetBytes<T>(byte[] buffer)
        where T : struct
    {
        nuint alignment = GetAlignment();
        int elementSize = Unsafe.SizeOf<T>();

        // Compute a mask for rounding addresses up to the next alignment boundary.
        // Example: alignment = 64 -> mask = 0b0011_1111
        nuint mask = alignment - 1;

        // Obtain the address of the first byte in the array.
        ref byte r0 = ref MemoryMarshal.GetArrayDataReference(buffer);
        nuint baseAddr = (nuint)Unsafe.AsPointer(ref r0);

        // Round the base address up to the next aligned address.
        // This is a standard power-of-two alignment operation:
        //   aligned = (addr + (alignment - 1)) & ~(alignment - 1)
        nuint alignedAddr = (baseAddr + mask) & ~mask;

        // Compute the byte offset needed to reach the aligned address.
        nuint offset = alignedAddr - baseAddr;

        // Ensure the offset is a multiple of sizeof(T), which is required for
        // MemoryMarshal.Cast<byte, T> to be valid.
        nuint rem = offset % (nuint)elementSize;
        if (rem != 0)
        {
            offset += (nuint)elementSize - rem;
        }

        return (int)offset;
    }
}
