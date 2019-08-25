// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides the base methods to perform non-affine transforms on an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ProjectiveTransformProcessor<TPixel> : TransformProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly ProjectiveTransformProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="definition">The <see cref="ProjectiveTransformProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public ProjectiveTransformProcessor(ProjectiveTransformProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(source, sourceRectangle)
        {
            this.definition = definition;
        }

        private Size TargetDimensions => this.definition.TargetDimensions;

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination()
        {
            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames = this.Source.Frames.Select<ImageFrame<TPixel>, ImageFrame<TPixel>>(
                x => new ImageFrame<TPixel>(
                    this.Source.GetConfiguration(),
                    this.TargetDimensions.Width,
                    this.TargetDimensions.Height,
                    x.Metadata.DeepClone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(this.Source.GetConfiguration(), this.Source.Metadata.DeepClone(), frames);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        {
            Matrix4x4 transformMatrix = this.definition.TransformMatrix;

            // Handle transforms that result in output identical to the original.
            if (transformMatrix.Equals(default) || transformMatrix.Equals(Matrix4x4.Identity))
            {
                // The clone will be blank here copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return;
            }

            int width = this.TargetDimensions.Width;
            var targetBounds = new Rectangle(Point.Empty, this.TargetDimensions);

            // Convert from screen to world space.
            Matrix4x4.Invert(transformMatrix, out Matrix4x4 matrix);

            IResampler sampler = this.definition.Sampler;

            if (sampler is NearestNeighborResampler)
            {
                ParallelHelper.IterateRows(
                    targetBounds,
                    this.Configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                Span<TPixel> destRow = destination.GetPixelRowSpan(y);

                                for (int x = 0; x < width; x++)
                                {
                                    Vector2 point = TransformUtils.ProjectiveTransform2D(x, y, matrix);
                                    int px = (int)MathF.Round(point.X);
                                    int py = (int)MathF.Round(point.Y);

                                    if (this.SourceRectangle.Contains(px, py))
                                    {
                                        destRow[x] = source[px, py];
                                    }
                                }
                            }
                        });

                return;
            }

            var kernel = new TransformKernelMap(this.Configuration, source.Size(), destination.Size(), sampler);
            try
            {
                ParallelHelper.IterateRowsWithTempBuffer<Vector4>(
                    targetBounds,
                    this.Configuration,
                    (rows, vectorBuffer) =>
                        {
                            Span<Vector4> vectorSpan = vectorBuffer.Span;
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                Span<TPixel> targetRowSpan = destination.GetPixelRowSpan(y);
                                PixelOperations<TPixel>.Instance.ToVector4(this.Configuration, targetRowSpan, vectorSpan);
                                ref float ySpanRef = ref kernel.GetYStartReference(y);
                                ref float xSpanRef = ref kernel.GetXStartReference(y);

                                for (int x = 0; x < width; x++)
                                {
                                    // Use the single precision position to calculate correct bounding pixels
                                    // otherwise we get rogue pixels outside of the bounds.
                                    Vector2 point = TransformUtils.ProjectiveTransform2D(x, y, matrix);
                                    kernel.Convolve(
                                        point,
                                        x,
                                        ref ySpanRef,
                                        ref xSpanRef,
                                        source.PixelBuffer,
                                        vectorSpan);
                                }

                                PixelOperations<TPixel>.Instance.FromVector4Destructive(
                                    this.Configuration,
                                    vectorSpan,
                                    targetRowSpan);
                            }
                        });
            }
            finally
            {
                kernel.Dispose();
            }
        }
    }
}
