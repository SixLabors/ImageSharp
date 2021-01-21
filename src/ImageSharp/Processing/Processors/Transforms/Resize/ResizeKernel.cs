// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Points to a collection of of weights allocated in <see cref="ResizeKernelMap"/>.
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
        /// Gets the the length of the kernel.
        /// </summary>
        public int Length
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get;
        }

        /// <summary>
        /// Gets the span representing the portion of the <see cref="ResizeKernelMap"/> that this window covers.
        /// </summary>
        /// <value>The <see cref="Span{T}"/>.</value>
        /// <remarks>When FMA is used, this span is 4x as long as <see cref="Length"/>.</remarks>
        public Span<float> Values
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                if (Fma.IsSupported)
                {
                    return new Span<float>(this.bufferPtr, this.Length * 4);
                }
#endif
                return new Span<float>(this.bufferPtr, this.Length);
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
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Fma.IsSupported)
            {
                float* bufferStart = this.bufferPtr;
                ref Vector4 rowEndRef = ref Unsafe.Add(ref rowStartRef, this.Length & ~3);
                Vector256<float> result256_0 = Vector256<float>.Zero;
                Vector256<float> result256_1 = Vector256<float>.Zero;

                while (Unsafe.IsAddressLessThan(ref rowStartRef, ref rowEndRef))
                {
                    // It is important to use a single expression here so that the JIT will correctly use vfmadd132ps
                    // for the FMA operation, and execute it directly on the target register and reading directly from
                    // memory for the first parameter. This skips extra copies compared to using local variables.
                    // The code below should compile in the following assembly on .NET 5 x64:
                    //
                    // vmovupd ymm2, [r8]               ; load ref *bufferStart into ymm2 as [a, a, a, a, b, b, b, b]
                    // vfmadd132ps ymm2, ymm0, [rax]    ; FMA operation with a pair of pixels, into ymm2
                    // vmovaps ymm0, ymm2               ; copy the partial results to the accumulator result256_0
                    //
                    // For tracking the codegen issue with FMA, see: https://github.com/dotnet/runtime/issues/12212.
                    // Additionally, we're also unrolling two computations per each loop iterations to leverage the
                    // fact that most CPUs have two ports to schedule multiply operations for FMA instructions.
                    result256_0 = Fma.MultiplyAdd(
                        Unsafe.As<Vector4, Vector256<float>>(ref rowStartRef),
                        Unsafe.As<float, Vector256<float>>(ref *bufferStart),
                        result256_0);

                    result256_1 = Fma.MultiplyAdd(
                        Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref rowStartRef, 2)),
                        Unsafe.As<float, Vector256<float>>(ref bufferStart[8]),
                        result256_1);

                    bufferStart += 16;
                    rowStartRef = ref Unsafe.Add(ref rowStartRef, 4);
                }

                result256_0 = Avx.Add(result256_0, result256_1);

                if ((this.Length & 3) >= 2)
                {
                    result256_0 = Fma.MultiplyAdd(
                        Unsafe.As<Vector4, Vector256<float>>(ref rowStartRef),
                        Unsafe.As<float, Vector256<float>>(ref *bufferStart),
                        result256_0);

                    bufferStart += 8;
                    rowStartRef = ref Unsafe.Add(ref rowStartRef, 2);
                }

                Vector128<float> result128 = Sse.Add(result256_0.GetLower(), result256_0.GetUpper());

                if ((this.Length & 1) != 0)
                {
                    result128 = Fma.MultiplyAdd(
                        Unsafe.As<Vector4, Vector128<float>>(ref rowStartRef),
                        Unsafe.As<float, Vector128<float>>(ref *bufferStart),
                        result128);
                }

                return *(Vector4*)&result128;
            }
            else
#endif
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
            => new ResizeKernel(left, this.bufferPtr, this.Length);

        internal void FillOrCopyAndExpandForFma(Span<float> values)
        {
            DebugGuard.IsTrue(values.Length == this.Length, nameof(values), "ResizeKernel.Fill: values.Length != this.Length!");

#if SUPPORTS_RUNTIME_INTRINSICS
            if (Fma.IsSupported)
            {
                var bufferStart = (Vector4*)this.bufferPtr;
                ref float valuesStart = ref MemoryMarshal.GetReference(values);
                ref float valuesEnd = ref Unsafe.Add(ref valuesStart, values.Length);

                while (Unsafe.IsAddressLessThan(ref valuesStart, ref valuesEnd))
                {
                    *bufferStart = new Vector4(valuesStart);

                    bufferStart++;
                    valuesStart = ref Unsafe.Add(ref valuesStart, 1);
                }
            }
            else
#endif
            {
                values.CopyTo(this.Values);
            }
        }
    }
}
