// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    internal sealed class OpaqueProcessor<TPixel> : ImageProcessor<TPixel>
          where TPixel : unmanaged, IPixel<TPixel>
    {
        public OpaqueProcessor(
            Configuration configuration,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
        }

        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            var operation = new OpaqueRowOperation(this.Configuration, source, interest);
            ParallelRowIterator.IterateRows<OpaqueRowOperation, Vector4>(this.Configuration, interest, in operation);
        }

        private readonly struct OpaqueRowOperation : IRowOperation<Vector4>
        {
            private readonly Configuration configuration;
            private readonly ImageFrame<TPixel> target;
            private readonly Rectangle bounds;

            [MethodImpl(InliningOptions.ShortMethod)]
            public OpaqueRowOperation(
                Configuration configuration,
                ImageFrame<TPixel> target,
                Rectangle bounds)
            {
                this.configuration = configuration;
                this.target = target;
                this.bounds = bounds;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y, Span<Vector4> span)
            {
                Span<TPixel> targetRowSpan = this.target.GetPixelRowSpan(y).Slice(this.bounds.X);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, targetRowSpan.Slice(0, span.Length), span, PixelConversionModifiers.Scale);
                ref Vector4 baseRef = ref MemoryMarshal.GetReference(span);

                for (int x = 0; x < this.bounds.Width; x++)
                {
                    ref Vector4 v = ref Unsafe.Add(ref baseRef, x);
                    v.W = 1F;
                }

                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, span, targetRowSpan, PixelConversionModifiers.Scale);
            }
        }
    }
}
