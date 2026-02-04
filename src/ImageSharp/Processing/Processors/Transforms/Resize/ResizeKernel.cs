// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Points to a collection of weights allocated in <see cref="ResizeKernelMap"/>.
/// </summary>
internal readonly unsafe struct ResizeKernel
{
    /// <summary>
    /// The buffer with the convolution factors.
    /// Note that when FMA is supported, this is of size 4x that reported in <see cref="Length"/>.
    /// </summary>
    private readonly float* bufferPtr;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResizeKernel"/> struct.
    /// </summary>
    /// <param name="startIndex">The starting index for the destination row.</param>
    /// <param name="bufferPtr">The pointer to the buffer with the convolution factors.</param>
    /// <param name="length">The length of the kernel.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal ResizeKernel(int startIndex, float* bufferPtr, int length)
    {
        this.StartIndex = startIndex;
        this.bufferPtr = bufferPtr;
        this.Length = length;
    }

    /// <summary>
    /// Gets a value indicating whether vectorization is supported.
    /// </summary>
    public static bool IsHardwareAccelerated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256.IsHardwareAccelerated;
    }

    /// <summary>
    /// Gets the start index for the destination row.
    /// </summary>
    public int StartIndex
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        get;
    }

    /// <summary>
    /// Gets the length of the kernel.
    /// </summary>
    public int Length
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        get;
    }

    /// <summary>
    /// Gets the span representing the portion of the <see cref="ResizeKernelMap"/> that this window covers.
    /// </summary>
    /// <value>The <see cref="Span{T}"/>.
    /// </value>
    public Span<float> Values
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        get
        {
            if (Vector256.IsHardwareAccelerated)
            {
                return new(this.bufferPtr, this.Length * 4);
            }

            return new(this.bufferPtr, this.Length);
        }
    }

    /// <summary>
    /// Computes the sum of vectors in 'rowSpan' weighted by weight values, pointed by this <see cref="ResizeKernel"/> instance.
    /// </summary>
    /// <param name="rowSpan">The input span of vectors</param>
    /// <returns>The weighted sum</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public Vector4 Convolve(Span<Vector4> rowSpan)
        => this.ConvolveCore(ref rowSpan[this.StartIndex]);

    [MethodImpl(InliningOptions.ShortMethod)]
    public Vector4 ConvolveCore(ref Vector4 rowStartRef)
    {
        if (IsHardwareAccelerated)
        {
            float* bufferStart = this.bufferPtr;
            ref Vector4 rowEndRef = ref Unsafe.Add(ref rowStartRef, this.Length & ~3);
            Vector256<float> result256_0 = Vector256<float>.Zero;
            Vector256<float> result256_1 = Vector256<float>.Zero;

            while (Unsafe.IsAddressLessThan(ref rowStartRef, ref rowEndRef))
            {
                Vector256<float> pixels256_0 = Unsafe.As<Vector4, Vector256<float>>(ref rowStartRef);
                Vector256<float> pixels256_1 = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref rowStartRef, (nuint)2));

                result256_0 = Vector256_.MultiplyAdd(result256_0, Vector256.Load(bufferStart), pixels256_0);
                result256_1 = Vector256_.MultiplyAdd(result256_1, Vector256.Load(bufferStart + 8), pixels256_1);

                bufferStart += 16;
                rowStartRef = ref Unsafe.Add(ref rowStartRef, (nuint)4);
            }

            result256_0 += result256_1;

            if ((this.Length & 3) >= 2)
            {
                Vector256<float> pixels256_0 = Unsafe.As<Vector4, Vector256<float>>(ref rowStartRef);
                result256_0 = Vector256_.MultiplyAdd(result256_0, Vector256.Load(bufferStart), pixels256_0);

                bufferStart += 8;
                rowStartRef = ref Unsafe.Add(ref rowStartRef, (nuint)2);
            }

            Vector128<float> result128 = result256_0.GetLower() + result256_0.GetUpper();

            if ((this.Length & 1) != 0)
            {
                Vector128<float> pixels128 = Unsafe.As<Vector4, Vector128<float>>(ref rowStartRef);
                result128 = Vector128_.MultiplyAdd(result128, Vector128.Load(bufferStart), pixels128);
            }

            return result128.AsVector4();
        }
        else
        {
            // Destination color components
            Vector4 result = Vector4.Zero;
            float* bufferStart = this.bufferPtr;
            float* bufferEnd = this.bufferPtr + this.Length;

            while (bufferStart < bufferEnd)
            {
                // Vector4 v = offsetedRowSpan[i];
                result += rowStartRef * *bufferStart;

                bufferStart++;
                rowStartRef = ref Unsafe.Add(ref rowStartRef, (nuint)1);
            }

            return result;
        }
    }

    /// <summary>
    /// Copy the contents of <see cref="ResizeKernel"/> altering <see cref="StartIndex"/>
    /// to the value <paramref name="left"/>.
    /// </summary>
    /// <param name="left">The new value for <see cref="StartIndex"/>.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal ResizeKernel AlterLeftValue(int left)
        => new(left, this.bufferPtr, this.Length);

    internal void FillOrCopyAndExpand(Span<float> values)
    {
        DebugGuard.IsTrue(values.Length == this.Length, nameof(values), "ResizeKernel.Fill: values.Length != this.Length!");

        if (IsHardwareAccelerated)
        {
            Vector4* bufferStart = (Vector4*)this.bufferPtr;
            ref float valuesStart = ref MemoryMarshal.GetReference(values);
            ref float valuesEnd = ref Unsafe.Add(ref valuesStart, values.Length);

            while (Unsafe.IsAddressLessThan(ref valuesStart, ref valuesEnd))
            {
                *bufferStart = new Vector4(valuesStart);

                bufferStart++;
                valuesStart = ref Unsafe.Add(ref valuesStart, (nuint)1);
            }
        }
        else
        {
            values.CopyTo(this.Values);
        }
    }
}
