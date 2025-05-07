// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using SixLabors.ImageSharp.Common.Helpers;
using Vector128_ = SixLabors.ImageSharp.Common.Helpers.Vector128Utilities;
using Vector256_ = SixLabors.ImageSharp.Common.Helpers.Vector256Utilities;

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

#pragma warning disable SA1600 // ElementsMustBeDocumented
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
#pragma warning restore SA1600 // ElementsMustBeDocumented

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
    public unsafe void LoadFrom(Span<int> source)
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
    public unsafe void ScaledCopyTo(float[] dest)
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
    [MethodImpl(InliningOptions.ShortMethod)]
    public void MultiplyInPlace(float value)
    {
        // TODO: Vector512
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> valueVec = Vector256.Create(value);
            this.V0 *= valueVec;
            this.V1 *= valueVec;
            this.V2 *= valueVec;
            this.V3 *= valueVec;
            this.V4 *= valueVec;
            this.V5 *= valueVec;
            this.V6 *= valueVec;
            this.V7 *= valueVec;
        }
        else
        {
            Vector4 valueVec = new(value);
            this.V0L *= valueVec;
            this.V0R *= valueVec;
            this.V1L *= valueVec;
            this.V1R *= valueVec;
            this.V2L *= valueVec;
            this.V2R *= valueVec;
            this.V3L *= valueVec;
            this.V3R *= valueVec;
            this.V4L *= valueVec;
            this.V4R *= valueVec;
            this.V5L *= valueVec;
            this.V5R *= valueVec;
            this.V6L *= valueVec;
            this.V6R *= valueVec;
            this.V7L *= valueVec;
            this.V7R *= valueVec;
        }
    }

    /// <summary>
    /// Multiply all elements of the block by the corresponding elements of 'other'.
    /// </summary>
    /// <param name="other">The other block.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public unsafe void MultiplyInPlace(ref Block8x8F other)
    {
        // TODO: Vector512
        if (Vector256.IsHardwareAccelerated)
        {
            this.V0 *= other.V0;
            this.V1 *= other.V1;
            this.V2 *= other.V2;
            this.V3 *= other.V3;
            this.V4 *= other.V4;
            this.V5 *= other.V5;
            this.V6 *= other.V6;
            this.V7 *= other.V7;
        }
        else
        {
            this.V0L *= other.V0L;
            this.V0R *= other.V0R;
            this.V1L *= other.V1L;
            this.V1R *= other.V1R;
            this.V2L *= other.V2L;
            this.V2R *= other.V2R;
            this.V3L *= other.V3L;
            this.V3R *= other.V3R;
            this.V4L *= other.V4L;
            this.V4R *= other.V4R;
            this.V5L *= other.V5L;
            this.V5R *= other.V5R;
            this.V6L *= other.V6L;
            this.V6R *= other.V6R;
            this.V7L *= other.V7L;
            this.V7R *= other.V7R;
        }
    }

    /// <summary>
    /// Adds a vector to all elements of the block.
    /// </summary>
    /// <param name="value">The added vector.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void AddInPlace(float value)
    {
        // TODO: Vector512
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> valueVec = Vector256.Create(value);
            this.V0 += valueVec;
            this.V1 += valueVec;
            this.V2 += valueVec;
            this.V3 += valueVec;
            this.V4 += valueVec;
            this.V5 += valueVec;
            this.V6 += valueVec;
            this.V7 += valueVec;
        }
        else
        {
            Vector4 valueVec = new(value);
            this.V0L += valueVec;
            this.V0R += valueVec;
            this.V1L += valueVec;
            this.V1R += valueVec;
            this.V2L += valueVec;
            this.V2R += valueVec;
            this.V3L += valueVec;
            this.V3R += valueVec;
            this.V4L += valueVec;
            this.V4R += valueVec;
            this.V5L += valueVec;
            this.V5R += valueVec;
            this.V6L += valueVec;
            this.V6R += valueVec;
            this.V7L += valueVec;
            this.V7R += valueVec;
        }
    }

    /// <summary>
    /// Quantize input block, transpose, apply zig-zag ordering and store as <see cref="Block8x8"/>.
    /// </summary>
    /// <param name="block">Source block.</param>
    /// <param name="dest">Destination block.</param>
    /// <param name="qt">The quantization table.</param>
    public static void Quantize(ref Block8x8F block, ref Block8x8 dest, ref Block8x8F qt)
    {
        if (Avx2.IsSupported)
        {
            MultiplyIntoInt16_Avx2(ref block, ref qt, ref dest);
            ZigZag.ApplyTransposingZigZagOrderingAvx2(ref dest);
        }
        else if (Ssse3.IsSupported)
        {
            MultiplyIntoInt16_Sse2(ref block, ref qt, ref dest);
            ZigZag.ApplyTransposingZigZagOrderingSsse3(ref dest);
        }
        else
        {
            for (int i = 0; i < Size; i++)
            {
                int idx = ZigZag.TransposingOrder[i];
                float quantizedVal = block[idx] * qt[idx];
                quantizedVal += quantizedVal < 0 ? -0.5f : 0.5f;
                dest[i] = (short)quantizedVal;
            }
        }
    }

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
    /// Level shift by +maximum/2, clip to [0..maximum], and round all the values in the block.
    /// </summary>
    /// <param name="maximum">The maximum value.</param>
    public void NormalizeColorsAndRoundInPlace(float maximum)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            this.NormalizeColorsAndRoundInPlaceVector256(maximum);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            this.NormalizeColorsAndRoundInPlaceVector128(maximum);
        }
        else
        {
            this.NormalizeColorsInPlace(maximum);
            this.RoundInPlace();
        }
    }

    /// <summary>
    /// Rounds all values in the block.
    /// </summary>
    public void RoundInPlace()
    {
        for (int i = 0; i < Size; i++)
        {
            this[i] = MathF.Round(this[i]);
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public void LoadFrom(ref Block8x8 source)
    {
        if (SimdUtils.HasVector8)
        {
            this.LoadFromInt16ExtendedAvx2(ref source);
            return;
        }

        this.LoadFromInt16Scalar(ref source);
    }

    /// <summary>
    /// Loads values from <paramref name="source"/> using extended AVX2 intrinsics.
    /// </summary>
    /// <param name="source">The source <see cref="Block8x8"/></param>
    public void LoadFromInt16ExtendedAvx2(ref Block8x8 source)
    {
        DebugGuard.IsTrue(
            Avx2.IsSupported,
            "LoadFromUInt16ExtendedAvx2 only works on AVX2 compatible architecture!");

        ref short sRef = ref Unsafe.As<Block8x8, short>(ref source);
        ref Vector256<float> dRef = ref Unsafe.As<Block8x8F, Vector256<float>>(ref this);

        // Vector256<ushort>.Count == 16 on AVX2
        // We can process 2 block rows in a single step
        Vector256<int> top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef));
        Vector256<int> bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)Vector256<int>.Count));
        dRef = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 1) = Avx.ConvertToVector256Single(bottom);

        top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 2)));
        bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 3)));
        Unsafe.Add(ref dRef, 2) = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 3) = Avx.ConvertToVector256Single(bottom);

        top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 4)));
        bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 5)));
        Unsafe.Add(ref dRef, 4) = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 5) = Avx.ConvertToVector256Single(bottom);

        top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 6)));
        bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 7)));
        Unsafe.Add(ref dRef, 6) = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 7) = Avx.ConvertToVector256Single(bottom);
    }

    /// <summary>
    /// Compares entire 8x8 block to a single scalar value.
    /// </summary>
    /// <param name="value">Value to compare to.</param>
    public bool EqualsToScalar(int value)
    {
        if (Avx2.IsSupported)
        {
            const int equalityMask = unchecked((int)0b1111_1111_1111_1111_1111_1111_1111_1111);

            Vector256<int> targetVector = Vector256.Create(value);
            ref Vector256<float> blockStride = ref this.V0;

            for (nuint i = 0; i < RowCount; i++)
            {
                Vector256<int> areEqual = Avx2.CompareEqual(Avx.ConvertToVector256Int32WithTruncation(Unsafe.Add(ref this.V0, i)), targetVector);
                if (Avx2.MoveMask(areEqual.AsByte()) != equalityMask)
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
    public bool Equals(Block8x8F other)
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
    public void TransposeInPlace()
    {
        if (Avx.IsSupported)
        {
            this.TransposeInplace_Avx();
        }
        else
        {
            this.TransposeInPlace_Scalar();
        }
    }

    /// <summary>
    /// Scalar in-place transpose implementation for <see cref="TransposeInPlace"/>
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void TransposeInPlace_Scalar()
    {
        ref float elemRef = ref Unsafe.As<Block8x8F, float>(ref this);

        // row #0
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 1), ref Unsafe.Add(ref elemRef, 8));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 2), ref Unsafe.Add(ref elemRef, 16));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 3), ref Unsafe.Add(ref elemRef, 24));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 4), ref Unsafe.Add(ref elemRef, 32));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 5), ref Unsafe.Add(ref elemRef, 40));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 6), ref Unsafe.Add(ref elemRef, 48));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 7), ref Unsafe.Add(ref elemRef, 56));

        // row #1
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 10), ref Unsafe.Add(ref elemRef, 17));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 11), ref Unsafe.Add(ref elemRef, 25));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 12), ref Unsafe.Add(ref elemRef, 33));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 13), ref Unsafe.Add(ref elemRef, 41));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 14), ref Unsafe.Add(ref elemRef, 49));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 15), ref Unsafe.Add(ref elemRef, 57));

        // row #2
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 19), ref Unsafe.Add(ref elemRef, 26));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 20), ref Unsafe.Add(ref elemRef, 34));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 21), ref Unsafe.Add(ref elemRef, 42));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 22), ref Unsafe.Add(ref elemRef, 50));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 23), ref Unsafe.Add(ref elemRef, 58));

        // row #3
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 28), ref Unsafe.Add(ref elemRef, 35));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 29), ref Unsafe.Add(ref elemRef, 43));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 30), ref Unsafe.Add(ref elemRef, 51));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 31), ref Unsafe.Add(ref elemRef, 59));

        // row #4
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 37), ref Unsafe.Add(ref elemRef, 44));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 38), ref Unsafe.Add(ref elemRef, 52));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 39), ref Unsafe.Add(ref elemRef, 60));

        // row #5
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 46), ref Unsafe.Add(ref elemRef, 53));
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 47), ref Unsafe.Add(ref elemRef, 61));

        // row #6
        RuntimeUtility.Swap(ref Unsafe.Add(ref elemRef, 55), ref Unsafe.Add(ref elemRef, 62));
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector<float> NormalizeAndRound(Vector<float> row, Vector<float> off, Vector<float> max)
    {
        row += off;
        row = Vector.Max(row, Vector<float>.Zero);
        row = Vector.Min(row, max);
        return row.FastRound();
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector256<float> NormalizeAndRoundVector256(Vector256<float> row, Vector256<float> off, Vector256<float> max)
    {
        row += off;
        row = Vector256.Max(row, Vector256<float>.Zero);
        row = Vector256.Min(row, max);
        return Vector256_.RoundToNearestInteger(row);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector128<float> NormalizeAndRoundVector128(Vector128<float> row, Vector128<float> off, Vector128<float> max)
    {
        row += off;
        row = Vector128.Max(row, Vector128<float>.Zero);
        row = Vector128.Min(row, max);
        return Vector128_.RoundToNearestInteger(row);
    }
}
