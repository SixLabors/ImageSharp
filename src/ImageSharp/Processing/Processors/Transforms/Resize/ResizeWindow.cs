// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    class ResizeWindow : IDisposable
    {
        private readonly Buffer2D<Vector4> buffer;

        private readonly Configuration configuration;

        private readonly Rectangle sourceRectangle;

        private readonly PixelConversionModifiers conversionModifiers;

        private readonly ResizeKernelMap horizontalKernelMap;

        private readonly ResizeKernelMap verticalKernelMap;

        private readonly Rectangle workingRectangle;

        private readonly int startX;

        public ResizeWindow(
            Configuration configuration,
            Rectangle sourceRectangle,
            PixelConversionModifiers conversionModifiers,
            ResizeKernelMap horizontalKernelMap,
            ResizeKernelMap verticalKernelMap,
            int destWidth,
            Rectangle workingRectangle,
            int startX)
        {
            this.configuration = configuration;
            this.sourceRectangle = sourceRectangle;
            this.conversionModifiers = conversionModifiers;
            this.horizontalKernelMap = horizontalKernelMap;
            this.verticalKernelMap = verticalKernelMap;
            this.workingRectangle = workingRectangle;
            this.startX = startX;
            this.buffer = configuration.MemoryAllocator.Allocate2D<Vector4>(sourceRectangle.Height, destWidth, AllocationOptions.Clean);

            this.Top = sourceRectangle.Top;

            this.Bottom = sourceRectangle.Bottom;
        }

        public int Top { get; private set; }

        public int Bottom { get; private set; }

        public void Initialize<TPixel>(
            Buffer2D<TPixel> source,
            Span<Vector4> tempRowSpan)
            where TPixel : struct, IPixel<TPixel>
        {
            for (int y = this.sourceRectangle.Top; y < this.sourceRectangle.Bottom; y++)
            {
                Span<TPixel> sourceRow = source.GetRowSpan(y).Slice(this.sourceRectangle.X);

                PixelOperations<TPixel>.Instance.ToVector4(
                    this.configuration,
                    sourceRow,
                    tempRowSpan,
                    this.conversionModifiers);

                ref Vector4 firstPassBaseRef = ref this.buffer.Span[y - this.sourceRectangle.Y];

                for (int x = this.workingRectangle.Left; x < this.workingRectangle.Right; x++)
                {
                    ResizeKernel kernel = this.horizontalKernelMap.GetKernel(x - this.startX);
                    Unsafe.Add(ref firstPassBaseRef, x * this.sourceRectangle.Height) = kernel.Convolve(tempRowSpan);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Vector4> GetColumnSpan(int x)
        {
            return this.buffer.GetRowSpan(x);
        }

        public void Dispose()
        {
            this.buffer.Dispose();
        }
    }
}