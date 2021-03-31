// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    internal sealed class LinearTransformKernelFactory<TResampler> : IDisposable
        where TResampler : struct, IResampler
    {
        private static readonly TolerantMath TolerantMath = TolerantMath.Default;
        private readonly TResampler sampler;
        private readonly double[] tempValues;
        private readonly int sourceLength;
        private readonly int destinationLength;
        private readonly double ratio;
        private readonly double scale;
        private readonly MemoryHandle pinHandle;

        private readonly Buffer2D<float> data;
        private bool isDisposed;

        public LinearTransformKernelFactory(
            MemoryAllocator memoryAllocator,
            TResampler sampler,
            int sourceSize,
            int destinationSize)
        {
            double ratio = (double)sourceSize / destinationSize;
            double scale = ratio;

            if (scale < 1)
            {
                scale = 1;
            }

            int radius = (int)TolerantMath.Ceiling(scale * sampler.Radius);

            this.sampler = sampler;
            this.ratio = ratio;
            this.scale = scale;
            this.MaxRadius = radius;
            this.sourceLength = sourceSize;
            this.destinationLength = destinationSize;
            this.MaxDiameter = (radius * 2) + 1;
            this.data = memoryAllocator.Allocate2D<float>(this.MaxDiameter, destinationSize, AllocationOptions.Clean);
            this.pinHandle = this.data.GetSingleMemory().Pin();
            this.tempValues = new double[this.MaxDiameter];
        }

        /// <summary>
        /// Gets the maximum diameter of the kernels.
        /// </summary>
        public int MaxDiameter { get; }

        /// <summary>
        /// Gets the maximum radius of the kernels.
        /// </summary>
        public int MaxRadius { get; private set; }

        public LinearTransformKernel GetKernel(int dataRowIndex, float xy)
        {
            double center = xy * this.ratio; // ((xy + .5) * this.ratio) - .5;

            // Keep inside bounds.
            int left = (int)TolerantMath.Ceiling(center - this.MaxRadius);
            if (left < 0)
            {
                left = 0;
            }

            int right = (int)TolerantMath.Floor(center + this.MaxRadius);
            if (right > this.sourceLength - 1)
            {
                right = this.sourceLength - 1;
            }

            LinearTransformKernel kernel = this.CreateKernel(dataRowIndex, left, right);

            Span<double> kernelValues = this.tempValues.AsSpan().Slice(0, kernel.Length);
            double sum = 0;

            for (int j = left; j <= right; j++)
            {
                double value = this.sampler.GetValue((float)((j - center) / this.scale));
                sum += value;

                kernelValues[j - left] = value;
            }

            // Normalize.
            if (sum > 0)
            {
                for (int j = 0; j < kernel.Length; j++)
                {
                    ref double kRef = ref kernelValues[j];
                    kRef /= sum;
                }
            }

            kernel.Fill(kernelValues);

            return kernel;
        }

        private unsafe LinearTransformKernel CreateKernel(int dataRowIndex, int left, int right)
        {
            int length = right - left + 1;
            Span<float> rowSpan = this.data.GetRowSpan(dataRowIndex);

            ref float rowReference = ref MemoryMarshal.GetReference(rowSpan);
            float* rowPtr = (float*)Unsafe.AsPointer(ref rowReference);
            return new LinearTransformKernel(rowPtr, length);
        }

        public void Dispose() => this.Dispose(true);

        private void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;

                if (disposing)
                {
                    this.pinHandle.Dispose();
                    this.data.Dispose();
                }
            }
        }
    }

    internal readonly unsafe struct LinearTransformKernel
    {
        private readonly float* bufferPtr;

        [MethodImpl(InliningOptions.ShortMethod)]
        public LinearTransformKernel(float* bufferPtr, int length)
        {
            this.bufferPtr = bufferPtr;
            this.Length = length;
        }

        public int Length
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get;
        }

        public Span<float> Values
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => new Span<float>(this.bufferPtr, this.Length);
        }

        internal void Fill(Span<double> values)
        {
            for (int i = 0; i < this.Length; i++)
            {
                this.Values[i] = (float)values[i];
            }
        }
    }
}
