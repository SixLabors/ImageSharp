// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Provides the base methods to perform non-affine transforms on an image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class ProjectiveTransformProcessor<TPixel> : TransformProcessor<TPixel>, IResamplingTransformImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Size destinationSize;
    private readonly IResampler resampler;
    private readonly Matrix4x4 transformMatrix;
    private ImageFrame<TPixel>? source;
    private ImageFrame<TPixel>? destination;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectiveTransformProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="definition">The <see cref="ProjectiveTransformProcessor"/> defining the processor parameters.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public ProjectiveTransformProcessor(Configuration configuration, ProjectiveTransformProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        this.destinationSize = definition.DestinationSize;
        this.transformMatrix = definition.TransformMatrix;
        this.resampler = definition.Sampler;
    }

    protected override Size GetDestinationSize() => this.destinationSize;

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
    {
        this.source = source;
        this.destination = destination;
        this.resampler.ApplyTransform(this);
    }

    /// <inheritdoc/>
    protected override Matrix4x4 GetTransformMatrix() => this.transformMatrix;

    /// <inheritdoc/>
    public void ApplyTransform<TResampler>(in TResampler sampler)
        where TResampler : struct, IResampler
    {
        Configuration configuration = this.Configuration;
        ImageFrame<TPixel> source = this.source!;
        ImageFrame<TPixel> destination = this.destination!;
        Matrix4x4 matrix = this.transformMatrix;

        // Handle transforms that result in output identical to the original.
        // Degenerate matrices are already handled in the upstream definition.
        if (matrix.Equals(Matrix4x4.Identity))
        {
            // The clone will be blank here copy all the pixel data over
            Rectangle interest = Rectangle.Intersect(this.SourceRectangle, destination.Bounds);
            Buffer2DRegion<TPixel> sourceBuffer = source.PixelBuffer.GetRegion(interest);
            Buffer2DRegion<TPixel> destinationBuffer = destination.PixelBuffer.GetRegion(interest);
            for (int y = 0; y < sourceBuffer.Height; y++)
            {
                sourceBuffer.DangerousGetRowSpan(y).CopyTo(destinationBuffer.DangerousGetRowSpan(y));
            }

            return;
        }

        // Convert from screen to world space.
        Matrix4x4.Invert(matrix, out matrix);

        if (sampler is NearestNeighborResampler)
        {
            NNProjectiveOperation nnOperation = new(
                source.PixelBuffer,
                Rectangle.Intersect(this.SourceRectangle, source.Bounds),
                destination.PixelBuffer,
                matrix);

            ParallelRowIterator.IterateRows(
                configuration,
                destination.Bounds,
                in nnOperation);

            return;
        }

        ProjectiveOperation<TResampler> operation = new(
            configuration,
            source.PixelBuffer,
            Rectangle.Intersect(this.SourceRectangle, source.Bounds),
            destination.PixelBuffer,
            in sampler,
            matrix);

        ParallelRowIterator.IterateRowIntervals<ProjectiveOperation<TResampler>, Vector4>(
            configuration,
            destination.Bounds,
            in operation);
    }

    private readonly struct NNProjectiveOperation : IRowOperation
    {
        private readonly Buffer2D<TPixel> source;
        private readonly Buffer2D<TPixel> destination;
        private readonly Rectangle bounds;
        private readonly Matrix4x4 matrix;

        [MethodImpl(InliningOptions.ShortMethod)]
        public NNProjectiveOperation(
            Buffer2D<TPixel> source,
            Rectangle bounds,
            Buffer2D<TPixel> destination,
            Matrix4x4 matrix)
        {
            this.source = source;
            this.bounds = bounds;
            this.destination = destination;
            this.matrix = matrix;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y)
        {
            Span<TPixel> destinationRowSpan = this.destination.DangerousGetRowSpan(y);

            for (int x = 0; x < destinationRowSpan.Length; x++)
            {
                Vector2 point = TransformUtils.ProjectiveTransform2D(x, y, this.matrix);
                int px = (int)MathF.Round(point.X);
                int py = (int)MathF.Round(point.Y);

                if (this.bounds.Contains(px, py))
                {
                    destinationRowSpan[x] = this.source.GetElementUnsafe(px, py);
                }
            }
        }
    }

    private readonly struct ProjectiveOperation<TResampler> : IRowIntervalOperation<Vector4>
        where TResampler : struct, IResampler
    {
        private readonly Configuration configuration;
        private readonly Buffer2D<TPixel> source;
        private readonly Rectangle bounds;
        private readonly Buffer2D<TPixel> destination;
        private readonly TResampler sampler;
        private readonly Matrix4x4 matrix;
        private readonly float yRadius;
        private readonly float xRadius;

        [MethodImpl(InliningOptions.ShortMethod)]
        public ProjectiveOperation(
            Configuration configuration,
            Buffer2D<TPixel> source,
            Rectangle bounds,
            Buffer2D<TPixel> destination,
            in TResampler sampler,
            Matrix4x4 matrix)
        {
            this.configuration = configuration;
            this.source = source;
            this.bounds = bounds;
            this.destination = destination;
            this.sampler = sampler;
            this.matrix = matrix;

            this.yRadius = LinearTransformUtility.GetSamplingRadius(in sampler, bounds.Height, destination.Height);
            this.xRadius = LinearTransformUtility.GetSamplingRadius(in sampler, bounds.Width, destination.Width);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetRequiredBufferLength(Rectangle bounds)
            => bounds.Width;

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(in RowInterval rows, Span<Vector4> span)
        {
            Matrix4x4 matrix = this.matrix;
            TResampler sampler = this.sampler;
            float yRadius = this.yRadius;
            float xRadius = this.xRadius;
            int minY = this.bounds.Y;
            int maxY = this.bounds.Bottom - 1;
            int minX = this.bounds.X;
            int maxX = this.bounds.Right - 1;

            for (int y = rows.Min; y < rows.Max; y++)
            {
                Span<TPixel> destinationRowSpan = this.destination.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToVector4(
                    this.configuration,
                    destinationRowSpan,
                    span,
                    PixelConversionModifiers.Scale);

                for (int x = 0; x < span.Length; x++)
                {
                    Vector2 point = TransformUtils.ProjectiveTransform2D(x, y, matrix);
                    float pY = point.Y;
                    float pX = point.X;

                    int top = LinearTransformUtility.GetRangeStart(yRadius, pY, minY, maxY);
                    int bottom = LinearTransformUtility.GetRangeEnd(yRadius, pY, minY, maxY);
                    int left = LinearTransformUtility.GetRangeStart(xRadius, pX, minX, maxX);
                    int right = LinearTransformUtility.GetRangeEnd(xRadius, pX, minX, maxX);

                    if (bottom <= top || right <= left)
                    {
                        continue;
                    }

                    Vector4 sum = Vector4.Zero;
                    for (int yK = top; yK <= bottom; yK++)
                    {
                        Span<TPixel> sourceRowSpan = this.source.DangerousGetRowSpan(yK);
                        float yWeight = sampler.GetValue(yK - pY);

                        for (int xK = left; xK <= right; xK++)
                        {
                            float xWeight = sampler.GetValue(xK - pX);

                            Vector4 current = sourceRowSpan[xK].ToScaledVector4();
                            Numerics.Premultiply(ref current);
                            sum += current * xWeight * yWeight;
                        }
                    }

                    span[x] = sum;
                }

                Numerics.UnPremultiply(span);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(
                    this.configuration,
                    span,
                    destinationRowSpan,
                    PixelConversionModifiers.Scale);
            }
        }
    }
}
