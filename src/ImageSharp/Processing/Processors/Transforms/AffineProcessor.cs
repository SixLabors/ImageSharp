// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides the base methods to perform affine transforms on an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class AffineProcessor<TPixel> : ResamplingWeightedProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AffineProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the resize operation.</param>
        protected AffineProcessor(IResampler sampler)
            : base(sampler, 1, 1, Rectangles.DefaultRectangle) // Hack to prevent Guard throwing in base, we always set the canvas
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether to expand the canvas to fit the transformed image.
        /// </summary>
        public bool Expand { get; set; } = true;

        /// <summary>
        /// Returns the processing matrix used for transforming the image.
        /// </summary>
        /// <returns>The <see cref="Matrix3x2"/></returns>
        protected abstract Matrix3x2 CreateProcessingMatrix();

        /// <summary>
        /// Creates a new target canvas to contain the results of the matrix transform.
        /// </summary>
        /// <param name="sourceRectangle">The source rectangle.</param>
        protected virtual void CreateNewCanvas(Rectangle sourceRectangle)
        {
            if (this.ResizeRectangle == Rectangles.DefaultRectangle)
            {
                if (this.Expand)
                {
                    this.ResizeRectangle = Matrix3x2.Invert(this.CreateProcessingMatrix(), out Matrix3x2 sizeMatrix)
                        ? ImageMaths.GetBoundingRectangle(sourceRectangle, sizeMatrix)
                        : sourceRectangle;
                }
                else
                {
                    this.ResizeRectangle = sourceRectangle;
                }
            }

            this.Width = this.ResizeRectangle.Width;
            this.Height = this.ResizeRectangle.Height;
        }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            this.CreateNewCanvas(sourceRectangle);

            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames =
                source.Frames.Select(x => new ImageFrame<TPixel>(this.ResizeRectangle.Width, this.ResizeRectangle.Height, x.MetaData.Clone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(source.GetConfiguration(), source.MetaData.Clone(), frames);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
            int height = this.ResizeRectangle.Height;
            int width = this.ResizeRectangle.Width;
            Matrix3x2 matrix = this.GetCenteredMatrix(source);
            Rectangle sourceBounds = source.Bounds();

            if (this.Sampler is NearestNeighborResampler)
            {
                Parallel.For(
                    0,
                    height,
                    configuration.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> destRow = destination.GetPixelRowSpan(y);

                        for (int x = 0; x < width; x++)
                        {
                            var transformedPoint = Point.Transform(new Point(x, y), matrix);
                            if (sourceBounds.Contains(transformedPoint.X, transformedPoint.Y))
                            {
                                destRow[x] = source[transformedPoint.X, transformedPoint.Y];
                            }
                        }
                    });

                return;
            }

            int maxX = source.Width - 1;
            int maxY = source.Height - 1;
            int radius = Math.Max((int)this.Sampler.Radius, 1);

            Parallel.For(
                0,
                height,
                configuration.ParallelOptions,
                y =>
                {
                    Span<TPixel> destRow = destination.GetPixelRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        var transformedPoint = Point.Transform(new Point(x, y), matrix);
                        if (sourceBounds.Contains(transformedPoint.X, transformedPoint.Y))
                        {
                            WeightsWindow windowX = this.HorizontalWeights.Weights[transformedPoint.X];
                            WeightsWindow windowY = this.VerticalWeights.Weights[transformedPoint.Y];

                            Vector4 dXY = this.ComputeWeightedSumAtPosition(source, maxX, maxY, radius, ref windowX, ref windowY, ref transformedPoint);
                            ref TPixel dest = ref destRow[x];
                            dest.PackFromVector4(dXY);
                        }
                    }
                });
        }

        /// <summary>
        /// Gets a transform matrix adjusted to center upon the target image bounds.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <returns>
        /// The <see cref="Matrix3x2"/>.
        /// </returns>
        protected Matrix3x2 GetCenteredMatrix(ImageFrame<TPixel> source)
        {
            var translationToTargetCenter = Matrix3x2.CreateTranslation(-this.ResizeRectangle.Width * .5F, -this.ResizeRectangle.Height * .5F);
            var translateToSourceCenter = Matrix3x2.CreateTranslation(source.Width * .5F, source.Height * .5F);
            return (translationToTargetCenter * this.CreateProcessingMatrix()) * translateToSourceCenter;
        }

        /// <summary>
        /// Computes the weighted sum at the given XY position
        /// </summary>
        /// <param name="source">The source image</param>
        /// <param name="maxX">The maximum x value</param>
        /// <param name="maxY">The maximum y value</param>
        /// <param name="radius">The radius of the current sampling window</param>
        /// <param name="windowX">The horizontal weights</param>
        /// <param name="windowY">The vertical weights</param>
        /// <param name="point">The transformed position</param>
        /// <returns>The <see cref="Vector4"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector4 ComputeWeightedSumAtPosition(ImageFrame<TPixel> source, int maxX, int maxY, int radius, ref WeightsWindow windowX, ref WeightsWindow windowY, ref Point point)
        {
            ref float horizontalValues = ref windowX.GetStartReference();
            ref float verticalValues = ref windowY.GetStartReference();

            int left = point.X - radius;
            int right = point.X + radius;
            int top = point.Y - radius;
            int bottom = point.Y + radius;

            // Faster than clamping + we know we are only looking in one direction
            if (left < 0)
            {
                left = 0;
            }

            if (top < 0)
            {
                top = 0;
            }

            if (right > maxX)
            {
                right = maxX;
            }

            if (bottom > maxY)
            {
                bottom = maxY;
            }

            Vector4 result = Vector4.Zero;

            // We calculate our sample by iterating up-down/left-right from our transformed point.
            // Ignoring the weight of outlying pixels is better for shape preservation on transforms such as skew with samplers that use larger radii.
            // We don't offset our window index so that the weight compensates for the missing values
            for (int y = top, yl = 0; y <= bottom; y++, yl++)
            {
                float yweight = Unsafe.Add(ref verticalValues, yl);

                for (int x = left, xl = 0; x <= right; x++, xl++)
                {
                    float xweight = Unsafe.Add(ref horizontalValues, xl);
                    float weight = yweight * xweight;

                    result += source[x, y].ToVector4() * weight;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Contains a static rectangle used for comparison when creating a new canvas.
    /// We do this so we can inherit from the resampling weights class and pass the guard in the constructor and also avoid creating a new rectangle each time.
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleType", Justification = "I'm using this only here to prevent duplication in generic types.")]
    internal static class Rectangles
    {
        public static Rectangle DefaultRectangle { get; } = new Rectangle(0, 0, 1, 1);
    }
}