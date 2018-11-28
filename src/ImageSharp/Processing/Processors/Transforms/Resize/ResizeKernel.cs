// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        internal ResizeKernel(int left, float* bufferPtr, int length)
        {
            this.Left = left;
            this.bufferPtr = bufferPtr;
            this.Length = length;
        }

        /// <summary>
        /// Gets the left index for the destination row
        /// </summary>
        public int Left { get; }

        /// <summary>
        /// Gets the the length of the kernel
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the span representing the portion of the <see cref="ResizeKernelMap"/> that this window covers
        /// </summary>
        /// <value>The <see cref="Span{T}"/>
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
        {
            ref float horizontalValues = ref Unsafe.AsRef<float>(this.bufferPtr);
            int left = this.Left;
            ref Vector4 vecPtr = ref Unsafe.Add(ref MemoryMarshal.GetReference(rowSpan), left);

            // Destination color components
            Vector4 result = Vector4.Zero;

            for (int i = 0; i < this.Length; i++)
            {
                float weight = Unsafe.Add(ref horizontalValues, i);
                Vector4 v = Unsafe.Add(ref vecPtr, i);
                result += v * weight;
            }

            return result;
        }

        /// <summary>
        /// Copy the contents of <see cref="ResizeKernel"/> altering <see cref="Left"/>
        /// to the value <paramref name="left"/>.
        /// </summary>
        internal ResizeKernel AlterLeftValue(int left)
        {
            return new ResizeKernel(left, this.bufferPtr, this.Length);
        }

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