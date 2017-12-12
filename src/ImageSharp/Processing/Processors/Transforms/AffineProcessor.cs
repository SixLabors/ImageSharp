// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides the base methods to perform affine transforms on an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class AffineProcessor<TPixel> : CloningImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Rectangle targetRectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="AffineProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        protected AffineProcessor(Matrix3x2 matrix, IResampler sampler)
           : this(matrix, sampler, Rectangle.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AffineProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        /// <param name="rectangle">The rectangle to constrain the transformed image to.</param>
        protected AffineProcessor(Matrix3x2 matrix, IResampler sampler, Rectangle rectangle)
        {
            // Tansforms are inverted else the output is the opposite of the expected.
            Matrix3x2.Invert(matrix, out matrix);
            this.TransformMatrix = matrix;

            this.Sampler = sampler;

            this.targetRectangle = rectangle;
        }

        /// <summary>
        /// Gets the matrix used to supply the affine transform
        /// </summary>
        public Matrix3x2 TransformMatrix { get; }

        /// <summary>
        /// Gets the sampler to perform interpolation of the transform operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            if (this.targetRectangle == Rectangle.Empty)
            {
                this.targetRectangle = Matrix3x2.Invert(this.TransformMatrix, out Matrix3x2 sizeMatrix)
                                           ? ImageMaths.GetBoundingRectangle(sourceRectangle, sizeMatrix)
                                           : sourceRectangle;
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

            // Since could potentially be resizing the canvas we need to re-center the matrix
            Matrix3x2 matrix = this.GetProcessingMatrix(sourceBounds, this.targetRectangle);

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
                                //
                                // TODO: If we can somehow improve edge pixel handling that would be most beneficial.
                                // Currently the interpolated edge non-alpha components are altered which causes slight darkening of edges.
                                // Ideally the edge pixels should represent the nearest sample with altered alpha component only.
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
                                        var mupltiplied = new Vector4(new Vector3(vector.X, vector.Y, vector.Z) * vector.W, vector.W);
                                        sum += mupltiplied * xWeight * yWeight;
                                    }
                                }

                                ref TPixel dest = ref destRow[x];
                                dest.PackFromVector4(new Vector4(new Vector3(sum.X, sum.Y, sum.Z) / sum.W, sum.W));
                            }
                        });
            }
        }

        /// <inheritdoc/>
        protected override void AfterImageApply(Image<TPixel> source, Image<TPixel> destination, Rectangle sourceRectangle)
        {
            ExifProfile profile = destination.MetaData.ExifProfile;
            if (profile == null)
            {
                return;
            }

            if (profile.GetValue(ExifTag.PixelXDimension) != null)
            {
                profile.SetValue(ExifTag.PixelXDimension, destination.Width);
            }

            if (profile.GetValue(ExifTag.PixelYDimension) != null)
            {
                profile.SetValue(ExifTag.PixelYDimension, destination.Height);
            }
        }

        /// <summary>
        /// Gets a transform matrix adjusted for final processing based upon the target image bounds.
        /// </summary>
        /// <param name="sourceRectangle">The source image bounds.</param>
        /// <param name="destinationRectangle">The destination image bounds.</param>
        /// <returns>
        /// The <see cref="Matrix3x2"/>.
        /// </returns>
        protected virtual Matrix3x2 GetProcessingMatrix(Rectangle sourceRectangle, Rectangle destinationRectangle)
        {
            return this.TransformMatrix;
        }

        /// <summary>
        /// Calculated the weights for the given point.
        /// This method uses more samples than the upscaled version to ensure edge pixels are correctly rendered.
        /// Additionally the weights are nomalized.
        /// </summary>
        /// <param name="min">The minimum sampling offset</param>
        /// <param name="max">The maximum sampling offset</param>
        /// <param name="sourceMin">The minimum source bounds</param>
        /// <param name="sourceMax">The maximum source bounds</param>
        /// <param name="point">The transformed point dimension</param>
        /// <param name="sampler">The sampler</param>
        /// <param name="scale">The transformed image scale relative to the source</param>
        /// <param name="weights">The collection of weights</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateWeightsDown(int min, int max, int sourceMin, int sourceMax, float point, IResampler sampler, float scale, Span<float> weights)
        {
            float sum = 0;
            ref float weightsBaseRef = ref weights[0];

            // Downsampling weights requires more edge sampling plus normalization of the weights
            for (int x = 0, i = min; i <= max; i++, x++)
            {
                int index = i;
                if (index < sourceMin)
                {
                    index = sourceMin;
                }

                if (index > sourceMax)
                {
                    index = sourceMax;
                }

                float weight = sampler.GetValue((index - point) / scale);
                sum += weight;
                Unsafe.Add(ref weightsBaseRef, x) = weight;
            }

            if (sum > 0)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    ref float wRef = ref Unsafe.Add(ref weightsBaseRef, i);
                    wRef = wRef / sum;
                }
            }
        }

        /// <summary>
        /// Calculated the weights for the given point.
        /// </summary>
        /// <param name="sourceMin">The minimum source bounds</param>
        /// <param name="sourceMax">The maximum source bounds</param>
        /// <param name="point">The transformed point dimension</param>
        /// <param name="sampler">The sampler</param>
        /// <param name="weights">The collection of weights</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateWeightsScaleUp(int sourceMin, int sourceMax, float point, IResampler sampler, Span<float> weights)
        {
            ref float weightsBaseRef = ref weights[0];
            for (int x = 0, i = sourceMin; i <= sourceMax; i++, x++)
            {
                float weight = sampler.GetValue(i - point);
                Unsafe.Add(ref weightsBaseRef, x) = weight;
            }
        }

        /// <summary>
        /// Calculates the sampling radius for the current sampler
        /// </summary>
        /// <param name="sourceSize">The source dimension size</param>
        /// <param name="destinationSize">The destination dimension size</param>
        /// <returns>The radius, and scaling factor</returns>
        private (float radius, float scale, float ratio) GetSamplingRadius(int sourceSize, int destinationSize)
        {
            float ratio = (float)sourceSize / destinationSize;
            float scale = ratio;

            if (scale < 1F)
            {
                scale = 1F;
            }

            return (MathF.Ceiling(scale * this.Sampler.Radius), scale, ratio);
        }
    }
}