// <copyright file="ResamplingWeightedProcessor.Weights.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using ImageSharp.Memory;

    /// <content>
    /// Conains the definition of <see cref="WeightsWindow"/> and <see cref="WeightsBuffer"/>.
    /// </content>
    internal abstract partial class ResamplingWeightedProcessor<TPixel>
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
            private readonly Buffer<float> buffer;

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
                this.buffer = buffer;
                this.Length = length;
            }

            /// <summary>
            /// Gets a reference to the first item of the window.
            /// </summary>
            /// <returns>The reference to the first item of the window</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref float GetStartReference()
            {
                return ref this.buffer[this.flatStartIndex];
            }

            /// <summary>
            /// Gets the span representing the portion of the <see cref="WeightsBuffer"/> that this window covers
            /// </summary>
            /// <returns>The <see cref="Span{T}"/></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<float> GetWindowSpan() => this.buffer.Slice(this.flatStartIndex, this.Length);

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
                ref Vector4 vecPtr = ref Unsafe.Add(ref rowSpan.DangerousGetPinnableReference(), left + sourceX);

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
                ref Vector4 vecPtr = ref Unsafe.Add(ref rowSpan.DangerousGetPinnableReference(), left + sourceX);

                // Destination color components
                Vector4 result = Vector4.Zero;

                for (int i = 0; i < this.Length; i++)
                {
                    float weight = Unsafe.Add(ref horizontalValues, i);
                    Vector4 v = Unsafe.Add(ref vecPtr, i);
                    result += v.Expand() * weight;
                }

                return result;
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

                return result;
            }
        }

        /// <summary>
        /// Holds the <see cref="WeightsWindow"/> values in an optimized contigous memory region.
        /// </summary>
        internal class WeightsBuffer : IDisposable
        {
            private readonly Buffer2D<float> dataBuffer;

            /// <summary>
            /// Initializes a new instance of the <see cref="WeightsBuffer"/> class.
            /// </summary>
            /// <param name="sourceSize">The size of the source window</param>
            /// <param name="destinationSize">The size of the destination window</param>
            public WeightsBuffer(int sourceSize, int destinationSize)
            {
                this.dataBuffer = Buffer2D<float>.CreateClean(sourceSize, destinationSize);
                this.Weights = new WeightsWindow[destinationSize];
            }

            /// <summary>
            /// Gets the calculated <see cref="Weights"/> values.
            /// </summary>
            public WeightsWindow[] Weights { get; }

            /// <summary>
            /// Disposes <see cref="WeightsBuffer"/> instance releasing it's backing buffer.
            /// </summary>
            public void Dispose()
            {
                this.dataBuffer.Dispose();
            }

            /// <summary>
            /// Slices a weights value at the given positions.
            /// </summary>
            /// <param name="destIdx">The index in destination buffer</param>
            /// <param name="leftIdx">The local left index value</param>
            /// <param name="rightIdx">The local right index value</param>
            /// <returns>The weights</returns>
            public WeightsWindow GetWeightsWindow(int destIdx, int leftIdx, int rightIdx)
            {
                return new WeightsWindow(destIdx, leftIdx, this.dataBuffer, rightIdx - leftIdx + 1);
            }
        }
    }
}