// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Points to a collection of of weights allocated in <see cref="KernelMap"/>.
    /// </summary>
    internal struct ResizeKernel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeKernel"/> struct.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal ResizeKernel(int left, Memory<float> bufferSlice)
        {
            this.Left = left;
            this.BufferSlice = bufferSlice;
        }

        /// <summary>
        /// Gets the left index for the destination row
        /// </summary>
        public int Left { get; }

        /// <summary>
        /// Gets the slice of the buffer containing the weights values.
        /// </summary>
        public Memory<float> BufferSlice { get; }

        /// <summary>
        /// Gets the the length of the kernel
        /// </summary>
        public int Length => this.BufferSlice.Length;

        /// <summary>
        /// Gets the span representing the portion of the <see cref="KernelMap"/> that this window covers
        /// </summary>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Span<float> GetValues() => this.BufferSlice.Span;

        /// <summary>
        /// Computes the sum of vectors in 'rowSpan' weighted by weight values, pointed by this <see cref="ResizeKernel"/> instance.
        /// </summary>
        /// <param name="rowSpan">The input span of vectors</param>
        /// <returns>The weighted sum</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 Convolve(Span<Vector4> rowSpan)
        {
            ref float horizontalValues = ref MemoryMarshal.GetReference(this.GetValues());
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
    }
}