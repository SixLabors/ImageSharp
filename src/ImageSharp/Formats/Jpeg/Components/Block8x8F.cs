// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using SixLabors.ImageSharp.Common.Helpers;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <summary>
/// 8x8 matrix of <see cref="float"/> coefficients.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal partial struct Block8x8F : IEquatable<Block8x8F>
{
    /// <summary>
    /// A number of scalar coefficients in a <see cref="Block8x8F"/>
    /// </summary>
    public const int Size = 64;

    /// <summary>
    /// The number of rows in a <see cref="Block8x8F"/>. Each row has 8 coefficients.
    /// </summary>
    public const int RowCount = 8;

    [FieldOffset(0)]
    public Vector4 V0L;
    [FieldOffset(16)]
    public Vector4 V0R;

    [FieldOffset(32)]
    public Vector4 V1L;
    [FieldOffset(48)]
    public Vector4 V1R;

    [FieldOffset(64)]
    public Vector4 V2L;
    [FieldOffset(80)]
    public Vector4 V2R;

    [FieldOffset(96)]
    public Vector4 V3L;
    [FieldOffset(112)]
    public Vector4 V3R;

    [FieldOffset(128)]
    public Vector4 V4L;
    [FieldOffset(144)]
    public Vector4 V4R;

    [FieldOffset(160)]
    public Vector4 V5L;
    [FieldOffset(176)]
    public Vector4 V5R;

    [FieldOffset(192)]
    public Vector4 V6L;
    [FieldOffset(208)]
    public Vector4 V6R;

    [FieldOffset(224)]
    public Vector4 V7L;
    [FieldOffset(240)]
    public Vector4 V7R;

#pragma warning disable SA1310 // Field names should not contain underscore
    [FieldOffset(0)]
    public Vector256<float> V256_0;
    [FieldOffset(32)]
    public Vector256<float> V256_1;
    [FieldOffset(64)]
    public Vector256<float> V256_2;
    [FieldOffset(96)]
    public Vector256<float> V256_3;
    [FieldOffset(128)]
    public Vector256<float> V256_4;
    [FieldOffset(160)]
    public Vector256<float> V256_5;
    [FieldOffset(192)]
    public Vector256<float> V256_6;
    [FieldOffset(224)]
    public Vector256<float> V256_7;
#pragma warning restore SA1310 // Field names should not contain underscore

    /// <summary>
    /// Get/Set scalar elements at a given index
    /// </summary>
    /// <param name="idx">The index</param>
    /// <returns>The float value at the specified index</returns>
    public float this[int idx]
    {
        get => this[(uint)idx];
        set => this[(uint)idx] = value;
    }

    internal float this[nuint idx]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            DebugGuard.MustBeBetweenOrEqualTo((int)idx, 0, Size - 1, nameof(idx));
            ref float selfRef = ref Unsafe.As<Block8x8F, float>(ref this);
            return Unsafe.Add(ref selfRef, idx);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            DebugGuard.MustBeBetweenOrEqualTo((int)idx, 0, Size - 1, nameof(idx));
            ref float selfRef = ref Unsafe.As<Block8x8F, float>(ref this);
            Unsafe.Add(ref selfRef, idx) = value;
        }
    }

    public float this[int x, int y]
    {
        get => this[((uint)y * 8) + (uint)x];
        set => this[((uint)y * 8) + (uint)x] = value;
    }

    /// <summary>
    /// Load raw 32bit floating point data from source.
    /// </summary>
    /// <param name="data">Source</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static Block8x8F Load(Span<float> data)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(data.Length, Size, "data is too small");

        ref byte src = ref Unsafe.As<float, byte>(ref MemoryMarshal.GetReference(data));
        return Unsafe.ReadUnaligned<Block8x8F>(ref src);
    }

    /// <summary>
    /// Load raw 32bit floating point data from source
    /// </summary>
    /// <param name="source">Source</param>
    public unsafe void LoadFrom(ReadOnlySpan<int> source)
    {
        fixed (Vector4* ptr = &this.V0L)
        {
            float* fp = (float*)ptr;
            for (int i = 0; i < Size; i++)
            {
                fp[i] = source[i];
            }
        }
    }

    /// <summary>
    /// Copy raw 32bit floating point data to dest
    /// </summary>
    /// <param name="dest">Destination</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly void ScaledCopyTo(float[] dest)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(dest.Length, Size, "dest is too small");

        ref byte destRef = ref Unsafe.As<float, byte>(ref MemoryMarshal.GetArrayDataReference(dest));
        Unsafe.WriteUnaligned(ref destRef, this);
    }

    public float[] ToArray()
    {
        float[] result = new float[Size];
        this.ScaledCopyTo(result);
        return result;
    }

    /// <summary>
    /// Multiply all elements of the block.
    /// </summary>
    /// <param name="value">The value to multiply by.</param>
    public void MultiplyInPlace(float value)
    {
        Span<float> values = MemoryMarshal.CreateSpan(ref Unsafe.As<Block8x8F, float>(ref this), Size);
        TensorPrimitives.Multiply(values, value, values);
    }

    /// <summary>
    /// Sets all elements of the block to a value.
    /// </summary>
    /// <param name="value">The value.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void Fill(float value)
        => MemoryMarshal.CreateSpan(ref Unsafe.As<Block8x8F, float>(ref this), Size).Fill(value);

    public void RoundInto(ref Block8x8 dest)
    {
        for (int i = 0; i < Size; i++)
        {
            float val = this[i];
            if (val < 0)
            {
                val -= 0.5f;
            }
            else
            {
                val += 0.5f;
            }

            dest[i] = (short)val;
        }
    }

    public Block8x8 RoundAsInt16Block()
    {
        Block8x8 result = default;
        this.RoundInto(ref result);
        return result;
    }

    /// <summary>
    /// Fills the block from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source block.</param>
    public void LoadFrom(ref Block8x8 source)
    {
        // Each 16-bit value widens to 32 bits and converts exactly to float.
        // The block has 64 values. Thus, every width processes complete registers and has no remainder.
        ref short sourceBase = ref Unsafe.As<Block8x8, short>(ref source);
        ref float destinationBase = ref Unsafe.As<Block8x8F, float>(ref this);

        if (Vector512.IsHardwareAccelerated)
        {
            for (nuint i = 0; i < Size; i += 16)
            {
                Vector512<int> values = Vector512.WidenLower(Vector256.LoadUnsafe(ref sourceBase, i).ToVector512Unsafe());
                Vector512.ConvertToSingle(values).StoreUnsafe(ref destinationBase, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            for (nuint i = 0; i < Size; i += 8)
            {
                Vector256<int> values = Vector256_.Widen(Vector128.LoadUnsafe(ref sourceBase, i));
                Vector256.ConvertToSingle(values).StoreUnsafe(ref destinationBase, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            for (nuint i = 0; i < Size; i += 8)
            {
                Vector128<short> values = Vector128.LoadUnsafe(ref sourceBase, i);
                Vector128.ConvertToSingle(Vector128.WidenLower(values)).StoreUnsafe(ref destinationBase, i);
                Vector128.ConvertToSingle(Vector128.WidenUpper(values)).StoreUnsafe(ref destinationBase, i + 4);
            }
        }
        else
        {
            for (nuint i = 0; i < Size; i++)
            {
                Unsafe.Add(ref destinationBase, i) = Unsafe.Add(ref sourceBase, i);
            }
        }
    }

    /// <summary>
    /// Compares entire 8x8 block to a single scalar value.
    /// </summary>
    /// <param name="value">Value to compare to.</param>
    public bool EqualsToScalar(int value)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> targetVector = Vector256.Create(value);
            ref Vector256<float> blockStride = ref this.V256_0;

            for (nuint i = 0; i < RowCount; i++)
            {
                if (!Vector256.EqualsAll(Vector256.ConvertToInt32(Unsafe.Add(ref this.V256_0, i)), targetVector))
                {
                    return false;
                }
            }

            return true;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> targetVector = Vector128.Create(value);
            ref Vector4 blockStride = ref this.V0L;

            for (nuint i = 0; i < RowCount * 2; i++)
            {
                if (!Vector128.EqualsAll(Vector128.ConvertToInt32(Unsafe.Add(ref this.V0L, i).AsVector128()), targetVector))
                {
                    return false;
                }
            }

            return true;
        }

        ref float scalars = ref Unsafe.As<Block8x8F, float>(ref this);

        for (nuint i = 0; i < Size; i++)
        {
            if ((int)Unsafe.Add(ref scalars, i) != value)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public readonly bool Equals(Block8x8F other)
        => this.V0L == other.V0L
        && this.V0R == other.V0R
        && this.V1L == other.V1L
        && this.V1R == other.V1R
        && this.V2L == other.V2L
        && this.V2R == other.V2R
        && this.V3L == other.V3L
        && this.V3R == other.V3R
        && this.V4L == other.V4L
        && this.V4R == other.V4R
        && this.V5L == other.V5L
        && this.V5R == other.V5R
        && this.V6L == other.V6L
        && this.V6R == other.V6R
        && this.V7L == other.V7L
        && this.V7R == other.V7R;

    /// <inheritdoc />
    public override bool Equals(object? obj) => this.Equals((Block8x8F?)obj);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int left = HashCode.Combine(
            this.V0L,
            this.V1L,
            this.V2L,
            this.V3L,
            this.V4L,
            this.V5L,
            this.V6L,
            this.V7L);

        int right = HashCode.Combine(
            this.V0R,
            this.V1R,
            this.V2R,
            this.V3R,
            this.V4R,
            this.V5R,
            this.V6R,
            this.V7R);

        return HashCode.Combine(left, right);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append('[');
        for (int i = 0; i < Size - 1; i++)
        {
            sb.Append(this[i]).Append(',');
        }

        sb.Append(this[Size - 1]).Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Transpose the block in-place.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void TransposeInPlace() => Apply<TransposeOperator>(ref this);
}
