// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides the base methods to perform non-affine transforms on an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ProjectiveTransformProcessor<TPixel> : InterpolatedTransformProcessorBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        /// <param name="targetDimensions">The target dimensions to constrain the transformed image to.</param>
        public ProjectiveTransformProcessor(Matrix4x4 matrix, IResampler sampler, Size targetDimensions)
            : base(sampler)
        {
            this.TransformMatrix = matrix;
            this.TargetDimensions = targetDimensions;
        }

        /// <summary>
        /// Gets the matrix used to supply the projective transform
        /// </summary>
        public Matrix4x4 TransformMatrix { get; }

        /// <summary>
        /// Gets the target dimensions to constrain the transformed image to
        /// </summary>
        public Size TargetDimensions { get; }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames =
                source.Frames.Select(x => new ImageFrame<TPixel>(source.GetConfiguration(), this.TargetDimensions.Width, this.TargetDimensions.Height, x.MetaData.DeepClone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(source.GetConfiguration(), source.MetaData.DeepClone(), frames);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
            int height = this.TargetDimensions.Height;
            int width = this.TargetDimensions.Width;

            Rectangle sourceBounds = source.Bounds();
            var targetBounds = new Rectangle(0, 0, width, height);

            // Since could potentially be resizing the canvas we might need to re-calculate the matrix
            Matrix4x4 matrix = this.GetProcessingMatrix(sourceBounds, targetBounds);

            // Convert from screen to world space.
            Matrix4x4.Invert(matrix, out matrix);
            const float Epsilon = 0.0000001F;

            if (this.Sampler is NearestNeighborResampler)
            {
                ParallelHelper.IterateRows(
                    targetBounds,
                    configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                Span<TPixel> destRow = destination.GetPixelRowSpan(y);

                                for (int x = 0; x < width; x++)
                                {
                                    var v3 = Vector3.Transform(new Vector3(x, y, 1), matrix);

                                    float z = MathF.Max(v3.Z, Epsilon);
                                    int px = (int)MathF.Round(v3.X / z);
                                    int py = (int)MathF.Round(v3.Y / z);

                                    if (sourceBounds.Contains(px, py))
                                    {
                                        destRow[x] = source[px, py];
                                    }
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

            // Using Vector4 with dummy 0-s, because Vector2 SIMD implementation is not reliable:
            var radius = new Vector4(xRadiusScale.radius, yRadiusScale.radius, 0, 0);

            IResampler sampler = this.Sampler;
            var maxSource = new Vector4(maxSourceX, maxSourceY, maxSourceX, maxSourceY);
            int xLength = (int)MathF.Ceiling((radius.X * 2) + 2);
            int yLength = (int)MathF.Ceiling((radius.Y * 2) + 2);

            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;

            using (Buffer2D<float> yBuffer = memoryAllocator.Allocate2D<float>(yLength, height))
            using (Buffer2D<float> xBuffer = memoryAllocator.Allocate2D<float>(xLength, height))
            {
                ParallelHelper.IterateRows(
                    targetBounds,
                    configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                ref TPixel destRowRef = ref MemoryMarshal.GetReference(destination.GetPixelRowSpan(y));
                                ref float ySpanRef = ref MemoryMarshal.GetReference(yBuffer.GetRowSpan(y));
                                ref float xSpanRef = ref MemoryMarshal.GetReference(xBuffer.GetRowSpan(y));

                                for (int x = 0; x < width; x++)
                                {
                                    // Use the single precision position to calculate correct bounding pixels
                                    // otherwise we get rogue pixels outside of the bounds.
                                    var v3 = Vector3.Transform(new Vector3(x, y, 1), matrix);
                                    float z = MathF.Max(v3.Z, Epsilon);

                                    // Using Vector4 with dummy 0-s, because Vector2 SIMD implementation is not reliable:
                                    Vector4 point = new Vector4(v3.X, v3.Y, 0, 0) / z;

                                    // Clamp sampling pixel radial extents to the source image edges
                                    Vector4 maxXY = point + radius;
                                    Vector4 minXY = point - radius;

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
                                        CalculateWeightsDown(
                                            top,
                                            bottom,
                                            minY,
                                            maxY,
                                            point.Y,
                                            sampler,
                                            yScale,
                                            ref ySpanRef,
                                            yLength);

                                        CalculateWeightsDown(
                                            left,
                                            right,
                                            minX,
                                            maxX,
                                            point.X,
                                            sampler,
                                            xScale,
                                            ref xSpanRef,
                                            xLength);
                                    }
                                    else
                                    {
                                        CalculateWeightsScaleUp(minY, maxY, point.Y, sampler, ref ySpanRef);
                                        CalculateWeightsScaleUp(minX, maxX, point.X, sampler, ref xSpanRef);
                                    }

                                    // Now multiply the results against the offsets
                                    Vector4 sum = Vector4.Zero;
                                    for (int yy = 0, j = minY; j <= maxY; j++, yy++)
                                    {
                                        float yWeight = Unsafe.Add(ref ySpanRef, yy);

                                        for (int xx = 0, i = minX; i <= maxX; i++, xx++)
                                        {
                                            float xWeight = Unsafe.Add(ref xSpanRef, xx);
                                            var vector = source[i, j].ToVector4();

                                            // Values are first premultiplied to prevent darkening of edge pixels
                                            Vector4 multiplied = vector.Premultiply();
                                            sum += multiplied * xWeight * yWeight;
                                        }
                                    }

                                    ref TPixel dest = ref Unsafe.Add(ref destRowRef, x);

                                    // Reverse the premultiplication
                                    dest.PackFromVector4(sum.UnPremultiply());
                                }
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
    }
}