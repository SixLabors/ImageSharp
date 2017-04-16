namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <content>
    /// Conains the definition of <see cref="WeightsWindow"/> and <see cref="WeightsBuffer"/>.
    /// </content>
    internal abstract partial class ResamplingWeightedProcessor<TColor>
    {
        /// <summary>
        /// Points to a collection of of weights allocated in <see cref="WeightsBuffer"/>.
        /// </summary>
        internal unsafe struct WeightsWindow
        {
            /// <summary>
            /// The local left index position
            /// </summary>
            public int Left;

            /// <summary>
            /// The span of weights pointing to <see cref="WeightsBuffer"/>.
            /// </summary>
            // TODO: In the case of switching to official System.Memory and System.Buffers.Primitives this should be System.Buffers.Buffer<T> (formerly Memory<T>), because Span<T> is stack-only!
            // see: https://github.com/dotnet/corefxlab/blob/873d35ebed7264e2f9adb556f3b61bebc12395d6/docs/specs/memory.md
            public BufferSpan<float> Span;

            /// <summary>
            /// Initializes a new instance of the <see cref="WeightsWindow"/> struct.
            /// </summary>
            /// <param name="left">The local left index</param>
            /// <param name="span">The span</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal WeightsWindow(int left, BufferSpan<float> span)
            {
                this.Left = left;
                this.Span = span;
            }

            /// <summary>
            /// Gets an unsafe float* pointer to the beginning of <see cref="Span"/>.
            /// </summary>
            public ref float Ptr => ref this.Span.DangerousGetPinnableReference();

            /// <summary>
            /// Gets the lenghth of the weights window
            /// </summary>
            public int Length => this.Span.Length;

            /// <summary>
            /// Computes the sum of vectors in 'rowSpan' weighted by weight values, pointed by this <see cref="WeightsWindow"/> instance.
            /// </summary>
            /// <param name="rowSpan">The input span of vectors</param>
            /// <returns>The weighted sum</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector4 ComputeWeightedRowSum(BufferSpan<Vector4> rowSpan)
            {
                ref float horizontalValues = ref this.Ptr;
                int left = this.Left;
                ref Vector4 vecPtr = ref Unsafe.Add(ref rowSpan.DangerousGetPinnableReference(), left);

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
            /// <returns>The weighted sum</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector4 ComputeExpandedWeightedRowSum(BufferSpan<Vector4> rowSpan)
            {
                ref float horizontalValues = ref this.Ptr;
                int left = this.Left;
                ref Vector4 vecPtr = ref Unsafe.Add(ref rowSpan.DangerousGetPinnableReference(), left);

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
            /// Computes the sum of vectors in 'firstPassPixels' at a column pointed by 'x',
            /// weighted by weight values, pointed by this <see cref="WeightsWindow"/> instance.
            /// </summary>
            /// <param name="firstPassPixels">The buffer of input vectors in row first order</param>
            /// <param name="x">The column position</param>
            /// <returns>The weighted sum</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector4 ComputeWeightedColumnSum(Buffer2D<Vector4> firstPassPixels, int x)
            {
                ref float verticalValues = ref this.Ptr;
                int left = this.Left;

                // Destination color components
                Vector4 result = Vector4.Zero;

                for (int i = 0; i < this.Length; i++)
                {
                    float yw = Unsafe.Add(ref verticalValues, i);
                    int index = left + i;
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
            private Buffer2D<float> dataBuffer;

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
                BufferSpan<float> span = this.dataBuffer.GetRowSpan(destIdx).Slice(leftIdx, rightIdx - leftIdx + 1);
                return new WeightsWindow(leftIdx, span);
            }
        }
    }
}