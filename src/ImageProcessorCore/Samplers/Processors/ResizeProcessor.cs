// <copyright file="ResizeProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms.
    /// </summary>
    public class ResizeProcessor : ImageSampler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor"/> class.
        /// </summary>
        /// <param name="sampler">
        /// The sampler to perform the resize operation.
        /// </param>
        public ResizeProcessor(IResampler sampler)
        {
            Guard.NotNull(sampler, nameof(sampler));

            this.Sampler = sampler;
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets or sets the horizontal weights.
        /// </summary>
        protected Weights[] HorizontalWeights { get; set; }

        /// <summary>
        /// Gets or sets the vertical weights.
        /// </summary>
        protected Weights[] VerticalWeights { get; set; }

        /// <inheritdoc/>
        protected override void OnApply<T>(ImageBase<T> target, ImageBase<T> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (!(this.Sampler is NearestNeighborResampler))
            {
                this.HorizontalWeights = this.PrecomputeWeights(targetRectangle.Width, sourceRectangle.Width);
                this.VerticalWeights = this.PrecomputeWeights(targetRectangle.Height, sourceRectangle.Height);
            }
        }

        /// <inheritdoc/>
        protected override void Apply<T>(ImageBase<T> target, ImageBase<T> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            // Jump out, we'll deal with that later.
            if (source.Bounds == target.Bounds && sourceRectangle == targetRectangle)
            {
                return;
            }

            int width = target.Width;
            int height = target.Height;
            int sourceHeight = sourceRectangle.Height;
            int targetX = target.Bounds.X;
            int targetY = target.Bounds.Y;
            int targetRight = target.Bounds.Right;
            int targetBottom = target.Bounds.Bottom;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;
            bool compand = this.Compand;

            if (this.Sampler is NearestNeighborResampler)
            {
                // Scaling factors
                float widthFactor = sourceRectangle.Width / (float)targetRectangle.Width;
                float heightFactor = sourceRectangle.Height / (float)targetRectangle.Height;

                using (IPixelAccessor<T> sourcePixels = source.Lock())
                using (IPixelAccessor<T> targetPixels = target.Lock())
                {
                    Parallel.For(
                        startY,
                        endY,
                        y =>
                            {
                                if (targetY <= y && y < targetBottom)
                                {
                                    // Y coordinates of source points
                                    int originY = (int)((y - startY) * heightFactor);

                                    for (int x = startX; x < endX; x++)
                                    {
                                        if (targetX <= x && x < targetRight)
                                        {
                                            // X coordinates of source points
                                            int originX = (int)((x - startX) * widthFactor);
                                            targetPixels[x, y] = sourcePixels[originX, originY];
                                        }
                                    }

                                    this.OnRowProcessed();
                                }
                            });
                }

                // Break out now.
                return;
            }

            // Interpolate the image using the calculated weights.
            // A 2-pass 1D algorithm appears to be faster than splitting a 1-pass 2D algorithm 
            // First process the columns. Since we are not using multiple threads startY and endY
            // are the upper and lower bounds of the source rectangle.
            Image<T> firstPass = new Image<T>(target.Width, source.Height);
            using (IPixelAccessor<T> sourcePixels = source.Lock())
            using (IPixelAccessor<T> firstPassPixels = firstPass.Lock())
            using (IPixelAccessor<T> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    sourceHeight,
                    y =>
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            if (x >= 0 && x < width)
                            {
                                // Ensure offsets are normalised for cropping and padding.
                                int offsetX = x - startX;
                                float sum = this.HorizontalWeights[offsetX].Sum;
                                Weight[] horizontalValues = this.HorizontalWeights[offsetX].Values;

                                // Destination color components
                                //Color destination = new Color();

                                //for (int i = 0; i < sum; i++)
                                //{
                                //    Weight xw = horizontalValues[i];
                                //    int originX = xw.Index;
                                //    Color sourceColor = compand
                                //        ? Color.Expand(sourcePixels[originX, y])
                                //        : sourcePixels[originX, y];

                                //    destination += sourceColor * xw.Value;
                                //}

                                //if (compand)
                                //{
                                //    destination = Color.Compress(destination);
                                //}

                                //firstPassPixels[x, y] = destination;
                                Vector4 destination = new Vector4();

                                for (int i = 0; i < sum; i++)
                                {
                                    Weight xw = horizontalValues[i];
                                    int originX = xw.Index;
                                    Vector4 sourceColor = sourcePixels[originX, y].ToVector4();
                                    //Color sourceColor = compand
                                    //    ? Color.Expand(sourcePixels[originX, y])
                                    //    : sourcePixels[originX, y];
                                    destination += sourceColor * xw.Value;
                                }

                                //if (compand)
                                //{
                                //    destination = Color.Compress(destination);
                                //}
                                T packed = new T();
                                packed.PackVector(destination);

                                firstPassPixels[x, y] = packed;
                            }
                        }
                    });

                // Now process the rows.
                Parallel.For(
                    startY,
                    endY,
                    y =>
                    {
                        if (y >= 0 && y < height)
                        {
                            // Ensure offsets are normalised for cropping and padding.
                            int offsetY = y - startY;
                            float sum = this.VerticalWeights[offsetY].Sum;
                            Weight[] verticalValues = this.VerticalWeights[offsetY].Values;

                            for (int x = 0; x < width; x++)
                            {
                                // Destination color components
                                Vector4 destination = new Vector4();

                                for (int i = 0; i < sum; i++)
                                {
                                    Weight yw = verticalValues[i];
                                    int originY = yw.Index;
                                    //Color sourceColor = compand
                                    //    ? Color.Expand(firstPassPixels[x, originY])
                                    //    : firstPassPixels[x, originY];
                                    Vector4 sourceColor = firstPassPixels[x, originY].ToVector4();
                                    destination += sourceColor * yw.Value;
                                }

                                //if (compand)
                                //{
                                //    destination = Color.Compress(destination);
                                //}

                                T packed = new T();
                                packed.PackVector(destination);

                                targetPixels[x, y] = packed;
                            }
                        }

                        this.OnRowProcessed();
                    });

            }
        }

        /// <inheritdoc/>
        protected override void AfterApply<T>(ImageBase<T> target, ImageBase<T> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            // Copy the pixels over.
            if (source.Bounds == target.Bounds && sourceRectangle == targetRectangle)
            {
                target.ClonePixels(target.Width, target.Height, source.Pixels);
            }
        }

        /// <summary>
        /// Computes the weights to apply at each pixel when resizing.
        /// </summary>
        /// <param name="destinationSize">The destination section size.</param>
        /// <param name="sourceSize">The source section size.</param>
        /// <returns>
        /// The <see cref="T:Weights[]"/>.
        /// </returns>
        protected Weights[] PrecomputeWeights(int destinationSize, int sourceSize)
        {
            float scale = (float)destinationSize / sourceSize;
            IResampler sampler = this.Sampler;
            float radius = sampler.Radius;
            double left;
            double right;
            float weight;
            int index;
            int sum;

            Weights[] result = new Weights[destinationSize];

            // When shrinking, broaden the effective kernel support so that we still
            // visit every source pixel.
            if (scale < 1)
            {
                float width = radius / scale;
                float filterScale = 1 / scale;

                // Make the weights slices, one source for each column or row.
                for (int i = 0; i < destinationSize; i++)
                {
                    float centre = i / scale;
                    left = Math.Ceiling(centre - width);
                    right = Math.Floor(centre + width);

                    result[i] = new Weights
                    {
                        Values = new Weight[(int)(right - left + 1)]
                    };

                    for (double j = left; j <= right; j++)
                    {
                        weight = sampler.GetValue((float)((centre - j) / filterScale)) / filterScale;
                        if (j < 0)
                        {
                            index = (int)-j;
                        }
                        else if (j >= sourceSize)
                        {
                            index = (int)((sourceSize - j) + sourceSize - 1);
                        }
                        else
                        {
                            index = (int)j;
                        }

                        sum = (int)result[i].Sum++;
                        result[i].Values[sum] = new Weight(index, weight);
                    }
                }
            }
            else
            {
                // Make the weights slices, one source for each column or row.
                for (int i = 0; i < destinationSize; i++)
                {
                    float centre = i / scale;
                    left = Math.Ceiling(centre - radius);
                    right = Math.Floor(centre + radius);
                    result[i] = new Weights
                    {
                        Values = new Weight[(int)(right - left + 1)]
                    };

                    for (double j = left; j <= right; j++)
                    {
                        weight = sampler.GetValue((float)(centre - j));
                        if (j < 0)
                        {
                            index = (int)-j;
                        }
                        else if (j >= sourceSize)
                        {
                            index = (int)((sourceSize - j) + sourceSize - 1);
                        }
                        else
                        {
                            index = (int)j;
                        }

                        sum = (int)result[i].Sum++;
                        result[i].Values[sum] = new Weight(index, weight);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Represents the weight to be added to a scaled pixel.
        /// </summary>
        protected struct Weight
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Weight"/> struct.
            /// </summary>
            /// <param name="index">The index.</param>
            /// <param name="value">The value.</param>
            public Weight(int index, float value)
            {
                this.Index = index;
                this.Value = value;
            }

            /// <summary>
            /// Gets the pixel index.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Gets the result of the interpolation algorithm.
            /// </summary>
            public float Value { get; }
        }

        /// <summary>
        /// Represents a collection of weights and their sum.
        /// </summary>
        protected class Weights
        {
            /// <summary>
            /// Gets or sets the values.
            /// </summary>
            public Weight[] Values { get; set; }

            /// <summary>
            /// Gets or sets the sum.
            /// </summary>
            public float Sum { get; set; }
        }
    }
}