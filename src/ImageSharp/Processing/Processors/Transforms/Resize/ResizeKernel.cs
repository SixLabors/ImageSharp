// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Points to a collection of weights allocated in <see cref="ResizeKernelMap"/>.
/// </summary>
internal readonly unsafe struct ResizeKernel
{
    private readonly float* bufferPtr;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResizeKernel"/> struct.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal ResizeKernel(int startIndex, float* bufferPtr, int length)
    {
        this.StartIndex = startIndex;
        this.bufferPtr = bufferPtr;
        this.Length = length;
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
        get => new(this.bufferPtr, this.Length);
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
        if (Avx2.IsSupported && Fma.IsSupported)
        {
            float* bufferStart = this.bufferPtr;
            float* bufferEnd = bufferStart + (this.Length & ~3);
            Vector256<float> result256_0 = Vector256<float>.Zero;
            Vector256<float> result256_1 = Vector256<float>.Zero;
            ReadOnlySpan<byte> maskBytes =
            [
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                1, 0, 0, 0, 1, 0, 0, 0,
                1, 0, 0, 0, 1, 0, 0, 0
            ];
            Vector256<int> mask = Unsafe.ReadUnaligned<Vector256<int>>(ref MemoryMarshal.GetReference(maskBytes));

            while (bufferStart < bufferEnd)
            {
                // It is important to use a single expression here so that the JIT will correctly use vfmadd231ps
                // for the FMA operation, and execute it directly on the target register and reading directly from
                // memory for the first parameter. This skips initializing a SIMD register, and an extra copy.
                // The code below should compile in the following assembly on .NET 5 x64:
                //
                // vmovsd xmm2, [rax]               ; load *(double*)bufferStart into xmm2 as [ab, _]
                // vpermps ymm2, ymm1, ymm2         ; permute as a float YMM register to [a, a, a, a, b, b, b, b]
                // vfmadd231ps ymm0, ymm2, [r8]     ; result256_0 = FMA(pixels, factors) + result256_0
                //
                // For tracking the codegen issue with FMA, see: https://github.com/dotnet/runtime/issues/12212.
                // Additionally, we're also unrolling two computations per each loop iterations to leverage the
                // fact that most CPUs have two ports to schedule multiply operations for FMA instructions.
                result256_0 = Fma.MultiplyAdd(
                    Unsafe.As<Vector4, Vector256<float>>(ref rowStartRef),
                    Avx2.PermuteVar8x32(Vector256.CreateScalarUnsafe(*(double*)bufferStart).AsSingle(), mask),
                    result256_0);

                result256_1 = Fma.MultiplyAdd(
                    Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref rowStartRef, 2)),
                    Avx2.PermuteVar8x32(Vector256.CreateScalarUnsafe(*(double*)(bufferStart + 2)).AsSingle(), mask),
                    result256_1);

                bufferStart += 4;
                rowStartRef = ref Unsafe.Add(ref rowStartRef, 4);
            }

            result256_0 = Avx.Add(result256_0, result256_1);

            if ((this.Length & 3) >= 2)
            {
                result256_0 = Fma.MultiplyAdd(
                    Unsafe.As<Vector4, Vector256<float>>(ref rowStartRef),
                    Avx2.PermuteVar8x32(Vector256.CreateScalarUnsafe(*(double*)bufferStart).AsSingle(), mask),
                    result256_0);

                bufferStart += 2;
                rowStartRef = ref Unsafe.Add(ref rowStartRef, 2);
            }

            Vector128<float> result128 = Sse.Add(result256_0.GetLower(), result256_0.GetUpper());

            if ((this.Length & 1) != 0)
            {
                result128 = Fma.MultiplyAdd(
                    Unsafe.As<Vector4, Vector128<float>>(ref rowStartRef),
                    Vector128.Create(*bufferStart),
                    result128);
            }

            return *(Vector4*)&result128;
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
                rowStartRef = ref Unsafe.Add(ref rowStartRef, 1);
            }

            return result;
        }
    }

    /// <summary>
    /// Copy the contents of <see cref="ResizeKernel"/> altering <see cref="StartIndex"/>
    /// to the value <paramref name="left"/>.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal ResizeKernel AlterLeftValue(int left)
        => new(left, this.bufferPtr, this.Length);

    internal void Fill(Span<double> values)
    {
        DebugGuard.IsTrue(values.Length == this.Length, nameof(values), "ResizeKernel.Fill: values.Length != this.Length!");

        for (int i = 0; i < this.Length; i++)
        {
            this.Values[i] = (float)values[i];
        }
    }
}
