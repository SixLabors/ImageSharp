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
    /// Points to a collection of of weights allocated in <see cref="WeightsBuffer"/>.
    /// </summary>
    internal struct WeightsWindow
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
        /// The index in the destination buffer
        /// </summary>
        private readonly int flatStartIndex;

        /// <summary>
        /// The buffer containing the weights values.
        /// </summary>
        private readonly MemorySource<float> buffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="WeightsWindow"/> struct.
        /// </summary>
        /// <param name="index">The destination index in the buffer</param>
        /// <param name="left">The local left index</param>
        /// <param name="buffer">The span</param>
        /// <param name="length">The length of the window</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal WeightsWindow(int index, int left, Buffer2D<float> buffer, int length)
        {
            this.flatStartIndex = (index * buffer.Width) + left;
            this.Left = left;
            this.buffer = buffer.MemorySource;
            this.Length = length;
        }

        /// <summary>
        /// Gets a reference to the first item of the window.
        /// </summary>
        /// <returns>The reference to the first item of the window</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref float GetStartReference()
        {
            Span<float> span = this.buffer.GetSpan();
            return ref span[this.flatStartIndex];
        }

        /// <summary>
        /// Gets the span representing the portion of the <see cref="WeightsBuffer"/> that this window covers
        /// </summary>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<float> GetWindowSpan() => this.buffer.GetSpan().Slice(this.flatStartIndex, this.Length);

        /// <summary>
        /// Computes the sum of vectors in 'rowSpan' weighted by weight values, pointed by this <see cref="WeightsWindow"/> instance.
        /// </summary>
        /// <param name="rowSpan">The input span of vectors</param>
        /// <param name="sourceX">The source row position.</param>
        /// <returns>The weighted sum</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ComputeWeightedRowSum(Span<Vector4> rowSpan, int sourceX)
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
                result += v.Premultiply() * weight;
            }

            return result;
        }

        /// <summary>
        /// Computes the sum of vectors in 'rowSpan' weighted by weight values, pointed by this <see cref="WeightsWindow"/> instance.
        /// Applies <see cref="Vector4Extensions.Expand(float)"/> to all input vectors.
        /// </summary>
        /// <param name="rowSpan">The input span of vectors</param>
        /// <param name="sourceX">The source row position.</param>
        /// <returns>The weighted sum</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ComputeExpandedWeightedRowSum(Span<Vector4> rowSpan, int sourceX)
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
                result += v.Premultiply().Expand() * weight;
            }

            return result.UnPremultiply();
        }

        /// <summary>
        /// Computes the sum of vectors in 'firstPassPixels' at a row pointed by 'x',
        /// weighted by weight values, pointed by this <see cref="WeightsWindow"/> instance.
        /// </summary>
        /// <param name="firstPassPixels">The buffer of input vectors in row first order</param>
        /// <param name="x">The row position</param>
        /// <param name="sourceY">The source column position.</param>
        /// <returns>The weighted sum</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ComputeWeightedColumnSum(Buffer2D<Vector4> firstPassPixels, int x, int sourceY)
        {
            ref float verticalValues = ref this.GetStartReference();
            int left = this.Left;

            // Destination color components
            Vector4 result = Vector4.Zero;

            for (int i = 0; i < this.Length; i++)
            {
                float yw = Unsafe.Add(ref verticalValues, i);
                int index = left + i + sourceY;
                result += firstPassPixels[x, index] * yw;
            }

            return result.UnPremultiply();
        }
    }
}