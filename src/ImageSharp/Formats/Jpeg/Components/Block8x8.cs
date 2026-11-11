// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <summary>
/// 8x8 matrix of <see cref="short"/> coefficients.
/// </summary>
// ReSharper disable once InconsistentNaming
[StructLayout(LayoutKind.Explicit, Size = 2 * Size)]
internal partial struct Block8x8 : IEquatable<Block8x8>
{
    /// <summary>
    /// A number of scalar coefficients in a <see cref="Block8x8F"/>
    /// </summary>
    public const int Size = 64;

    /// <summary>
    /// The 64 values in row order.
    /// </summary>
    /// <remarks>
    /// The indexer and the SIMD code use a reference to the first value. This field gives the struct its data.
    /// <see cref="Equals(Block8x8)"/> and <see cref="GetHashCode"/> use all 64 values.
    /// </remarks>
    [FieldOffset(0)]
    private InlineArray64<short> values;

    /// <summary>
    /// Gets or sets a <see cref="short"/> value at the given index
    /// </summary>
    /// <param name="idx">The index</param>
    /// <returns>The value</returns>
    public short this[int idx]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            DebugGuard.MustBeBetweenOrEqualTo(idx, 0, Size - 1, nameof(idx));

            ref short selfRef = ref Unsafe.As<Block8x8, short>(ref this);
            return Unsafe.Add(ref selfRef, (uint)idx);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            DebugGuard.MustBeBetweenOrEqualTo(idx, 0, Size - 1, nameof(idx));

