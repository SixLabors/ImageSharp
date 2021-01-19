// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
#if SUPPORTS_RUNTIME_INTRINSICS
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
        /// <value>The <see cref="Span{T}"/>.
        /// </value>
        public Span<float> Values
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => new Span<float>(this.bufferPtr, this.Length);
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
                float* bufferEnd = bufferStart + (this.Length & ~1);
                Vector256<float> result256 = Vector256<float>.Zero;
                var mask = Vector256.Create(0, 0, 0, 0, 1, 1, 1, 1);

                while (bufferStart < bufferEnd)
                {
                    Vector256<float> rowItem256 = Unsafe.As<Vector4, Vector256<float>>(ref rowStartRef);
                    Vector256<float> bufferItem256 = Avx2.PermuteVar8x32(Vector256.Create(*(double*)bufferStart).AsSingle(), mask);

                    result256 = Fma.MultiplyAdd(rowItem256, bufferItem256, result256);

                    bufferStart += 2;
                    rowStartRef = ref Unsafe.Add(ref rowStartRef, 2);
                }

                Vector128<float> result128 = Sse.Add(result256.GetLower(), result256.GetUpper());

                if ((this.Length & 1) != 0)
                {
                    Vector128<float> rowItem128 = Unsafe.As<Vector4, Vector128<float>>(ref rowStartRef);
                    var bufferItem128 = Vector128.Create(*bufferStart);

                    result128 = Fma.MultiplyAdd(rowItem128, bufferItem128, result128);
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

                    rowStartRef = ref Unsafe.Add(ref rowStartRef, 1);
                    bufferStart++;
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

        internal void Fill(Span<double> values)
        {
            DebugGuard.IsTrue(values.Length == this.Length, nameof(values), "ResizeKernel.Fill: values.Length != this.Length!");

            for (int i = 0; i < this.Length; i++)
            {
                this.Values[i] = (float)values[i];
            }
        }
    }
}
