// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Filters;

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
        Rectangle interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds);

        OpaqueRowOperation operation = new(this.Configuration, source.PixelBuffer, interest);
        ParallelRowIterator.IterateRows<OpaqueRowOperation, Vector4>(this.Configuration, interest, in operation);
    }

    private readonly struct OpaqueRowOperation : IRowOperation<Vector4>
    {
        private readonly Configuration configuration;
        private readonly Buffer2D<TPixel> target;
        private readonly Rectangle bounds;

        [MethodImpl(InliningOptions.ShortMethod)]
        public OpaqueRowOperation(
            Configuration configuration,
            Buffer2D<TPixel> target,
            Rectangle bounds)
        {
            this.configuration = configuration;
            this.target = target;
            this.bounds = bounds;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetRequiredBufferLength(Rectangle bounds)
            => bounds.Width;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y, Span<Vector4> span)
        {
            Span<TPixel> targetRowSpan = this.target.DangerousGetRowSpan(y)[this.bounds.X..];
            PixelOperations<TPixel>.Instance.ToVector4(this.configuration, targetRowSpan[..span.Length], span, PixelConversionModifiers.Scale);
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(span);

            for (int x = 0; x < this.bounds.Width; x++)
            {
                ref Vector4 v = ref Unsafe.Add(ref baseRef, (uint)x);
                v.W = 1F;
            }

            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, span, targetRowSpan, PixelConversionModifiers.Scale);
        }
    }
}