            ref short selfRef = ref Unsafe.As<Block8x8, short>(ref this);
            Unsafe.Add(ref selfRef, (uint)idx) = value;
        }
    }

    /// <summary>
    /// Gets or sets a value in a row+column of the 8x8 block
    /// </summary>
    /// <param name="x">The x position index in the row</param>
    /// <param name="y">The column index</param>
    /// <returns>The value</returns>
    public short this[int x, int y]
    {
        get => this[(y * 8) + x];
        set => this[(y * 8) + x] = value;
    }

    public static Block8x8 Load(Span<short> data)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(data.Length, Size, "data is too small");

        ref byte src = ref Unsafe.As<short, byte>(ref MemoryMarshal.GetReference(data));
        return Unsafe.ReadUnaligned<Block8x8>(ref src);
    }

    /// <summary>
    /// Convert to <see cref="Block8x8F"/>
    /// </summary>
    public Block8x8F AsFloatBlock()
    {
        Block8x8F result = default;
        result.LoadFrom(ref this);
        return result;
    }

    /// <summary>
    /// Copy all elements to an array of <see cref="short"/>.
    /// </summary>
    public readonly short[] ToArray()
    {
        short[] result = new short[Size];
        this.CopyTo(result);
        return result;
    }

    /// <summary>
    /// Copy elements into 'destination' Span of <see cref="short"/> values
    /// </summary>
    public readonly void CopyTo(Span<short> destination)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(destination.Length, Size, "destination is too small");

        ref byte destRef = ref Unsafe.As<short, byte>(ref MemoryMarshal.GetReference(destination));
        Unsafe.WriteUnaligned(ref destRef, this);
    }

    /// <summary>
    /// Copy elements into 'destination' Span of <see cref="int"/> values
    /// </summary>
    public void CopyTo(Span<int> destination)
    {
        for (int i = 0; i < Size; i++)
        {
            destination[i] = this[i];
        }
    }

    public static Block8x8 Load(ReadOnlySpan<byte> data)
    {
        Unsafe.SkipInit(out Block8x8 result);
        result.LoadFrom(data);
        return result;
    }

    public void LoadFrom(ReadOnlySpan<byte> source)
    {
        for (int i = 0; i < Size; i++)
        {
            this[i] = source[i];
        }
    }

    /// <summary>
    /// Cast and copy <see cref="Size"/> <see cref="int"/>-s from the beginning of 'source' span.
    /// </summary>
    public void LoadFrom(ReadOnlySpan<int> source)
    {
        for (int i = 0; i < Size; i++)
        {
            this[i] = (short)source[i];
        }
    }

    /// <summary>
    /// Gets the 64 values in row order.
    /// </summary>
    /// <returns>The values.</returns>
    private readonly ReadOnlySpan<short> AsReadOnlySpan()
        => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<Block8x8, short>(ref Unsafe.AsRef(in this)), Size);

    /// <inheritdoc />
    public readonly bool Equals(Block8x8 other) => this.AsReadOnlySpan().SequenceEqual(other.AsReadOnlySpan());

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Block8x8 other && this.Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode()
    {
        HashCode hash = default;
        hash.AddBytes(MemoryMarshal.AsBytes(this.AsReadOnlySpan()));
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append('[');
        for (int i = 0; i < Size; i++)
        {
            sb.Append(this[i]);
            if (i < Size - 1)
            {
                sb.Append(',');
            }
        }

        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Tells if all elements after the first element are zero.
    /// In a coefficient block, this means that only the DC coefficient can be not zero.
    /// </summary>
    /// <returns><see langword="true"/> when values 1 to 63 are zero.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly bool IsDcOnly()
    {
        // Each width combines all registers of the block with OR and does one test of the result.
        // The first register holds value 0 in lane 0. The mask clears that lane before the OR.
        // Lane 0 of the result also holds other values: 32 for Vector512, 16, 32 and 48 for Vector256,
        // and each eighth value for Vector128.
        // A scan for the last non-zero value has branches that change with the data. This method has no such branch.
        ref short first = ref Unsafe.As<Block8x8, short>(ref Unsafe.AsRef(in this));
        if (Vector512.IsHardwareAccelerated)
        {
            // Two registers of 32 values. The mask is a constant: zero in lane 0, all bits set in the other lanes.
            Vector256<short> lowMask = Vector256.Create((short)0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
            Vector512<short> acMask = Vector512.Create(lowMask, Vector256<short>.AllBitsSet);
            Vector512<short> ac = Vector512.LoadUnsafe(ref first, 0) & acMask;
            return (ac | Vector512.LoadUnsafe(ref first, 32)) == Vector512<short>.Zero;
        }

        if (Vector256.IsHardwareAccelerated)
        {
            // Four registers of 16 values.
            Vector256<short> ac = Vector256.LoadUnsafe(ref first, 0) & Vector256.Create((short)0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
            Vector256<short> any = (ac | Vector256.LoadUnsafe(ref first, 16)) | (Vector256.LoadUnsafe(ref first, 32) | Vector256.LoadUnsafe(ref first, 48));
            return any == Vector256<short>.Zero;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // Eight registers of 8 values.
            Vector128<short> ac = Vector128.LoadUnsafe(ref first, 0) & Vector128.Create((short)0, -1, -1, -1, -1, -1, -1, -1);
            Vector128<short> low = (ac | Vector128.LoadUnsafe(ref first, 8)) | (Vector128.LoadUnsafe(ref first, 16) | Vector128.LoadUnsafe(ref first, 24));
            Vector128<short> high = (Vector128.LoadUnsafe(ref first, 32) | Vector128.LoadUnsafe(ref first, 40))
                | (Vector128.LoadUnsafe(ref first, 48) | Vector128.LoadUnsafe(ref first, 56));

            Vector128<short> any = low | high;
            return any == Vector128<short>.Zero;
        }

        int accumulator = 0;
        for (nint i = 1; i < Size; i++)
        {
            accumulator |= (ushort)Unsafe.Add(ref first, i);
        }

        return accumulator == 0;
    }

    /// <summary>
    /// Gets a mask of the values that are not zero.
    /// </summary>
    /// <returns>A mask with bit <c>i</c> set when value <c>i</c> is not zero.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly ulong GetNonZeroMask()
    {
        // Each width compares the elements with zero. A lane that is zero becomes all bits set.
        // ExtractMostSignificantBits puts the top bit of lane k in bit k. Each register covers the next
        // group of elements, so its bits shift up by the element index of its first lane.
        // The complement of the zero mask is the non-zero mask. The SIMD paths have no branch that changes with the data.
        ref short first = ref Unsafe.As<Block8x8, short>(ref Unsafe.AsRef(in this));
        if (Vector512.IsHardwareAccelerated)
        {
            // Two registers of 32 values: elements 0 to 31 and 32 to 63.
            ulong low = Vector512.Equals(Vector512.LoadUnsafe(ref first, 0), Vector512<short>.Zero).ExtractMostSignificantBits();
            ulong high = Vector512.Equals(Vector512.LoadUnsafe(ref first, 32), Vector512<short>.Zero).ExtractMostSignificantBits();
            return ~(low | (high << 32));
        }

        if (Vector256.IsHardwareAccelerated)
        {
            // Four registers of 16 values.
            ulong m0 = Vector256.Equals(Vector256.LoadUnsafe(ref first, 0), Vector256<short>.Zero).ExtractMostSignificantBits();
            ulong m1 = Vector256.Equals(Vector256.LoadUnsafe(ref first, 16), Vector256<short>.Zero).ExtractMostSignificantBits();
            ulong m2 = Vector256.Equals(Vector256.LoadUnsafe(ref first, 32), Vector256<short>.Zero).ExtractMostSignificantBits();
            ulong m3 = Vector256.Equals(Vector256.LoadUnsafe(ref first, 48), Vector256<short>.Zero).ExtractMostSignificantBits();
            return ~(m0 | (m1 << 16) | (m2 << 32) | (m3 << 48));
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // Eight registers of 8 values.
            ulong zero = 0;
            for (int i = 0; i < 8; i++)
            {
                ulong m = Vector128.Equals(Vector128.LoadUnsafe(ref first, (nuint)(i * 8)), Vector128<short>.Zero).ExtractMostSignificantBits();
                zero |= m << (i * 8);
            }

            return ~zero;
        }

        ulong mask = 0;
        for (int i = 0; i < Size; i++)
        {
            if (Unsafe.Add(ref first, i) != 0)
            {
                mask |= 1UL << i;
            }
        }

        return mask;
    }

    /// <summary>
    /// Calculate the total sum of absolute differences of elements in 'a' and 'b'.
    /// </summary>
    public static long TotalDifference(ref Block8x8 a, ref Block8x8 b)
    {
        long result = 0;
        for (int i = 0; i < Size; i++)
        {
            int d = a[i] - b[i];
            result += Math.Abs(d);
        }

        return result;
    }
}
