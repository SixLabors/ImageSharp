// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// A factory class for the generation of kernel buffers for linear transforms.
    /// Unlike simple resizing resampling operations the generated kernels are unique, based upon the
    /// location of individual transformed points and cannot be stored and indexed.
    /// </summary>
    /// <remarks>
    /// This class allocates a single buffer to reuse across multiple operations and
    /// by design is not thread safe.
    /// </remarks>
    /// <typeparam name="TResampler">The type of sampler.</typeparam>
    internal sealed class LinearTransformKernelFactory<TResampler> : IDisposable
        where TResampler : struct, IResampler
    {
        private readonly TResampler sampler;
        private readonly int sourceLength;
        private readonly IMemoryOwner<float> data;
        private readonly int radius;
        private bool isDisposed;

        public LinearTransformKernelFactory(
            TResampler sampler,
            int sourceSize,
            int destinationSize,
            MemoryAllocator allocator)
        {
            double scale = (double)((double)sourceSize / destinationSize);

            if (scale < 1)
            {
                scale = 1;
            }

            int radius = (int)Math.Ceiling(scale * sampler.Radius);

            this.sampler = sampler;
            this.radius = radius;
            this.sourceLength = sourceSize;

            // Since we always overwrite the buffer there is no value to
            // cleaning it.
            this.data = allocator.Allocate<float>((radius * 2) + 1);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public LinearTransformKernel BuildKernel(float center)
        {
            // Keep inside bounds.
            int radius = this.radius;
            int left = (int)Math.Ceiling(center - radius);
            if (left < 0)
            {
                left = 0;
            }

            int right = (int)Math.Floor(center + radius);
            if (right > this.sourceLength - 1)
            {
                right = this.sourceLength - 1;
            }

            var kernel = new LinearTransformKernel(this.data.GetSpan(), left, right);
            ref var valuesRef = ref MemoryMarshal.GetReference(kernel.Values);

            for (int i = kernel.Start; i <= kernel.End; i++)
            {
                Unsafe.Add(ref valuesRef, i - left) = this.sampler.GetValue(i - center);
            }

            return kernel;
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.data.Dispose();
            }
        }
    }
}
