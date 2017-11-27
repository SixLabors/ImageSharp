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

            Parallel.For(
                0,
                height,
                new ParallelOptions { MaxDegreeOfParallelism = 1 },
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

                            Vector4 dXY = this.ComputeWeightedSumAtPosition(source, maxX, maxY, ref windowX, ref windowY, ref transformedPoint);
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
            return (translationToTargetCenter * this.CreateProcessingMatrix(this.ResizeRectangle)) * translateToSourceCenter;
        }

        /// <summary>
        /// Computes the weighted sum at the given XY position
        /// </summary>
        /// <param name="source">The source image</param>
        /// <param name="maxX">The maximum x value</param>
        /// <param name="maxY">The maximum y value</param>
        /// <param name="windowX">The horizontal weights</param>
        /// <param name="windowY">The vertical weights</param>
        /// <param name="point">The transformed position</param>
        /// <returns>The <see cref="Vector4"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector4 ComputeWeightedSumAtPosition(
            ImageFrame<TPixel> source,
            int maxX,
            int maxY,
            ref WeightsWindow windowX,
            ref WeightsWindow windowY,
            ref Point point)
        {
            // What, in theory, is supposed to happen here is the following...
            //
            // We identify the maximum possible pixel offsets allowable by the current sampler
            // clamping values to ensure that we do not go outwith the bounds of our image.
            //
            // Then we get the weights of that offset value from our pre-calculated vaues.
            // First we grab the weight on the y-axis, then the x-axis and then we multiply
            // them together to get the final weight.
            //
            // Unfortunately this simply does not seem to work!
            // The output is rubbish and I cannot see why :(
            ref float horizontalValues = ref windowX.GetStartReference();
            ref float verticalValues = ref windowY.GetStartReference();

            int yLength = windowY.Length;
            int xLength = windowX.Length;
            int yRadius = (int)MathF.Ceiling((yLength - 1) * .5F);
            int xRadius = (int)MathF.Ceiling((xLength - 1) * .5F);

            int left = point.X - xRadius;
            int right = point.X + xRadius;
            int top = point.Y - yRadius;
            int bottom = point.Y + yRadius;

            int yIndex = 0;
            int xIndex = 0;

            // Faster than clamping + we know we are only looking in one direction
            if (left < 0)
            {
                // Trim the length of our weights iterator across the x-axis.
                // Offset our start index across the x-axis.
                xIndex = ImageMaths.FastAbs(left);
                xLength -= xIndex;
                left = 0;
            }

            if (top < 0)
            {
                // Trim the length of our weights iterator across the y-axis.
                // Offset our start index across the y-axis.
                yIndex = ImageMaths.FastAbs(top);
                yLength -= yIndex;
                top = 0;
            }

            if (right >= maxX)
            {
                // Trim the length of our weights iterator across the x-axis.
                xLength -= right - maxX;
            }

            if (bottom >= maxY)
            {
                // Trim the length of our weights iterator across the y-axis.
                yLength -= bottom - maxY;
            }

            Vector4 result = Vector4.Zero;

            // We calculate our sample by iterating up-down/left-right from our transformed point.
            for (int y = top, yi = yIndex; yi < yLength; y++, yi++)
            {
                float yweight = Unsafe.Add(ref verticalValues, yi);

                for (int x = left, xi = xIndex; xi < xLength; x++, xi++)
                {
                    float xweight = Unsafe.Add(ref horizontalValues, xi);
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