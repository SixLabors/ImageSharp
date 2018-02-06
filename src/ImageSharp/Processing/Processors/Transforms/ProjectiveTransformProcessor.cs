// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides the base methods to perform non-affine transforms on an image.
    /// TODO: Doesn't work yet! Implement tests + Finish implementation + Document Matrix4x4 behavior
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ProjectiveTransformProcessor<TPixel> : InterpolatedTransformProcessorBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        // TODO: We should use a Size instead! (See AffineTransformProcessor<T>)
        private Rectangle targetRectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        public ProjectiveTransformProcessor(Matrix4x4 matrix)
           : this(matrix, KnownResamplers.Bicubic)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        public ProjectiveTransformProcessor(Matrix4x4 matrix, IResampler sampler)
           : this(matrix, sampler, Rectangle.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        /// <param name="rectangle">The rectangle to constrain the transformed image to.</param>
        public ProjectiveTransformProcessor(Matrix4x4 matrix, IResampler sampler, Rectangle rectangle)
            : base(sampler)
        {
            // Transforms are inverted else the output is the opposite of the expected.
            Matrix4x4.Invert(matrix, out matrix);
            this.TransformMatrix = matrix;
            this.targetRectangle = rectangle;
        }

        /// <summary>
        /// Gets the matrix used to supply the non-affine transform
        /// </summary>
        public Matrix4x4 TransformMatrix { get; }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            if (this.targetRectangle == Rectangle.Empty)
            {
                this.targetRectangle = this.GetTransformedBoundingRectangle(sourceRectangle, this.TransformMatrix);
            }

            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames =
                source.Frames.Select(x => new ImageFrame<TPixel>(this.targetRectangle.Width, this.targetRectangle.Height, x.MetaData.Clone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(source.GetConfiguration(), source.MetaData.Clone(), frames);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
            int height = this.targetRectangle.Height;
            int width = this.targetRectangle.Width;
            Rectangle sourceBounds = source.Bounds();

            // Since could potentially be resizing the canvas we might need to re-calculate the matrix
            Matrix4x4 matrix = this.GetProcessingMatrix(sourceBounds, this.targetRectangle);

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
                            var point = Point.Round(Vector2.Transform(new Vector2(x, y), matrix));
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
            (float radius, float scale, float ratio) xRadiusScale = this.GetSamplingRadius(source.Width, destination.Width);
            (float radius, float scale, float ratio) yRadiusScale = this.GetSamplingRadius(source.Height, destination.Height);
            float xScale = xRadiusScale.scale;
            float yScale = yRadiusScale.scale;
            var radius = new Vector2(xRadiusScale.radius, yRadiusScale.radius);
            IResampler sampler = this.Sampler;
            var maxSource = new Vector4(maxSourceX, maxSourceY, maxSourceX, maxSourceY);
            int xLength = (int)MathF.Ceiling((radius.X * 2) + 2);
            int yLength = (int)MathF.Ceiling((radius.Y * 2) + 2);

            using (var yBuffer = new Buffer2D<float>(yLength, height))
            using (var xBuffer = new Buffer2D<float>(xLength, height))
            {
                Parallel.For(
                    0,
                    height,
                    configuration.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> destRow = destination.GetPixelRowSpan(y);
                        Span<float> ySpan = yBuffer.GetRowSpan(y);
                        Span<float> xSpan = xBuffer.GetRowSpan(y);

                        for (int x = 0; x < width; x++)
                        {
                            // Use the single precision position to calculate correct bounding pixels
                            // otherwise we get rogue pixels outside of the bounds.
                            var point = Vector2.Transform(new Vector2(x, y), matrix);

                            // Clamp sampling pixel radial extents to the source image edges
                            Vector2 maxXY = point + radius;
                            Vector2 minXY = point - radius;

                            // max, maxY, minX, minY
                            var extents = new Vector4(
                                MathF.Floor(maxXY.X + .5F),
                                MathF.Floor(maxXY.Y + .5F),
                                MathF.Ceiling(minXY.X - .5F),
                                MathF.Ceiling(minXY.Y - .5F));

                            int right = (int)extents.X;
                            int bottom = (int)extents.Y;
                            int left = (int)extents.Z;
                            int top = (int)extents.W;

                            extents = Vector4.Clamp(extents, Vector4.Zero, maxSource);

                            int maxX = (int)extents.X;
                            int maxY = (int)extents.Y;
                            int minX = (int)extents.Z;
                            int minY = (int)extents.W;

                            if (minX == maxX || minY == maxY)
                            {
                                continue;
                            }

                            // It appears these have to be calculated on-the-fly.
                            // Precalulating transformed weights would require prior knowledge of every transformed pixel location
                            // since they can be at sub-pixel positions on both axis.
                            // I've optimized where I can but am always open to suggestions.
                            if (yScale > 1 && xScale > 1)
                            {
                                CalculateWeightsDown(top, bottom, minY, maxY, point.Y, sampler, yScale, ySpan);
                                CalculateWeightsDown(left, right, minX, maxX, point.X, sampler, xScale, xSpan);
                            }
                            else
                            {
                                CalculateWeightsScaleUp(minY, maxY, point.Y, sampler, ySpan);
                                CalculateWeightsScaleUp(minX, maxX, point.X, sampler, xSpan);
                            }

                            // Now multiply the results against the offsets
                            Vector4 sum = Vector4.Zero;
                            for (int yy = 0, j = minY; j <= maxY; j++, yy++)
                            {
                                float yWeight = ySpan[yy];

                                for (int xx = 0, i = minX; i <= maxX; i++, xx++)
                                {
                                    float xWeight = xSpan[xx];
                                    var vector = source[i, j].ToVector4();

                                    // Values are first premultiplied to prevent darkening of edge pixels
                                    Vector4 mupltiplied = vector.Premultiply();
                                    sum += mupltiplied * xWeight * yWeight;
                                }
                            }

                            ref TPixel dest = ref destRow[x];

                            // Reverse the premultiplication
                            dest.PackFromVector4(sum.UnPremultiply());
                        }
                    });
            }
        }

        /// <summary>
        /// Gets a transform matrix adjusted for final processing based upon the target image bounds.
        /// </summary>
        /// <param name="sourceRectangle">The source image bounds.</param>
        /// <param name="destinationRectangle">The destination image bounds.</param>
        /// <returns>
        /// The <see cref="Matrix4x4"/>.
        /// </returns>
        protected virtual Matrix4x4 GetProcessingMatrix(Rectangle sourceRectangle, Rectangle destinationRectangle)
        {
            return this.TransformMatrix;
        }

        /// <summary>
        /// Gets the bounding <see cref="Rectangle"/> relative to the source for the given transformation matrix.
        /// </summary>
        /// <param name="sourceRectangle">The source rectangle.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>The <see cref="Rectangle"/></returns>
        protected virtual Rectangle GetTransformedBoundingRectangle(Rectangle sourceRectangle, Matrix4x4 matrix)
        {
            return sourceRectangle;
        }
    }
}