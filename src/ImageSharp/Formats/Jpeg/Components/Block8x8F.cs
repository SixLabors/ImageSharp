// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
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
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> valueVec = Vector256.Create(value);
            this.V256_0 *= valueVec;
            this.V256_1 *= valueVec;
            this.V256_2 *= valueVec;
            this.V256_3 *= valueVec;
            this.V256_4 *= valueVec;
            this.V256_5 *= valueVec;
            this.V256_6 *= valueVec;
            this.V256_7 *= valueVec;
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
        if (Vector256.IsHardwareAccelerated)
        {
            this.V256_0 *= other.V256_0;
            this.V256_1 *= other.V256_1;
            this.V256_2 *= other.V256_2;
            this.V256_3 *= other.V256_3;
            this.V256_4 *= other.V256_4;
            this.V256_5 *= other.V256_5;
            this.V256_6 *= other.V256_6;
            this.V256_7 *= other.V256_7;
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
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> valueVec = Vector256.Create(value);
            this.V256_0 += valueVec;
            this.V256_1 += valueVec;
            this.V256_2 += valueVec;
            this.V256_3 += valueVec;
            this.V256_4 += valueVec;
            this.V256_5 += valueVec;
            this.V256_6 += valueVec;
            this.V256_7 += valueVec;
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
        if (Vector256.IsHardwareAccelerated)
        {
            MultiplyIntoInt16Vector256(ref block, ref qt, ref dest);
            ZigZag.ApplyTransposingZigZagOrderingAvx2(ref dest);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            MultiplyIntoInt16Vector128(ref block, ref qt, ref dest);
            ZigZag.ApplyTransposingZigZagOrderingVector128(ref dest);
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
    /// Level shift by +maximum/2, clip to [0, maximum]
    /// </summary>
    /// <param name="maximum">The maximum value to normalize to.</param>
    public void NormalizeColorsInPlace(float maximum)
    {
        Vector4 min = Vector4.Zero;
        Vector4 max = new(maximum);
        Vector4 off = new(MathF.Ceiling(maximum * 0.5F));

        this.V0L = Vector4.Clamp(this.V0L + off, min, max);
        this.V0R = Vector4.Clamp(this.V0R + off, min, max);
        this.V1L = Vector4.Clamp(this.V1L + off, min, max);
        this.V1R = Vector4.Clamp(this.V1R + off, min, max);
        this.V2L = Vector4.Clamp(this.V2L + off, min, max);
        this.V2R = Vector4.Clamp(this.V2R + off, min, max);
        this.V3L = Vector4.Clamp(this.V3L + off, min, max);
        this.V3R = Vector4.Clamp(this.V3R + off, min, max);
        this.V4L = Vector4.Clamp(this.V4L + off, min, max);
        this.V4R = Vector4.Clamp(this.V4R + off, min, max);
        this.V5L = Vector4.Clamp(this.V5L + off, min, max);
        this.V5R = Vector4.Clamp(this.V5R + off, min, max);
        this.V6L = Vector4.Clamp(this.V6L + off, min, max);
        this.V6R = Vector4.Clamp(this.V6R + off, min, max);
        this.V7L = Vector4.Clamp(this.V7L + off, min, max);
        this.V7R = Vector4.Clamp(this.V7R + off, min, max);
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
        if (Vector256.IsHardwareAccelerated)
        {
            this.LoadFromInt16ExtendedVector256(ref source);
            return;
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            this.LoadFromInt16ExtendedVector128(ref source);
            return;
        }

        this.LoadFromInt16Scalar(ref source);
    }

    /// <summary>
    /// Fill the block from <paramref name="source"/> doing short -&gt; float conversion.
    /// </summary>
    /// <param name="source">The source block</param>
    public void LoadFromInt16Scalar(ref Block8x8 source)
    {
        ref short selfRef = ref Unsafe.As<Block8x8, short>(ref source);

        this.V0L.X = Unsafe.Add(ref selfRef, 0);
        this.V0L.Y = Unsafe.Add(ref selfRef, 1);
        this.V0L.Z = Unsafe.Add(ref selfRef, 2);
        this.V0L.W = Unsafe.Add(ref selfRef, 3);
        this.V0R.X = Unsafe.Add(ref selfRef, 4);
        this.V0R.Y = Unsafe.Add(ref selfRef, 5);
        this.V0R.Z = Unsafe.Add(ref selfRef, 6);
        this.V0R.W = Unsafe.Add(ref selfRef, 7);

        this.V1L.X = Unsafe.Add(ref selfRef, 8);
        this.V1L.Y = Unsafe.Add(ref selfRef, 9);
        this.V1L.Z = Unsafe.Add(ref selfRef, 10);
        this.V1L.W = Unsafe.Add(ref selfRef, 11);
        this.V1R.X = Unsafe.Add(ref selfRef, 12);
        this.V1R.Y = Unsafe.Add(ref selfRef, 13);
        this.V1R.Z = Unsafe.Add(ref selfRef, 14);
        this.V1R.W = Unsafe.Add(ref selfRef, 15);

        this.V2L.X = Unsafe.Add(ref selfRef, 16);
        this.V2L.Y = Unsafe.Add(ref selfRef, 17);
        this.V2L.Z = Unsafe.Add(ref selfRef, 18);
        this.V2L.W = Unsafe.Add(ref selfRef, 19);
        this.V2R.X = Unsafe.Add(ref selfRef, 20);
        this.V2R.Y = Unsafe.Add(ref selfRef, 21);
        this.V2R.Z = Unsafe.Add(ref selfRef, 22);
        this.V2R.W = Unsafe.Add(ref selfRef, 23);

        this.V3L.X = Unsafe.Add(ref selfRef, 24);
        this.V3L.Y = Unsafe.Add(ref selfRef, 25);
        this.V3L.Z = Unsafe.Add(ref selfRef, 26);
        this.V3L.W = Unsafe.Add(ref selfRef, 27);
        this.V3R.X = Unsafe.Add(ref selfRef, 28);
        this.V3R.Y = Unsafe.Add(ref selfRef, 29);
        this.V3R.Z = Unsafe.Add(ref selfRef, 30);
        this.V3R.W = Unsafe.Add(ref selfRef, 31);

        this.V4L.X = Unsafe.Add(ref selfRef, 32);
        this.V4L.Y = Unsafe.Add(ref selfRef, 33);
        this.V4L.Z = Unsafe.Add(ref selfRef, 34);
        this.V4L.W = Unsafe.Add(ref selfRef, 35);
        this.V4R.X = Unsafe.Add(ref selfRef, 36);
        this.V4R.Y = Unsafe.Add(ref selfRef, 37);
        this.V4R.Z = Unsafe.Add(ref selfRef, 38);
        this.V4R.W = Unsafe.Add(ref selfRef, 39);

        this.V5L.X = Unsafe.Add(ref selfRef, 40);
        this.V5L.Y = Unsafe.Add(ref selfRef, 41);
        this.V5L.Z = Unsafe.Add(ref selfRef, 42);
        this.V5L.W = Unsafe.Add(ref selfRef, 43);
        this.V5R.X = Unsafe.Add(ref selfRef, 44);
        this.V5R.Y = Unsafe.Add(ref selfRef, 45);
        this.V5R.Z = Unsafe.Add(ref selfRef, 46);
        this.V5R.W = Unsafe.Add(ref selfRef, 47);

        this.V6L.X = Unsafe.Add(ref selfRef, 48);
        this.V6L.Y = Unsafe.Add(ref selfRef, 49);
        this.V6L.Z = Unsafe.Add(ref selfRef, 50);
        this.V6L.W = Unsafe.Add(ref selfRef, 51);
        this.V6R.X = Unsafe.Add(ref selfRef, 52);
        this.V6R.Y = Unsafe.Add(ref selfRef, 53);
        this.V6R.Z = Unsafe.Add(ref selfRef, 54);
        this.V6R.W = Unsafe.Add(ref selfRef, 55);

        this.V7L.X = Unsafe.Add(ref selfRef, 56);
        this.V7L.Y = Unsafe.Add(ref selfRef, 57);
        this.V7L.Z = Unsafe.Add(ref selfRef, 58);
        this.V7L.W = Unsafe.Add(ref selfRef, 59);
        this.V7R.X = Unsafe.Add(ref selfRef, 60);
        this.V7R.Y = Unsafe.Add(ref selfRef, 61);
        this.V7R.Z = Unsafe.Add(ref selfRef, 62);
        this.V7R.W = Unsafe.Add(ref selfRef, 63);
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
        if (Vector256.IsHardwareAccelerated)
        {
            this.TransposeInPlaceVector256();
        }
        else
        {
            // TODO: Can we provide a Vector128 implementation for this?
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
}
