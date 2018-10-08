// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Points to a collection of of weights allocated in <see cref="KernelMap"/>.
    /// </summary>
    internal struct ResizeKernel
    {
        /// <summary>
        /// The local left index position
        /// </summary>
        public int Left;

        /// <summary>
        /// The length of the weights window
        /// </summary>
        public int Length;

        /// <summary>
        /// The buffer containing the weights values.
        /// </summary>
        private readonly Memory<float> buffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeKernel"/> struct.
        /// </summary>
        /// <param name="index">The destination index in the buffer</param>
        /// <param name="left">The local left index</param>
        /// <param name="buffer">The span</param>
        /// <param name="length">The length of the window</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal ResizeKernel(int index, int left, Buffer2D<float> buffer, int length)
        {
            int flatStartIndex = index * buffer.Width;
            this.Left = left;
            this.buffer = buffer.MemorySource.Memory.Slice(flatStartIndex, length);
            this.Length = length;
        }

        /// <summary>
        /// Gets a reference to the first item of the window.
        /// </summary>
        /// <returns>The reference to the first item of the window</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public ref float GetStartReference()
        {
            Span<float> span = this.buffer.Span;
            return ref span[0];
        }

        /// <summary>
        /// Gets the span representing the portion of the <see cref="KernelMap"/> that this window covers
        /// </summary>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Span<float> GetSpan() => this.buffer.Span;

        /// <summary>
        /// Computes the sum of vectors in 'rowSpan' weighted by weight values, pointed by this <see cref="ResizeKernel"/> instance.
        /// </summary>
        /// <param name="rowSpan">The input span of vectors</param>
        /// <param name="sourceX">The source row position.</param>
        /// <returns>The weighted sum</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 Convolve(Span<Vector4> rowSpan, int sourceX)
        {
            ref float horizontalValues = ref this.GetStartReference();
            int left = this.Left;
            ref Vector4 vecPtr = ref Unsafe.Add(ref MemoryMarshal.GetReference(rowSpan), left + sourceX);

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