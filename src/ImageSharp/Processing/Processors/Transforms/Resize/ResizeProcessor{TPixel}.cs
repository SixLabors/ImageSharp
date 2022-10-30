// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Implements resizing of images using various resamplers.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class ResizeProcessor<TPixel> : TransformProcessor<TPixel>, IResamplingTransformImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ResizeOptions options;
    private readonly int destinationWidth;
    private readonly int destinationHeight;
    private readonly IResampler resampler;
    private readonly Rectangle destinationRectangle;
    private Image<TPixel> destination;

    public ResizeProcessor(Configuration configuration, ResizeProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        this.destinationWidth = definition.DestinationWidth;
        this.destinationHeight = definition.DestinationHeight;
        this.destinationRectangle = definition.DestinationRectangle;
        this.options = definition.Options;
        this.resampler = definition.Options.Sampler;
    }

    /// <inheritdoc/>
    protected override Size GetDestinationSize() => new(this.destinationWidth, this.destinationHeight);

    /// <inheritdoc/>
    protected override void BeforeImageApply(Image<TPixel> destination)
    {
        this.destination = destination;
        this.resampler.ApplyTransform(this);

        base.BeforeImageApply(destination);
    }

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
    {
        // Everything happens in BeforeImageApply.
    }

    public void ApplyTransform<TResampler>(in TResampler sampler)
        where TResampler : struct, IResampler
    {
        Configuration configuration = this.Configuration;
        Image<TPixel> source = this.Source;
        Image<TPixel> destination = this.destination;
        Rectangle sourceRectangle = this.SourceRectangle;
        Rectangle destinationRectangle = this.destinationRectangle;
        bool compand = this.options.Compand;
        bool premultiplyAlpha = this.options.PremultiplyAlpha;
        TPixel fillColor = this.options.PadColor.ToPixel<TPixel>();
        bool shouldFill = (this.options.Mode == ResizeMode.BoxPad || this.options.Mode == ResizeMode.Pad)
                          && this.options.PadColor != default;

        // Handle resize dimensions identical to the original
        if (source.Width == destination.Width
            && source.Height == destination.Height
            && sourceRectangle == destinationRectangle)
        {
            for (int i = 0; i < source.Frames.Count; i++)
            {
                ImageFrame<TPixel> sourceFrame = source.Frames[i];
                ImageFrame<TPixel> destinationFrame = destination.Frames[i];

                // The cloned will be blank here copy all the pixel data over
                sourceFrame.GetPixelMemoryGroup().CopyTo(destinationFrame.GetPixelMemoryGroup());
            }

            return;
        }

        var interest = Rectangle.Intersect(destinationRectangle, destination.Bounds());

        if (sampler is NearestNeighborResampler)
        {
            for (int i = 0; i < source.Frames.Count; i++)
            {
                ImageFrame<TPixel> sourceFrame = source.Frames[i];
                ImageFrame<TPixel> destinationFrame = destination.Frames[i];

                if (shouldFill)
                {
                    destinationFrame.Clear(fillColor);
                }

                ApplyNNResizeFrameTransform(
                    configuration,
                    sourceFrame,
                    destinationFrame,
                    sourceRectangle,
                    destinationRectangle,
                    interest);
            }

            return;
        }

        // Since all image frame dimensions have to be the same we can calculate
        // the kernel maps and reuse for all frames.
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using var horizontalKernelMap = ResizeKernelMap.Calculate(
            in sampler,
            destinationRectangle.Width,
            sourceRectangle.Width,
            allocator);

        using var verticalKernelMap = ResizeKernelMap.Calculate(
            in sampler,
            destinationRectangle.Height,
            sourceRectangle.Height,
            allocator);

        for (int i = 0; i < source.Frames.Count; i++)
        {
            ImageFrame<TPixel> sourceFrame = source.Frames[i];
            ImageFrame<TPixel> destinationFrame = destination.Frames[i];

            if (shouldFill)
            {
                destinationFrame.Clear(fillColor);
            }

            ApplyResizeFrameTransform(
                configuration,
                sourceFrame,
                destinationFrame,
                horizontalKernelMap,
                verticalKernelMap,
                sourceRectangle,
                destinationRectangle,
                interest,
                compand,
                premultiplyAlpha);
        }
    }

    private static void ApplyNNResizeFrameTransform(
        Configuration configuration,
        ImageFrame<TPixel> source,
        ImageFrame<TPixel> destination,
        Rectangle sourceRectangle,
        Rectangle destinationRectangle,
        Rectangle interest)
    {
        // Scaling factors
        float widthFactor = sourceRectangle.Width / (float)destinationRectangle.Width;
        float heightFactor = sourceRectangle.Height / (float)destinationRectangle.Height;

        var operation = new NNRowOperation(
            sourceRectangle,
            destinationRectangle,
            interest,
            widthFactor,
            heightFactor,
            source.PixelBuffer,
            destination.PixelBuffer);

        ParallelRowIterator.IterateRows(
            configuration,
            interest,
            in operation);
    }

    private static PixelConversionModifiers GetModifiers(bool compand, bool premultiplyAlpha)
    {
        if (premultiplyAlpha)
        {
            return PixelConversionModifiers.Premultiply.ApplyCompanding(compand);
        }
        else
        {
            return PixelConversionModifiers.None.ApplyCompanding(compand);
        }
    }

    private static void ApplyResizeFrameTransform(
        Configuration configuration,
        ImageFrame<TPixel> source,
        ImageFrame<TPixel> destination,
        ResizeKernelMap horizontalKernelMap,
        ResizeKernelMap verticalKernelMap,
        Rectangle sourceRectangle,
        Rectangle destinationRectangle,
        Rectangle interest,
        bool compand,
        bool premultiplyAlpha)
    {
        PixelAlphaRepresentation? alphaRepresentation = PixelOperations<TPixel>.Instance.GetPixelTypeInfo()?.AlphaRepresentation;

        // Premultiply only if alpha representation is unknown or Unassociated:
        bool needsPremultiplication = alphaRepresentation == null || alphaRepresentation.Value == PixelAlphaRepresentation.Unassociated;
        premultiplyAlpha &= needsPremultiplication;
        PixelConversionModifiers conversionModifiers = GetModifiers(compand, premultiplyAlpha);

        Buffer2DRegion<TPixel> sourceRegion = source.PixelBuffer.GetRegion(sourceRectangle);

        // To reintroduce parallel processing, we would launch multiple workers
        // for different row intervals of the image.
        using var worker = new ResizeWorker<TPixel>(
            configuration,
            sourceRegion,
            conversionModifiers,
            horizontalKernelMap,
            verticalKernelMap,
            interest,
            destinationRectangle.Location);
        worker.Initialize();

        var workingInterval = new RowInterval(interest.Top, interest.Bottom);
        worker.FillDestinationPixels(workingInterval, destination.PixelBuffer);
    }

    private readonly struct NNRowOperation : IRowOperation
    {
        private readonly Rectangle sourceBounds;
        private readonly Rectangle destinationBounds;
        private readonly Rectangle interest;
        private readonly float widthFactor;
        private readonly float heightFactor;
        private readonly Buffer2D<TPixel> source;
        private readonly Buffer2D<TPixel> destination;

        [MethodImpl(InliningOptions.ShortMethod)]
        public NNRowOperation(
            Rectangle sourceBounds,
            Rectangle destinationBounds,
            Rectangle interest,
            float widthFactor,
            float heightFactor,
            Buffer2D<TPixel> source,
            Buffer2D<TPixel> destination)
        {
            this.sourceBounds = sourceBounds;
            this.destinationBounds = destinationBounds;
            this.interest = interest;
            this.widthFactor = widthFactor;
            this.heightFactor = heightFactor;
            this.source = source;
            this.destination = destination;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y)
        {
            int sourceX = this.sourceBounds.X;
            int sourceY = this.sourceBounds.Y;
            int destOriginX = this.destinationBounds.X;
            int destOriginY = this.destinationBounds.Y;
            int destLeft = this.interest.Left;
            int destRight = this.interest.Right;

            // Y coordinates of source points
            Span<TPixel> sourceRow = this.source.DangerousGetRowSpan((int)(((y - destOriginY) * this.heightFactor) + sourceY));
            Span<TPixel> targetRow = this.destination.DangerousGetRowSpan(y);

            for (int x = destLeft; x < destRight; x++)
            {
                // X coordinates of source points
                targetRow[x] = sourceRow[(int)(((x - destOriginX) * this.widthFactor) + sourceX)];
            }
        }
    }
}
