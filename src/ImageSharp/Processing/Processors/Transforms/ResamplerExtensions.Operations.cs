// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <content>
    /// Extensions for <see cref="IResampler"/>.
    /// </content>
    public static partial class ResamplerExtensions
    {
        private readonly struct NNAffineOperation<TPixel> : IRowIntervalOperation
            where TPixel : struct, IPixel<TPixel>
        {
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly Rectangle bounds;
            private readonly Matrix3x2 matrix;
            private readonly int maxX;

            [MethodImpl(InliningOptions.ShortMethod)]
            public NNAffineOperation(
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination,
                Matrix3x2 matrix)
            {
                this.source = source;
                this.destination = destination;
                this.bounds = source.Bounds();
                this.matrix = matrix;
                this.maxX = destination.Width;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> destRow = this.destination.GetPixelRowSpan(y);

                    for (int x = 0; x < this.maxX; x++)
                    {
                        var point = Vector2.Transform(new Vector2(x, y), this.matrix);
                        int px = (int)MathF.Round(point.X);
                        int py = (int)MathF.Round(point.Y);

                        if (this.bounds.Contains(px, py))
                        {
                            destRow[x] = this.source[px, py];
                        }
                    }
                }
            }
        }

        private readonly struct NNProjectiveOperation<TPixel> : IRowIntervalOperation
            where TPixel : struct, IPixel<TPixel>
        {
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly Rectangle bounds;
            private readonly Matrix4x4 matrix;
            private readonly int maxX;

            [MethodImpl(InliningOptions.ShortMethod)]
            public NNProjectiveOperation(
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination,
                Matrix4x4 matrix)
            {
                this.source = source;
                this.destination = destination;
                this.bounds = source.Bounds();
                this.matrix = matrix;
                this.maxX = destination.Width;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> destRow = this.destination.GetPixelRowSpan(y);

                    for (int x = 0; x < this.maxX; x++)
                    {
                        Vector2 point = TransformUtilities.ProjectiveTransform2D(x, y, this.matrix);
                        int px = (int)MathF.Round(point.X);
                        int py = (int)MathF.Round(point.Y);

                        if (this.bounds.Contains(px, py))
                        {
                            destRow[x] = this.source[px, py];
                        }
                    }
                }
            }
        }

        private readonly struct AffineOperation<TResampler, TPixel> : IRowIntervalOperation<Vector4>
            where TResampler : unmanaged, IResampler
            where TPixel : struct, IPixel<TPixel>
        {
            private readonly Configuration configuration;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly Buffer2D<float> yKernelBuffer;
            private readonly Buffer2D<float> xKernelBuffer;
            private readonly TResampler sampler;
            private readonly Matrix3x2 matrix;
            private readonly Vector2 radialExtents;
            private readonly Vector4 maxSourceExtents;
            private readonly int maxX;

            [MethodImpl(InliningOptions.ShortMethod)]
            public AffineOperation(
                Configuration configuration,
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination,
                Buffer2D<float> yKernelBuffer,
                Buffer2D<float> xKernelBuffer,
                in TResampler sampler,
                Matrix3x2 matrix,
                Vector2 radialExtents,
                Vector4 maxSourceExtents)
            {
                this.configuration = configuration;
                this.source = source;
                this.destination = destination;
                this.yKernelBuffer = yKernelBuffer;
                this.xKernelBuffer = xKernelBuffer;
                this.sampler = sampler;
                this.matrix = matrix;
                this.radialExtents = radialExtents;
                this.maxSourceExtents = maxSourceExtents;
                this.maxX = destination.Width;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows, Span<Vector4> span)
            {
                Buffer2D<TPixel> sourceBuffer = this.source.PixelBuffer;
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    PixelOperations<TPixel>.Instance.ToVector4(
                        this.configuration,
                        this.destination.GetPixelRowSpan(y),
                        span);

                    ref float yKernelSpanRef = ref MemoryMarshal.GetReference(this.yKernelBuffer.GetRowSpan(y));
                    ref float xKernelSpanRef = ref MemoryMarshal.GetReference(this.xKernelBuffer.GetRowSpan(y));

                    for (int x = 0; x < this.maxX; x++)
                    {
                        // Use the single precision position to calculate correct bounding pixels
                        // otherwise we get rogue pixels outside of the bounds.
                        var point = Vector2.Transform(new Vector2(x, y), this.matrix);
                        Convolve(
                            in this.sampler,
                            point,
                            sourceBuffer,
                            span,
                            x,
                            ref yKernelSpanRef,
                            ref xKernelSpanRef,
                            this.radialExtents,
                            this.maxSourceExtents);
                    }

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(
                        this.configuration,
                        span,
                        this.destination.GetPixelRowSpan(y));
                }
            }
        }

        private readonly struct ProjectiveOperation<TResampler, TPixel> : IRowIntervalOperation<Vector4>
            where TResampler : unmanaged, IResampler
            where TPixel : struct, IPixel<TPixel>
        {
            private readonly Configuration configuration;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly Buffer2D<float> yKernelBuffer;
            private readonly Buffer2D<float> xKernelBuffer;
            private readonly TResampler sampler;
            private readonly Matrix4x4 matrix;
            private readonly Vector2 radialExtents;
            private readonly Vector4 maxSourceExtents;
            private readonly int maxX;

            [MethodImpl(InliningOptions.ShortMethod)]
            public ProjectiveOperation(
                Configuration configuration,
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination,
                Buffer2D<float> yKernelBuffer,
                Buffer2D<float> xKernelBuffer,
                in TResampler sampler,
                Matrix4x4 matrix,
                Vector2 radialExtents,
                Vector4 maxSourceExtents)
            {
                this.configuration = configuration;
                this.source = source;
                this.destination = destination;
                this.yKernelBuffer = yKernelBuffer;
                this.xKernelBuffer = xKernelBuffer;
                this.sampler = sampler;
                this.matrix = matrix;
                this.radialExtents = radialExtents;
                this.maxSourceExtents = maxSourceExtents;
                this.maxX = destination.Width;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows, Span<Vector4> span)
            {
                Buffer2D<TPixel> sourceBuffer = this.source.PixelBuffer;
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    PixelOperations<TPixel>.Instance.ToVector4(
                        this.configuration,
                        this.destination.GetPixelRowSpan(y),
                        span);

                    ref float yKernelSpanRef = ref MemoryMarshal.GetReference(this.yKernelBuffer.GetRowSpan(y));
                    ref float xKernelSpanRef = ref MemoryMarshal.GetReference(this.xKernelBuffer.GetRowSpan(y));

                    for (int x = 0; x < this.maxX; x++)
                    {
                        // Use the single precision position to calculate correct bounding pixels
                        // otherwise we get rogue pixels outside of the bounds.
                        Vector2 point = TransformUtilities.ProjectiveTransform2D(x, y, this.matrix);
                        Convolve(
                            in this.sampler,
                            point,
                            sourceBuffer,
                            span,
                            x,
                            ref yKernelSpanRef,
                            ref xKernelSpanRef,
                            this.radialExtents,
                            this.maxSourceExtents);
                    }

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(
                        this.configuration,
                        span,
                        this.destination.GetPixelRowSpan(y));
                }
            }
        }
    }
}
