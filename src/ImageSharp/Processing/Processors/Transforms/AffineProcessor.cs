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
        /// <param name="rectangle">The rectangle bounds</param>
        /// <returns>The <see cref="Matrix3x2"/></returns>
        protected abstract Matrix3x2 CreateProcessingMatrix(Rectangle rectangle);

        /// <summary>
        /// Creates a new target canvas to contain the results of the matrix transform.
        /// </summary>
        /// <param name="sourceRectangle">The source rectangle.</param>
        protected virtual void CreateNewCanvas(Rectangle sourceRectangle)
        {
            this.ResizeRectangle = Matrix3x2.Invert(this.CreateProcessingMatrix(sourceRectangle), out Matrix3x2 sizeMatrix)
                ? ImageMaths.GetBoundingRectangle(sourceRectangle, sizeMatrix)
                : sourceRectangle;

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
            Rectangle sourceBounds = source.Bounds();
            Matrix3x2 matrix = this.GetCenteredMatrix(source);

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
                            var point = Point.Transform(new Point(x, y), matrix);
                            if (sourceBounds.Contains(point.X, point.Y))
                            {
                                destRow[x] = source[point.X, point.Y];
                            }
                        }
                    });

                return;
            }

            int maxSourceX = source.Width - 1;
            int maxSourceY = source.Height - 1;
            (float radius, float scale) xRadiusScale = this.GetSamplingRadius(source.Width, destination.Width);
            (float radius, float scale) yRadiusScale = this.GetSamplingRadius(source.Height, destination.Height);
            float xScale = xRadiusScale.scale;
            float yScale = yRadiusScale.scale;
            float xRadius = xRadiusScale.radius;
            float yRadius = yRadiusScale.radius;

            Parallel.For(
                0,
                height,
                configuration.ParallelOptions,
                y =>
                {
                    Span<TPixel> destRow = destination.GetPixelRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        // Use the single precision position to calculate correct bounding pixels
                        // otherwise we get rogue pixels outside of the bounds.
                        var point = PointF.Transform(new PointF(x, y), matrix);
                        int maxX = (int)MathF.Ceiling(point.X + xRadius);
                        int maxY = (int)MathF.Ceiling(point.Y + yRadius);
                        int minX = (int)MathF.Floor(point.X - xRadius);
                        int minY = (int)MathF.Floor(point.Y - yRadius);

                        // Clamp sampling pixels to the source image edge
                        maxX = maxX.Clamp(0, maxSourceX);
                        minX = minX.Clamp(0, maxSourceX);
                        maxY = maxY.Clamp(0, maxSourceY);
                        minY = minY.Clamp(0, maxSourceY);

                        if (minX == maxX || minY == maxY)
                        {
                            continue;
                        }

                        // It appears these have to be calculated manually.
                        // Using the precalculated weights give the wrong values.
                        // TODO: Find a way to speed this up.
                        Vector4 sum = Vector4.Zero;
                        for (int i = minX; i <= maxX; i++)
                        {
                            float weightX = this.Sampler.GetValue((i - point.X) / xScale);
                            for (int j = minY; j <= maxY; j++)
                            {
                                float weightY = this.Sampler.GetValue((j - point.Y) / yScale);
                                sum += source[i, j].ToVector4() * weightX * weightY;
                            }
                        }
                        ref TPixel dest = ref destRow[x];
                        dest.PackFromVector4(sum);
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
            return (translationToTargetCenter * this.CreateProcessingMatrix(this.ResizeRectangle)) * translateToSourceCenter;
        }

        /// <summary>
        /// Calculates the sampling radius for the current sampler
        /// </summary>
        /// <param name="sourceSize">The source dimension size</param>
        /// <param name="destinationSize">The destination dimension size</param>
        /// <returns>The radius, and scaling factor</returns>
        private (float radius, float scale) GetSamplingRadius(int sourceSize, int destinationSize)
        {
            float ratio = (float)sourceSize / destinationSize;
            float scale = ratio;

            if (scale < 1F)
            {
                scale = 1F;
            }

            return (MathF.Ceiling(scale * this.Sampler.Radius), scale);
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